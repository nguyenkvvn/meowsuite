using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace meowsplit
{
    class Program
    {
        static void Main(string[] args)
        {
            // Parameters
            string audioFilePath = "";
            float silenceThreshold = 0.01f; // Adjust this value as needed (0.0 to 1.0)
            double minimumSilenceDuration = 0.5; // Minimum duration of silence in seconds

            // Check if parameters are provided
            /// In the case where the user doesn't give anything, explain to the user what the program does.
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: SilenceDetector.exe <audiofilepath> (silenceThreshold) (minimumSilenceDuration)");
                return;
            }
            /// If the user only gives a file path, use defaults.
            else if (args.Length == 1)
            {
                if (File.Exists(args[0]))
                {
                    audioFilePath = args[0];
                }
                else
                {
                    Console.WriteLine("Error! That file path is not valid. (Does the file exist, or is it accessible?)");
                }

            }
            /// If the user gives the file path, and a silence threshold
            else if (args.Length == 2)
            {
                if (File.Exists(args[0]))
                {
                    audioFilePath = args[0];
                }
                else
                {
                    Console.WriteLine("Error! That file path is not valid. (Does the file exist, or is it accessible?)");
                }
                try
                {
                    silenceThreshold = float.Parse(args[1]);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error! silenceThreshold must be a float value...");
                    return;
                }
            }
            /// If the user gives the file path, and a silence threshold, and finally a minimum silence duration
            else
            {
                if (File.Exists(args[0]))
                {
                    audioFilePath = args[0];
                }
                else
                {
                    Console.WriteLine("Error! That file path is not valid. (Does the file exist, or is it accessible?)");
                }
                try
                {
                    silenceThreshold = float.Parse(args[1]);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error! silenceThreshold must be a float value...");
                    return;
                }

                try
                {
                    minimumSilenceDuration = float.Parse(args[2]);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error! minimumSilenceDuration must be a double value...");
                    return;
                }
            }

            // List to store silence intervals
            List<(double start, double end)> silenceIntervals = detectSilence(new AudioFileReader(audioFilePath), silenceThreshold, minimumSilenceDuration);

            // Output the results
            Console.WriteLine("\nSilence intervals detected:");
            foreach (var interval in silenceIntervals)
            {
                Console.WriteLine($"Start: {interval.start:F2}s, End: {interval.end:F2}s");
            }

            double trackDuration = 0.0;

            using (var reader = new AudioFileReader(audioFilePath))
            {
                trackDuration = reader.TotalTime.TotalSeconds;
            }

            //  Output track
            Console.WriteLine("Track intervals interpolated:");
            List<(double start, double end)> tracks = invertSilence(silenceIntervals, trackDuration);
            foreach ((double s, double e) in tracks)
            {
                Console.WriteLine($"Start: {s:F2}s, End: {e:F2}s");
            }

            splitAudio(audioFilePath, tracks, Path.GetDirectoryName(audioFilePath));

        }

        /// <summary>
        /// Calculates periods of silence in a given audio file
        /// </summary>
        /// <param name="afr_handler">Audio file stream</param>
        /// <param name="silence_threshold">In decibels, </param>
        /// <param name="minimum_silence_duration">In seconds, how long a period of silence should persist to count as silence</param>
        /// <returns>Returns a list of periods of silence in a given audio file</returns>
        public static List<(double, double)> detectSilence(AudioFileReader afr_handler, float silence_threshold, double minimum_silence_duration)
        {
            // Create a list to store intervals of silence
            List<(double start, double end)> silence_intervals = new List<(double start, double end)>();

            //  Read the audio file
            using (var reader = afr_handler)
            {
                // Calculate buffer size
                int sample_rate = reader.WaveFormat.SampleRate;
                int channels = reader.WaveFormat.Channels;
                float[] buffer = new float[sample_rate * channels]; // Buffer for 1s of audio

                //  Tracking variables
                int samples_read;
                bool in_Silence = false;
                long silence_start_frame = 0;
                long total_frames_read = 0;

                /// As long as the samples we are reading are greater than zero
                while ((samples_read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // The frames we have to read is equal to the samples we've read divided by channels
                    int frames_read = samples_read / channels;

                    //  For every frame
                    for (int i = 0; i < frames_read; i++)
                    {
                        // Calculate the amplitude (aberaged across all channels)
                        float amplitude = 0;

                        /// For every channel
                        for (int c = 0; c < channels; c++)
                        {
                            /// we will sum up the amplitude
                            amplitude += Math.Abs(buffer[i * channels + c]);
                        }

                        /// Then divide it by channels to get the average
                        amplitude = amplitude / channels;

                        //  Calculate the current time position
                        double current_time = (double)(total_frames_read + i) / sample_rate;

                        //  Determine if we are in silence
                        /// if the amplitude is less than the silence threshold
                        if (amplitude <= silence_threshold)
                        {
                            /// if we are not in a state of silence
                            if (!in_Silence)
                            {
                                /// and we detect silence, take note of the current frame where it began
                                in_Silence = true;
                                silence_start_frame = total_frames_read + i;

                            }
                        }
                        /// if the amplitude is GREATER than the silence threshold
                        else
                        {
                            /// if we are in a state of silence, then we have left it
                            if (in_Silence)
                            {
                                in_Silence = false;
                                long silence_end_frame = total_frames_read + i;

                                /// the start time of silence is the frame divided by the sample rate
                                /// (we have the location of the silence start frame from when we entered the silent state
                                double silence_start_time = (double)silence_start_frame / sample_rate;
                                /// ditto for the end time of silence, which we just captured above
                                double silence_end_time = (double)silence_end_frame / sample_rate;

                                // so now we can have the total duration of silence
                                double silence_duration = silence_end_time - silence_start_time;

                                // To eliminate false positives of silence (an oxymoron), we will check if the duration is longer than our minimum silence duration before tracking it
                                if (silence_duration >= minimum_silence_duration)
                                {
                                    silence_intervals.Add((silence_start_time, silence_end_time));
                                }
                            }
                        }

                    }

                    total_frames_read += frames_read;

                }

                // Handle the case if a file ends in silence
                /// if we are in a state of silence, then we have left it
                if (in_Silence)
                {
                    in_Silence = false;
                    long silence_end_frame = total_frames_read;

                    /// the start time of silence is the frame divided by the sample rate
                    /// (we have the location of the silence start frame from when we entered the silent state
                    double silence_start_time = (double)silence_start_frame / sample_rate;
                    /// ditto for the end time of silence, which we just captured above
                    double silence_end_time = (double)silence_end_frame / sample_rate;

                    // so now we can have the total duration of silence
                    double silence_duration = silence_end_time - silence_start_time;

                    // To eliminate false positives of silence (an oxymoron), we will check if the duration is longer than our minimum silence duration before tracking it
                    if (silence_duration >= minimum_silence_duration)
                    {
                        silence_intervals.Add((silence_start_time, silence_end_time));
                    }
                }
            }

            return silence_intervals;
        }

        public static string exportTimestampsToCSV(List<(double, double)> segments)
        {
            string csv_string = "";

            //  Create CSV header
            csv_string += "track start, track end, track name, track artist" + "\n";

            foreach (var d in segments)
            {
                csv_string += d.Item1 + "," + d.Item2 + ",,";
            }

            return csv_string;
        }

        public static List<(double, double)> invertSilence(List<(double start, double end)> silence_segments, double track_length)
        {
            List<(double, double)> segments = new List<(double, double)>();

            double previous_silence_interval_start = 0.0;
            double previous_silence_interval_end = 0.0;
            double current_silence_inverval_start = 0.0;
            double current_silence_interval_end = 0.0;

            foreach ((double s, double e) in silence_segments)
            {
                previous_silence_interval_start = current_silence_inverval_start;
                previous_silence_interval_end = current_silence_interval_end;

                //  Handle a case of where we begin with silence
                if (s == 0.0 || e == track_length)
                {
                    // redundant: beginning_of_silence = s;
                    //  Skip over this segment
                    continue;
                }

                current_silence_inverval_start = s;
                current_silence_interval_end = e;

                segments.Add((previous_silence_interval_end, current_silence_inverval_start));

            }

            return segments;

        }

        public static void splitAudio(string input_file_path, List<(double start, double end)> segments, string output_directory)
        {
            using (var reader = new AudioFileReader(input_file_path))
            {
                int sample_rate = reader.WaveFormat.SampleRate;
                int channels = reader.WaveFormat.Channels;
                int bytes_per_sample = reader.WaveFormat.BitsPerSample / 8;
                int block_align = reader.WaveFormat.BlockAlign;

                //  For every segment we want to split
                foreach (var (start, end) in segments)
                {
                    string output_file_name = $"Segment_{start:F2}-{end:F2}.wav";
                    string output_file_path = Path.Combine(output_directory, output_file_name);

                    using (var writer = new WaveFileWriter(output_file_path, reader.WaveFormat))
                    {
                        long start_position = (long)(start * reader.WaveFormat.AverageBytesPerSecond);
                        long end_position = (long)(end * reader.WaveFormat.AverageBytesPerSecond);

                        //  Adjust positions to align with block boundaries
                        start_position = (start_position / block_align) * block_align;
                        end_position = (end_position / block_align) * block_align;

                        //  Ensure positions are within the audio file length
                        if (start_position < 0)
                        {
                            start_position = 0;
                        }
                        if (end_position > reader.Length)
                        {
                            end_position = reader.Length;
                        }

                        //  Set reader position
                        reader.Position = start_position;

                        byte[] buffer = new byte[1024 * block_align];

                        while (reader.Position < end_position)
                        {
                            int bytes_required = (int)Math.Min(end_position - reader.Position, buffer.Length);
                            int bytes_read = reader.Read(buffer, 0, bytes_required);

                            if (bytes_read > 0)
                            {
                                writer.Write(buffer, 0, bytes_read);
                            }
                            else
                            {
                                break;
                            }
                        }

                    }

                    Console.WriteLine($"Segment written to: {output_file_path}");
                }
            }


        }

    }
}

