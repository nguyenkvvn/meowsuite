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
            if (args.Length == 0 || args.Length != 3)
            {
                Console.WriteLine("meowconcat usage:\n" +
                    "\t - arg1: target wav file to merge into" +
                    "\t - arg2: tool wav file" +
                    "\t - arg3: output file name");

                return;
            }

            MergeWavFile(args[0], args[1], args[2]);

        }

        static void MergeWavFile(string target_wav_path, string tool_wav_path, string output_file_path)
        {
            using (var reader1 = new WaveFileReader(target_wav_path))
            using (var reader2 = new WaveFileReader(tool_wav_path))
            {
                // Ensure formats are the same
                if (!reader1.WaveFormat.Equals(reader2.WaveFormat))
                {
                    throw new InvalidOperationException("WAV files must have the same format to be merged.");
                }

                // Create a new WAV file writer with the same format
                using (var writer = new WaveFileWriter(output_file_path, reader1.WaveFormat))
                {
                    // Write data from the first file
                    CopyAudioData(reader1, writer);

                    // Write data from the second file
                    CopyAudioData(reader2, writer);
                }
            }
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
