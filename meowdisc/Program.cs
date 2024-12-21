using meow_common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace meowdisc
{
    class Program
    {
        static void Main(string[] args)
        {
            // Define the directory path to search and the file extension to look for
            string directoryPath = args[0];
            string fileExtension = args[1];  // Change to desired extension

            List<Track> tracks = new List<Track>();

            // Check if the directory exists
            if (Directory.Exists(directoryPath))
            {
                // Get all files with the specified extension
                string[] files = Directory.GetFiles(directoryPath, "*" + fileExtension, SearchOption.AllDirectories);

                // Iterate through each file found
                foreach (var file in files)
                {
                    Console.WriteLine("Found file: " + file);

                    tracks.Add(new Track
                    {
                        track_start = 0.0,
                        track_end = 0.0,
                        track_length = 0.0,
                        track_name = "",
                        track_artist = "",
                        track_album = "",
                        file_name = Path.GetFileNameWithoutExtension(file),
                        track_album_art_file_name = ""
                    });
                }
            }
            else
            {
                Console.WriteLine("Directory does not exist: " + directoryPath);
            }

            //  Dump the CSV for later processing
            MeowCommon.exportStringToFile(MeowCommon.exportTracksToCSV(tracks), directoryPath + "\\" + $"{Path.GetFileNameWithoutExtension(directoryPath)}_INFO.csv");

        }
    }
}
