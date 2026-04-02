using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using TagLib;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            //  Get the current working directory we are in
            /// This should have the MP3s we want to put into a video
            string workingDir = Directory.GetCurrentDirectory();

            //  Check for FFMPEG and FFPROBE presence
            string ffmpegPath = Path.Combine(workingDir, "ffmpeg.exe"); // or just "ffmpeg" if in PATH
            if (!System.IO.File.Exists(ffmpegPath) && !IsOnPath("ffmpeg"))
            {
                Console.WriteLine("ffmpeg.exe was not found in the working folder or PATH.");
                return 1;
            }
            string ffmpegExe = System.IO.File.Exists(ffmpegPath) ? ffmpegPath : "ffmpeg";

            string ffprobePath = Path.Combine(workingDir, "ffprobe.exe");
            if (!System.IO.File.Exists(ffprobePath) && !IsOnPath("ffprobe"))
            {
                Console.WriteLine("ffprobe.exe was not found in the working folder or PATH.");
                return 1;
            }
            string ffprobeExe = System.IO.File.Exists(ffprobePath) ? ffprobePath : "ffprobe";

            // Collect all the MP3s in the current working directory
            string[] mp3Files = Directory.GetFiles(workingDir, "*.mp3")
                                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                         .ToArray();
            
            /// If there are none, get out.
            if (mp3Files.Length == 0)
            {
                Console.WriteLine("No MP3 files found in the working folder.");
                return 1;
            }

            /// Create our track listing structure
            var tracks = new List<TrackInfo>();
            TimeSpan runningTime = TimeSpan.Zero;

            foreach (string mp3 in mp3Files)
            {
                using (var tagFile = TagLib.File.Create(mp3))
                {
                    var info = new TrackInfo
                    {
                        FilePath = Path.GetFullPath(mp3),
                        FileName = Path.GetFileName(mp3),
                        Title = string.IsNullOrWhiteSpace(tagFile.Tag.Title)
                            ? Path.GetFileNameWithoutExtension(mp3)
                            : tagFile.Tag.Title,
                        Artist = tagFile.Tag.FirstPerformer ?? "",
                        Album = tagFile.Tag.Album ?? "",
                        Duration = GetAudioDurationWithFfprobe(ffprobeExe, mp3, workingDir),
                        StartTime = runningTime
                    };

                    tracks.Add(info);
                    runningTime += info.Duration;
                    Console.WriteLine("Found: " + info.Artist + " - " + info.Title + "(" + info.Duration + ")");
                }
            }

            // Grab album art, otherwise generate it if it doesn't exist.
            string imageFile = FindOrCreateImageFile(workingDir, tracks);
            if (imageFile == null)
            {
                string albumTitle = tracks.Select(t => t.Album)
                                          .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));

                imageFile = CreatePlaceholderImage(workingDir, albumTitle);
            }

            string folderName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;

            //  Create our concatenation working file to combine the MP3s into one wav for the video
            string concatPath = Path.Combine(workingDir, "concat.txt");
            WriteConcatFile(concatPath, tracks);

            //  Create our chapters description export.
            string chaptersPath = Path.Combine(workingDir, folderName + " - chapters.txt");
            WriteChaptersFile(chaptersPath, tracks);

            //  Set our export pathing
            /// This is the working audio file for our video containing the combined MP3s.
            string combinedAudioPath = Path.Combine(workingDir, "combined.wav");

            /// The name of our output mp4 video will be the name of the directory.
            string outputPath = Path.Combine(workingDir, folderName + ".mp4");

            // Pass 1: join MP3s into one WAV
            string joinArgs =
                $"-y " +
                $"-f concat -safe 0 -i {Quote(concatPath)} " +
                $"-vn " +
                $"{Quote(combinedAudioPath)}";

            Console.WriteLine("Joining MP3s into one audio file...");
            Console.WriteLine(joinArgs);

            int joinExitCode = RunProcess(ffmpegExe, joinArgs, workingDir);
            if (joinExitCode != 0)
            {
                Console.WriteLine($"Audio join failed with exit code {joinExitCode}");
                return joinExitCode;
            }

            string videoFilter = BuildVideoFilter(tracks, 1280, 720);
            Console.WriteLine("VIDEO FILTER:");
            Console.WriteLine(videoFilter);

            // Pass 2: create video from still image + combined audio
            string totalDuration = FormatFfmpegDuration(runningTime);

            string videoArgs =
                $"-y " +
                $"-loop 1 -framerate 1 -i {Quote(imageFile)} " +
                $"-i {Quote(combinedAudioPath)} " +
                $"-map 0:v:0 -map 1:a:0 " +
                $"-c:v libx264 -tune stillimage " +
                $"-vf \"{videoFilter}\" " +
                $"-c:a aac -b:a 320k " +
                $"-pix_fmt yuv420p " +
                $"-movflags +faststart " +
                $"-r 1 " +
                $"-shortest " +
                $"{Quote(outputPath)}";

            Console.WriteLine("Creating final MP4...");
            Console.WriteLine(videoArgs);

            int videoExitCode = RunProcess(ffmpegExe, videoArgs, workingDir);
            if (videoExitCode != 0)
            {
                Console.WriteLine($"Video creation failed with exit code {videoExitCode}");
                return videoExitCode;
            }

            Console.WriteLine("Done.");
            Console.WriteLine("Created:");
            Console.WriteLine(outputPath);
            Console.WriteLine(chaptersPath);

            // Cleanup
            System.IO.File.Delete(Path.Combine(workingDir, "concat.txt"));
            System.IO.File.Delete(Path.Combine(workingDir, "combined.wav"));

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }

    /// <summary>
    /// Looks for an album art cover "cover.jpg". If that doesn't exist, look for any image in the folder. And if THAT doesn't work, grab one from the first track.
    /// </summary>
    /// <param name="workingDir"></param>
    /// <param name="tracks"></param>
    /// <returns></returns>
    static string FindOrCreateImageFile(string workingDir, List<TrackInfo> tracks)
    {
        string preferred = Path.Combine(workingDir, "cover.jpg");
        if (System.IO.File.Exists(preferred))
            return preferred;

        string[] exts = new[] { "*.jpg", "*.jpeg", "*.png" };
        foreach (string ext in exts)
        {
            string found = Directory.GetFiles(workingDir, ext)
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                    .FirstOrDefault();
            if (found != null)
                return found;
        }

        // Fallback: extract embedded album art from first MP3 that has it
        foreach (var track in tracks)
        {
            using (var tagFile = TagLib.File.Create(track.FilePath))
            {
                if (tagFile.Tag.Pictures != null && tagFile.Tag.Pictures.Length > 0)
                {
                    var pic = tagFile.Tag.Pictures[0];
                    string ext = ".jpg";

                    if (!string.IsNullOrWhiteSpace(pic.MimeType))
                    {
                        string mime = pic.MimeType.ToLowerInvariant();
                        if (mime.Contains("png"))
                            ext = ".png";
                        else if (mime.Contains("jpeg") || mime.Contains("jpg"))
                            ext = ".jpg";
                    }

                    string extractedPath = Path.Combine(workingDir, "extracted_cover" + ext);
                    System.IO.File.WriteAllBytes(extractedPath, pic.Data.Data);
                    return extractedPath;
                }
            }
        }

        return null;
    }

    static string FormatFfmpegDuration(TimeSpan time)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1:00}:{2:00}.{3:000}",
            (int)time.TotalHours,
            time.Minutes,
            time.Seconds,
            time.Milliseconds);
    }

    static string CreatePlaceholderImage(string workingDir, string albumTitle)
    {
        string outputPath = Path.Combine(workingDir, "placeholder_cover.jpg");

        using (var bmp = new Bitmap(1280, 720))
        using (var g = Graphics.FromImage(bmp))
        using (var bgBrush = new SolidBrush(Color.FromArgb(24, 24, 24)))
        using (var textBrush = new SolidBrush(Color.White))
        using (var subBrush = new SolidBrush(Color.LightGray))
        using (var titleFont = new Font("Arial", 32, FontStyle.Bold))
        using (var subFont = new Font("Arial", 20, FontStyle.Regular))
        {
            g.FillRectangle(bgBrush, 0, 0, bmp.Width, bmp.Height);

            var sfCenter = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString(
                string.IsNullOrWhiteSpace(albumTitle) ? "Music Compilation" : albumTitle,
                titleFont,
                textBrush,
                new RectangleF(100, 250, 1080, 80),
                sfCenter);

            g.DrawString(
                "No album art found",
                subFont,
                subBrush,
                new RectangleF(100, 340, 1080, 50),
                sfCenter);

            bmp.Save(outputPath, ImageFormat.Jpeg);
        }

        return outputPath;
    }

    static void WriteConcatFile(string concatPath, List<TrackInfo> tracks)
    {
        using (var writer = new StreamWriter(
            concatPath,
            false,
            new System.Text.UTF8Encoding(false)))
        {
            foreach (var track in tracks)
            {
                string fullPath = Path.GetFullPath(track.FilePath);

                // Normalize first
                fullPath = fullPath.Replace("\\", "/");

                // THEN escape apostrophes
                fullPath = fullPath.Replace("'", "'\\''");

                string line = "file '" + fullPath + "'";

                Console.WriteLine(line); // debug

                writer.WriteLine(line);
            }
        }
    }

    /// <summary>
    /// Writes a text file containing the chapters based on the length of the songs
    /// </summary>
    /// <param name="chaptersPath"></param>
    /// <param name="tracks"></param>
    static void WriteChaptersFile(string chaptersPath, List<TrackInfo> tracks)
    {
        using (var writer = new StreamWriter(
            chaptersPath,
            false,
            new System.Text.UTF8Encoding(false)))
        {
            foreach (var track in tracks)
            {
                if (track.Artist != null)
                {
                    writer.WriteLine($"{FormatYouTubeTimestamp(track.StartTime)} {track.Artist} - {track.Title}");
                }
                else
                {
                    writer.WriteLine($"{FormatYouTubeTimestamp(track.StartTime)} {track.Title}");
                }
            }
        }
    }

    /// <summary>
    /// Creates the text overlay string for FFMPEG to render
    /// </summary>
    /// <param name="tracks"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    static string BuildVideoFilter(List<TrackInfo> tracks, int width, int height)
    {
        var parts = new List<string>();

        parts.Add(
            $"scale={width}:{height}:force_original_aspect_ratio=decrease," +
            $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2");

        parts.Add($"drawbox=x=40:y={height - 140}:w={width - 80}:h=80:color=black@0.55:t=fill");

        foreach (var track in tracks)
        {
            string label = string.IsNullOrWhiteSpace(track.Artist)
                ? track.Title
                : track.Artist + " - " + track.Title;

            string safeTitle = EscapeDrawtext(label);
            double start = track.StartTime.TotalSeconds;
            double end = track.EndTime.TotalSeconds;

            string draw =
                $"drawtext=" +
                $"text='{safeTitle}':" +
                $"x=(w-text_w)/2:" +
                $"y={height - 115}:" +
                $"fontsize=32:" +
                $"fontcolor=white:" +
                $"enable='between(t\\,{start.ToString(CultureInfo.InvariantCulture)}\\,{end.ToString(CultureInfo.InvariantCulture)})'";

            parts.Add(draw);
        }

        return string.Join(",", parts);
    }

    /// <summary>
    /// Probe for an accurate time span of an MP# file, rather than relying on MP3 ID tag (which may be inaccurate.)
    /// </summary>
    /// <param name="ffprobeExe"></param>
    /// <param name="filePath"></param>
    /// <param name="workingDir"></param>
    /// <returns></returns>
    static TimeSpan GetAudioDurationWithFfprobe(string ffprobeExe, string filePath, string workingDir)
    {
        string args =
            $"-v error -show_entries format=duration " +
            $"-of default=noprint_wrappers=1:nokey=1 " +
            $"{Quote(filePath)}";

        var psi = new ProcessStartInfo
        {
            FileName = ffprobeExe,
            Arguments = args,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = new Process())
        {
            process.StartInfo = psi;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception("ffprobe failed for file: " + filePath + Environment.NewLine + error);

            double seconds;
            if (!double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                throw new Exception("Could not parse ffprobe duration for file: " + filePath + Environment.NewLine + output);

            return TimeSpan.FromSeconds(seconds);
        }
    }

    static string EscapeDrawtext(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("'", "’")   // replace straight apostrophe with curly apostrophe
            .Replace("\\", "\\\\")
            .Replace(":", "\\:")
            .Replace(",", "\\,")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("%", "\\%")
            .Replace(";", "\\;");
    }

    static string FormatYouTubeTimestamp(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}",
                (int)time.TotalHours, time.Minutes, time.Seconds);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}",
            time.Minutes, time.Seconds);
    }

    static int RunProcess(string fileName, string arguments, string workingDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = new Process())
        {
            process.StartInfo = psi;

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    Console.WriteLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    Console.WriteLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return process.ExitCode;
        }
    }

    static string Quote(string path)
    {
        return "\"" + path + "\"";
    }

    static bool IsOnPath(string exeName)
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in path.Split(Path.PathSeparator))
        {
            try
            {
                string full = Path.Combine(dir.Trim(), exeName + ".exe");
                if (System.IO.File.Exists(full))
                    return true;

                full = Path.Combine(dir.Trim(), exeName);
                if (System.IO.File.Exists(full))
                    return true;
            }
            catch
            {
                // ignore invalid PATH entries
            }
        }
        return false;
    }

    class TrackInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get { return StartTime + Duration; } }
    }
}