using meow_common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace meowtag
{
    class Program
    {
        static void Main(string[] args)
        {
            //  Check if parameters are provided
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: meowtag <inputMetaCSV>");

                return;
            }

            merge_metadata_with_mp3s(args[0], Path.GetDirectoryName(args[0]));
        }

        public static void merge_metadata_with_mp3s(string input_CSV_path, string input_files_location_path)
        {
            //  Open up CSV and read in format
            List<Track> tracks = MeowCommon.importCSVtoTracks(input_CSV_path);

            //  Check and verify each segment or file mentioned in the CSV is present in the input_files_location_path
            //  Iterate over each row and merge in 
            foreach (Track t in tracks)
            {
                string current_file_path_MP3 = input_files_location_path + "\\" + t.file_name + ".mp3";

                int friendly_index = tracks.IndexOf(t) + 1;

                bool already_renamed = false;

                if (!(System.IO.File.Exists(current_file_path_MP3)))
                {
                    
                    string post_wav_renamed_file_name = friendly_index + ". " + t.track_artist + " - " + t.track_name;

                    current_file_path_MP3 = input_files_location_path + "\\" + post_wav_renamed_file_name + ".mp3";

                    already_renamed = true;
                }

                string current_file_path_WAV = input_files_location_path + "\\" + t.file_name + ".wav";

                if (System.IO.File.Exists(current_file_path_MP3))
                {
                    

                    Console.WriteLine("[Info] Tagging: " + friendly_index + " [" + t.track_album + "] " + t.track_artist + " - " + t.track_name);
                    try
                    {
                        //  Open the MP3 file
                        var file = TagLib.File.Create(current_file_path_MP3);

                        //  Insert in the metadata
                        file.Tag.Title = t.track_name;
                        file.Tag.Performers = new[] { t.track_artist };
                        file.Tag.Album = t.track_album;
                        
                        /// Advance the track number by 1 because a track number of 0 is invalid
                        file.Tag.Track = (uint)tracks.IndexOf(t) + 1;

                        //  Insert the album art if applicable
                        if ( !(String.IsNullOrWhiteSpace(t.track_album_art_file_name)) 
                            && !(String.IsNullOrEmpty(t.track_album_art_file_name)) 
                            && (File.Exists(input_files_location_path + "\\" + t.track_album_art_file_name)))
                        {
                            byte[] album_art_data = File.ReadAllBytes(input_files_location_path + "\\" + t.track_album_art_file_name);

                            string mime_type = "";

                            //  Determine if JPG or PNG
                            if (album_art_data[0] == 0xFF && album_art_data[1] == 0xD8 && album_art_data[2] == 0xFF)
                            {
                                mime_type = "image/jpeg";
                            }
                            else if (album_art_data[0] == 0x89 && album_art_data[1] == 0x50 && album_art_data[2] == 0x4E && album_art_data[3] == 0x47 &&
                 album_art_data[4] == 0x0D && album_art_data[5] == 0x0A && album_art_data[6] == 0x1A && album_art_data[7] == 0x0A)
                            {
                                mime_type = "image/png";
                            }
                            else
                            {
                                Console.WriteLine("Error! Album art is an unsupported format. (jpg and png only)");
                            }

                            var album_art = new TagLib.Picture(new TagLib.ByteVector(album_art_data))
                            {
                                Type = TagLib.PictureType.FrontCover,
                                MimeType = mime_type,
                                Description = "Album art"
                            };

                            file.Tag.Pictures = new[] { album_art };
                        }
                        else
                        {
                            Console.WriteLine("WARNING! Album art not found: " + input_files_location_path + "\\" + t.track_album_art_file_name);
                        }

                        // Save our changes
                        file.Save();
                        file.Dispose();

                        // Rename the file to something readable and sensical
                        if (!already_renamed)
                        {
                            File.Move(t.file_name + ".mp3", (tracks.IndexOf(t) + 1) + ". " + t.track_artist + " - " + t.track_name + ".mp3");
                        }
                        


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error! " + ex.Message);
                    }
                }
                else
                {
                    Console.WriteLine("WARNING! File not found: " + current_file_path_MP3);
                }

                //  Add a simple rename routine for WAV files
                if (System.IO.File.Exists(current_file_path_WAV))
                {
                    // Rename the file to something readable and sensical
                    File.Move(t.file_name + ".wav", (tracks.IndexOf(t) + 1) + ". " + t.track_artist + " - " + t.track_name + ".wav");
                }
                else
                {
                    Console.WriteLine("WARNING! File not found: " + current_file_path_WAV);
                }

            }


        }
    }
}
