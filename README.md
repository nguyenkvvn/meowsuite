# meowtag and meowsplit
This is the repository for meowsplit and meowtag.

Their functions is to split a continious .wav-form audio into segments based on periods of silence as delimiters, and then appropriately populate MP3 tags for converted segments respectively.

They were created to aid me on my quest to preserve content from, but not limited to, the Vietnamese diaspora.

## Suite of applications and their descriptions
- meowsplit, the titular name of the project, splits a .wav file into tracks based on the intervals of silence it can detect.
- meowtag applies the MP3 ID tags that you populate from the CSV created by meowsplit



## A use case on how you could use these applications

This is my workflow:

1. Backup my media which gets put into .aea or .wav format, depending on my method of backing up. (I also scan the album art.)
    `ffmpeg -i "my_audio.aea" "my_audio.wav"`
2. Use `meowsplit` to process and create segments from the media output.
    `meowsplit "my_audio.wav"`
    `& 'resources\Convert-MP3FFMPEG.ps1';`
3. Identify the content on the segments, either manually or with computer vision, then use Visual Studio Code to populate the .csv that gets created with the relevant metadata.
4. Use `meowtag` to populate the relevant .mp3 files with the metadata content, including album art if applicable.
    `meowtag "my_audio_INFO.csv"`

The end result is a reduced workflow tedium, leaving only the method of backing up the only factor dependent on equipment and audio playback, and only identifying the content on the segments limited by how fast I can query ChatGPT to transcribe scanned album art for me.