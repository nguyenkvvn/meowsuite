using meow_common;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            double minimumSilenceDuration = 0.25; // Minimum duration of silence in seconds
                //  0.25s - very aggressive
                //  0.5s - standard
                //  0.75s & > - very lax

            // Check if parameters are provided
            /// In the case where the user doesn't give anything, explain to the user what the program does.
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: meowsplit <audiofilepath> (silenceThreshold) (minimumSilenceDuration)");
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
                    Console.WriteLine("Error! silenceThreshold must be a float value... " + e.Message);
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
                    Console.WriteLine("Error! silenceThreshold must be a float value..." + e.Message);
                    return;
                }

                try
                {
                    minimumSilenceDuration = float.Parse(args[2]);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error! minimumSilenceDuration must be a double value..." + e.Message);
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

            //  Figure out the total length of the track
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

            //  Split the audio
            splitAudio(audioFilePath, tracks, Path.GetDirectoryName(audioFilePath));

            //  Dump the CSV for later processing
            MeowCommon.exportStringToFile(MeowCommon.exportTimestampsToCSV(tracks, audioFilePath), Path.GetDirectoryName(audioFilePath) + "\\" + $"{Path.GetFileNameWithoutExtension(audioFilePath)}_INFO.csv");

            
            //  Convert audio files to MP3
            /*List<Track> test_reimport = MeowCommon.importCSVtoTracks(Path.GetDirectoryName(audioFilePath) + "\\" + $"{Path.GetFileNameWithoutExtension(audioFilePath)}_INFO.csv");
            foreach (Track t in test_reimport)
            {
                string arguments = "-i " + "\"" + Path.GetDirectoryName(audioFilePath) + "\\" + t.file_name + ".wav\" " 
                    + "-codec:a libmp3lame -qscale:a 2 "
                    + "\"" + Path.GetDirectoryName(audioFilePath) + "\\" + t.file_name + ".mp3\" "
                    ;

                try
                {
                    // Create a new process start info
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = @"C:\includes\ffmpeg.exe",  // Application path
                        Arguments = arguments, // Arguments to pass
                        UseShellExecute = false, // Set to true if you want to open in shell
                        RedirectStandardOutput = true, // Capture output
                        RedirectStandardError = true, // Capture errors
                        CreateNoWindow = false // Do not show a console window
                    };

                    // Start the process
                    using (Process process = Process.Start(startInfo))
                    {
                        // Optional: Read output and error streams

                        Console.WriteLine($"Converting to mp3...");

                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        // Wait for the process to exit
                        process.WaitForExit();

                        // Output the results
                        Console.WriteLine("Output:");
                        Console.WriteLine(output);

                        if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine("Errors:");
                            Console.WriteLine(error);
                        }

                        Console.WriteLine($"Process exited with code: {process.ExitCode}");

                        if (process.ExitCode == 0)
                        {
                            Console.WriteLine($"Deleting .wav file...");

                            try
                            {
                                File.Delete(Path.GetDirectoryName(audioFilePath) + "\\" + t.file_name + ".wav");
                            }
                            catch (Exception exx)
                            {
                                Console.WriteLine("Error! " + exx.Message);
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error! " + e.Message);
                }
                
            }*/
        }

        /// <summary>
        /// Calculates periods of silence in a given audio file
        /// </summary>
        /// <param name="afr_handler">Audio file stream</param>
        /// <param name="silence_threshold">In decibels, what the "bar" ought to be for amplitude to fall under to be considered in a state of silence </param>
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

        /// <summary>
        /// Returns the inverted periods of sound from a given list of silence
        /// </summary>
        /// <param name="silence_segments"></param>
        /// <param name="track_length"></param>
        /// <returns></returns>
        public static List<(double, double)> invertSilence(List<(double start, double end)> silence_segments, double track_length)
        {
            //  Create a list to return of the segments with music
            List<(double, double)> segments = new List<(double, double)>();

            //  Sort silence periods by start time
            silence_segments.Sort((_s, _e) => _s.start.CompareTo(_e.start));

            /// Holder variable to track last previous end
            double previous_end = 0.0;
            
            //  For every period of silence,
            foreach (var (silence_start, silence_end) in silence_segments)
            {
                //  If there is a gap between the previous end and silence start
                if (previous_end < silence_start)
                {
                    //  And that period of sound is NOT smaller than X sec
                    if (!(silence_start - previous_end < 2.0))
                    {
                        //  Add the sound period
                        segments.Add((previous_end, silence_start));
                    }
                }

                //  Update the previous_end to the end of the current silence
                previous_end = Math.Max(previous_end, silence_end);
            }

            //  Handle a 
            if (previous_end < track_length)
            {
                if (!(track_length - previous_end < 2.0))
                {
                    segments.Add((previous_end, track_length));
                }
                
            }

            return segments;

        }

        /// <summary>
        /// Splits an audio file at the given segments
        /// </summary>
        /// <param name="input_file_path"></param>
        /// <param name="segments"></param>
        /// <param name="output_directory"></param>
        public static void splitAudio(string input_file_path, List<(double start, double end)> segments, string output_directory)
        {
            using (var reader = new AudioFileReader(input_file_path))
            {
                int sample_rate = reader.WaveFormat.SampleRate;
                int channels = reader.WaveFormat.Channels;
                int bytes_per_sample = reader.WaveFormat.BitsPerSample / 8;
                int block_align = reader.WaveFormat.BlockAlign;

                //  For every segment we want to split
                foreach (var track in segments)
                {
                    int track_no = segments.IndexOf(track) + 1;
                    //string output_file_name = $"{Path.GetFileNameWithoutExtension(input_file_path)}_Segment_{start:F2}-{end:F2}.wav";
                    //string output_file_name = "Track " + track_no + ".wav";
                    string output_file_name = $"{Path.GetFileNameWithoutExtension(input_file_path)}_Track_{track_no:F2}.wav";
                    string output_file_path = Path.Combine(output_directory, output_file_name);

                    using (var writer = new WaveFileWriter(output_file_path, reader.WaveFormat))
                    {
                        long start_position = (long)(track.start * reader.WaveFormat.AverageBytesPerSecond);
                        long end_position = (long)(track.end * reader.WaveFormat.AverageBytesPerSecond);

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

