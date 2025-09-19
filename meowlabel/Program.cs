using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using CsvHelper;
using System.Globalization;

class Program
{
    /// <summary>
    /// The OpenAI API endpoint
    /// </summary>
    private static readonly string apiUrl = "https://api.openai.com/v1/chat/completions"; // OpenAI API endpoint

# if DEBUG
    private static string apiKey = meowlabel.Properties.Settings.Default.OPENAI_API_KEY; // Replace with your API key
#else
    private static string apiKey;
#endif

    // Synchronous Main method as the entry point
    static void Main(string[] args)
    {
        // Make sure there is a image to transcribe
        if (args.Length == 0)
        {
            Console.WriteLine("Error: Please give a path to the tracklist image to transcribe.");
            return;
        }

        // Make sure the ChatGPT API key is present
        //string apiKey = meowlabel.Properties.Settings.Default.OPENAI_API_KEY;

        if (apiKey == null || apiKey.Length == 0)
        {
            Console.WriteLine("Error: Please set a ChatGPT API key. Use `meowlabel -apikey [your_api_key]`.");
            return;
        }

        // If the user wishes to set an API key...
        if (args[0] == "-apikey")
        {
            if (args.Length == 1)
            {
                Console.WriteLine("Error: Please provide an API key.");
                return;
            }

            apiKey = args[1];
            meowlabel.Properties.Settings.Default.OPENAI_API_KEY = args[1];
            meowlabel.Properties.Settings.Default.Save();

            Console.WriteLine("Info: Your API key has been set.");
            return;
        }

        if (args[0] == "merge")
        {
            if (args.Length != 4 )
            {
                Console.WriteLine("Error: Incomplete parameters.");
                return;
            }

            MergeByPosition(args[1], args[2], args[3]);
            return;
        }

        string imagePath = args[0];

        if (File.Exists(imagePath))
        {
            try
            {
                // Ask ChatGPT to read our image for us
                Console.WriteLine("Asking ChatGPT...");
                string response = ProcessImage(imagePath);

                // Parse the response
                var gpt_response = JObject.Parse(response);

                string content = gpt_response["choices"]?[0]?["message"]?["content"]?.ToString();

                SaveToCsv(content);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("File not found. Please check the path and try again.");
        }
    }

    public static void MergeByPosition(string timingCsv, string metaCsv, string outputCsv)
    {
        // 1) Read everything into memory (so writing cannot interfere with reads)
        var timingRows = ReadAllRows(timingCsv); // rows w/o header
        var metaRows = ReadAllRows(metaCsv);   // rows w/o header

        // 2) Build all output lines in memory
        var header = new[]
        {
            "track start","track end","track length",
            "track name","track artist","track album",
            "audio file name","album art file name"
        };

        var sb = new StringBuilder(1024 * 128);
        using (var sw = new StringWriter(sb))
        using (var csvOut = new CsvWriter(sw, CultureInfo.InvariantCulture))
        {
            foreach (var h in header) csvOut.WriteField(h);
            csvOut.NextRecord();

            int maxRows = Math.Max(timingRows.Count, metaRows.Count);
            for (int i = 0; i < maxRows; i++)
            {
                var t = (i < timingRows.Count) ? timingRows[i] : Array.Empty<string>();
                var m = (i < metaRows.Count) ? metaRows[i] : Array.Empty<string>();

                string trackStart = Get(t, 0);
                string trackEnd = Get(t, 1);
                string trackLength = Get(t, 2);
                string trackName = Get(m, 0);
                string trackArtist = Get(m, 1);
                string trackAlbum = Get(m, 2);
                string audioFile = Get(t, 3);
                string artFile = Get(t, 4);

                csvOut.WriteField(trackStart);
                csvOut.WriteField(trackEnd);
                csvOut.WriteField(trackLength);
                csvOut.WriteField(trackName);
                csvOut.WriteField(trackArtist);
                csvOut.WriteField(trackAlbum);
                csvOut.WriteField(audioFile);
                csvOut.WriteField(artFile);
                csvOut.NextRecord();
            }
        }

        // 3) Write once to a brand-new file (prevents read/write collisions)
        //    Also ensures everything is flushed.
        File.WriteAllText(outputCsv, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static List<string[]> ReadAllRows(string path)
    {
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var reader = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);
        var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // If your files have headers, skip them:
        if (csv.Read()) { csv.ReadHeader(); }

        var rows = new List<string[]>();
        while (csv.Read())
        {
            // Clone the parser buffer so it doesn't mutate
            var row = csv.Parser.Record?.ToArray() ?? Array.Empty<string>();
            rows.Add(row);
        }
        return rows;
    }

    private static string Get(string[] arr, int idx) =>
        (arr != null && idx >= 0 && idx < arr.Length) ? arr[idx] : string.Empty;

    /// <summary>
    /// Queries ChatGPT to transcribe a track list image
    /// </summary>
    /// <param name="imagePath">The path to the image to transcribe</param>
    /// <returns>The CSV output from ChatGPT</returns>
    static string ProcessImage(string imagePath)
    {
        var url = "https://api.openai.com/v1/chat/completions";

        using (var client = new HttpClient())
        using (var form = new MultipartFormDataContent())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Add prompt
            var base64String = Convert.ToBase64String(File.ReadAllBytes(imagePath));

            var messagesJson = new JArray
            {
                new JObject
                {
                    { "role", "user" },
                    { "content", new JArray
                        {
                            new JObject
                            {
                                { "type", "text" },
                                { "text", "You are a transcription service. Return nothing but the transcribed track lists from cassette tape images in CSV format with the following columns: track name, track artist, and track album. Replace commas with dashes in names and artists." }
                            },
                            new JObject
                            {
                                { "type", "image_url" },
                                { "image_url", new JObject
                                    {
                                        { "url", "data:image/jpeg;base64," + base64String }
                                    }
                                }
                            }
                        }
                    }
                }
            };


            var requestBody = new JObject
                {
                    { "model", "gpt-4-turbo" },
                    { "messages", messagesJson },
                    { "max_tokens", 1000 }
                };

            var stringContent = new StringContent(requestBody.ToString());
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = client.PostAsync(url, stringContent).Result;
            var responseString = response.Content.ReadAsStringAsync().Result;

            return responseString;
        }
    }

    static void SaveToCsv(string csvData)
    {
        string outputPath = Path.Combine(Environment.CurrentDirectory, "transcription_result.csv");

        File.WriteAllText(outputPath, RemoveLinesWithTripleBackticks(csvData), Encoding.UTF8);
        Console.WriteLine($"Transcription saved to: {outputPath}");
    }

    static string RemoveLinesWithTripleBackticks(string input)
    {
        var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var filtered = lines.Where(line => !line.Contains("```"));
        return string.Join(Environment.NewLine, filtered);
    }
}
