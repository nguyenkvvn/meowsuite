using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace meowconcat
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine("meowconcat usage:\n" +
                    "\t - arg1: output file name" +
                    "\t - argN: tool wav files");

                return;
            }

            List<string> tracks_to_merge = args.ToList();

            tracks_to_merge.RemoveAt(0);

            MergeWavFiles(tracks_to_merge, args[0]);

        }

        static void MergeWavFiles(List<string> wavFiles, string outputFilePath)
        {
            // Validate input files
            if (wavFiles.Count == 0)
            {
                Console.WriteLine("No WAV files provided for merging.");
                return;
            }

            // Open the first file to get the WaveFormat
            using (var reader = new WaveFileReader(wavFiles[0]))
            {
                var waveFormat = reader.WaveFormat;

                // Create the output WAV file with the same format
                using (var writer = new WaveFileWriter(outputFilePath, waveFormat))
                {
                    foreach (var wavFile in wavFiles)
                    {
                        Console.WriteLine($"Processing: {wavFile}");

                        using (var fileReader = new WaveFileReader(wavFile))
                        {
                            // Check if the formats match
                            if (!fileReader.WaveFormat.Equals(waveFormat))
                            {
                                throw new InvalidOperationException("All WAV files must have the same format.");
                            }

                            // Read data in chunks and write to the output file
                            byte[] buffer = new byte[1024];
                            int bytesRead;
                            while ((bytesRead = fileReader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                writer.Write(buffer, 0, bytesRead);
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"Files merged successfully into: {outputFilePath}");
        }

        static void CopyAudioData(WaveFileReader reader, WaveFileWriter writer)
        {
            byte[] buffer = new byte[1024];
            int bytesRead;

            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                writer.Write(buffer, 0, bytesRead);
            }
        }
    }
}
