using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace meow_common
{
    public class MeowCommon
    {
        /// <summary>
        /// Exports a freshly made set of segments into a CSV string for populating metadata into
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="input_file_path"></param>
        /// <returns></returns>
        public static string exportTimestampsToCSV(List<(double, double)> segments, string input_file_path)
        {
            string csv_string = "";

            //  Create CSV header
            csv_string += "track start, track end, track length, track name, track artist, track album, audio file name, album art file name" + "\n";

            foreach (var d in segments)
            {
                int track_no = segments.IndexOf(d) + 1;
                csv_string += d.Item1 + "," + d.Item2 + "," + (d.Item2 - d.Item1) + ",,,," + $"{Path.GetFileNameWithoutExtension(input_file_path)}_Track_{track_no:F2},," + "\n";
            }

            return csv_string;
        }

        public static string exportTracksToCSV(List<Track> l_tracks)
        {
            string csv_string = "";

            //  Create CSV header
            csv_string += "track start, track end, track length, track name, track artist, track album, audio file name, album art file name" + "\n";

            foreach (Track t in l_tracks)
            {
                csv_string +=
                    t.track_start + "," +
                    t.track_end + "," +
                    t.track_length + "," +
                    t.track_name + "," +
                    t.track_artist + "," +
                    t.track_album + "," +
                    t.file_name + "," +
                    t.track_album_art_file_name + "\n";
            }

            return csv_string;
        }

        /// <summary>
        /// Imports a CSV of tracks into the List<Track> data structure 
        /// </summary>
        /// <param name="input_file_path"></param>
        /// <returns></returns>
        public static List<Track> importCSVtoTracks(string input_file_path)
        {
            List<Track> output_list = new List<Track>();

            try
            {
                using (var reader = new StreamReader(input_file_path))
                {
                    //  Read in the header row
                    var header = reader.ReadLine();

                    /// Check in case the file is empty
                    if (header == null)
                    {
                        throw new Exception("The CSV is empty.");
                    }

                    //  Read in subsequent rows
                    while (!reader.EndOfStream)
                    {
                        //  read in the current line
                        var line = reader.ReadLine();
                        //  Skip over if the line is empty or has a space
                        if (String.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var track_values = line.Split(',');

                        /// "track start, track end, track length, track name, track artist, track album, file name, album art path"
                        Track track = new Track
                        {
                            track_start = Double.Parse(track_values[0]),
                            track_end = Double.Parse(track_values[1]),
                            track_length = Double.Parse(track_values[2]),

                            track_name = track_values[3],
                            track_artist = track_values[4],
                            track_album = track_values[5],

                            file_name = track_values[6],
                            track_album_art_file_name = track_values[7]
                        };

                        output_list.Add(track);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured: " + ex.Message);
            }

            return output_list;
        }

        /// <summary>
        /// Exports all string content to file path
        /// </summary>
        /// <param name="content"></param>
        /// <param name="file_path"></param>
        public static void exportStringToFile(string content, string file_path)
        {
            File.WriteAllText(file_path, content);
        }
    }

    /// <summary>
    /// A class object representing the metadata of a track
    /// </summary>
    public class Track
    {
        /// <summary>
        /// In seconds, when does this track begin from the start of the sample
        /// </summary>
        public double track_start;
        /// <summary>
        /// In seconds, when does this track end, from the end of the sample
        /// </summary>
        public double track_end;
        /// <summary>
        /// In seconds, how long does this track play for.
        /// 
        /// This value is automatically calculated.
        /// </summary>
        public double track_length;

        /// <summary>
        /// The name of the track
        /// </summary>
        public string track_name;
        /// <summary>
        /// The primary artist on this track
        /// </summary>
        public string track_artist;
        /// <summary>
        /// The album this track is on
        /// </summary>
        public string track_album;

        /// <summary>
        /// The file path of the track that this metadata should be applied to
        /// </summary>
        public string file_name;

        /// <summary>
        /// The album art for this track
        /// </summary>
        public string track_album_art_file_name;

    }
}
