# meowlabel

meowlabel is used to query AI for track listings and other metadata based on transcription from OpenAI.

## Usage
- Set an API key
    `meowlabel -apikey <your OpenAI API key>`

- Merge two existing meowsplit CSVs together
    `meowlabel merge table_output.csv table1.csv table2.csv table(n).csv`

- Query OpenAI to return a CSV table based on album art transcription
    `meowlabel path/to/album_art_scan.jpg`