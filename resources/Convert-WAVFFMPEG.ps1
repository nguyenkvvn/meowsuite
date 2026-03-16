# Use the current directory as the source and output folder
$sourceFolder = $PWD.Path
$outputFolder = $PWD.Path

# Ensure FFmpeg is accessible
$ffmpegPath = "ffmpeg"

# Get AEA files from the folder
$wavFiles = Get-ChildItem -Path $sourceFolder -Filter "*.aea"

# Check if any AEA files are found
if ($wavFiles.Count -eq 0) {
    Write-Host "No AEA files found in the current directory: $sourceFolder" -ForegroundColor Yellow
    return
}

New-Item -ItemType Directory -Path "AEAs"

# Process each WAV file
foreach ($file in $wavFiles) {
    $inputFile = $file.FullName
    $outputFile = Join-Path $outputFolder ($file.BaseName + ".wav")

    Write-Host "Converting $inputFile to $outputFile..."

    # Run FFmpeg command
    $ffmpegCommand = "$ffmpegPath -i `"$inputFile`" `"$outputFile`""
    #$ffmpegCommand = "$ffmpegPath -i `"$inputFile`" -vn -ar 44100 -ac 2 -b:a 192k `"$outputFile`""
    Start-Process -NoNewWindow -Wait -FilePath "cmd.exe" -ArgumentList "/c $ffmpegCommand"

    Write-Host "Conversion complete: $outputFile"

    #Remove-Item $inputFile
    Move-Item -path $inputfile -destination ("AEAs/"+ $file.BaseName + ".aea")
}

Write-Host "All AEA files in the current directory have been converted to WAV."
