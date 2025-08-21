using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

class Program
{
    private static readonly string apiUrl = "https://api.openai.com/v1/chat/completions"; // OpenAI API endpoint
    private static readonly string apiKey = meowlabel.Properties.Settings.Default.OPENAI_API_KEY; // Replace with your API key

    // Synchronous Main method as the entry point
    static void Main(string[] args)
    {
        string imagePath = args[0];

        if (File.Exists(imagePath))
        {
            try
            {
                // Ask ChatGPT to read our image for us
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

    // Asynchronous method for processing the image
    static async Task<string> ProcessImageWithChatGPT(string imagePath)
    {
        string imageBase64 = Convert.ToBase64String(File.ReadAllBytes(imagePath));

        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = "gpt-4o",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a transcription assistant. Transcribe track lists from cassette tape images into CSV format with columns: track name, track artist, and track album. Replace commas with dashes in names and artists."
                    },
                    new
                    {
                        role = "user",
                        content = $"Here is the image data in base64 format: {imageBase64}"
                    }
                }
            };

            string jsonRequestBody = JsonConvert.SerializeObject(requestBody);

            HttpResponseMessage response = await httpClient.PostAsync(apiUrl, new StringContent(jsonRequestBody, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                dynamic responseObject = JsonConvert.DeserializeObject(jsonResponse);
                string transcription = responseObject.choices[0].message.content;
                return transcription;
            }
            else
            {
                throw new Exception($"Failed to process image: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
    }

    static void SaveToCsv(string csvData)
    {
        string outputPath = Path.Combine(Environment.CurrentDirectory, "transcription_result.csv");
        File.WriteAllText(outputPath, csvData, Encoding.UTF8);
        Console.WriteLine($"Transcription saved to: {outputPath}");
    }
}
