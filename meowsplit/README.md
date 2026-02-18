# meowsplit
Meowsplit is a small applet that implements NAudio to identify periods of silence in an audio track, and splits them appropriately, and then exports a CSV file with relevant information for metadata tagging.

## Usage
`meowsplit <path_to_wav> [silence_threshold] [minimum_silence_duration]`

Point meowsplit to the .wav file you wish to split silence into tracks out of. It will generate the approrpriate tracks in .wav, and also a .csv file for populating metadata into.

As a bonus, it will also invoke `ffmpeg` to convert the .wav files into .mp3 too.

### Parameters

- `silence_threshold` - _optional_ In decibels, between 1.0db and 0.0db, what the floor of silence should be considered. By default, this value is 0.01db.
- `minimumSilenceDuration` - _optional_ In seconds, how long a period of silence should be to warrant a track to be cut.
    - 0.25s - very aggressive
    - 0.5s - standard (default)
    - 0.75s - very lax

#### Suggested Parameters:
- `meowsplit '.\01. No title.wav' 0.025 .25` - used for vinyls played on a Sony PS-LX310BT
- _default options_ - used for cassettes played on a Sony CMT-M333NT

## How to build
Build using Visual Studio 2017, targeting .NET Framework 4.7.2. The NuGet package manager should automatically fetch the `nAudio` library.