# Subtitle Translator

A command-line tool that translates subtitle files from one language to another using Claude AI.

## Features

- Translates `.srt` and `.vtt` subtitle formats
- Automatic source language detection
- Preserves subtitle timing and formatting
- Processes subtitles in configurable chunks for optimal translation quality
- Interactive mode with guided prompts
- Command-line mode for scripting and automation

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Claude CLI](https://docs.anthropic.com/en/docs/claude-cli) installed and authenticated

## Installation

```bash
git clone https://github.com/HemSoft/subtitle-translator.git
cd subtitle-translator
dotnet build
```

## Usage

### Command-line Mode

```bash
# Basic usage (auto-detect source, translate to English)
subtrans translate movie.srt

# Specify target language
subtrans translate movie.srt -t es

# Specify both source and target languages
subtrans translate movie.srt -s en -t fr

# Custom chunk size and output file
subtrans translate movie.srt -t de -c 100 -o movie_german.srt

# With custom translation instructions
subtrans translate movie.srt -t es --instructions "Use formal Spanish (usted)"
```

### Interactive Mode

```bash
subtrans interactive
# or
subtrans i
```

### Options

| Option | Description |
|--------|-------------|
| `-s, --source` | Source language ISO code (auto-detect if not specified) |
| `-t, --target` | Target language ISO code (default: en) |
| `-o, --output` | Output file path (default: input file with language code appended) |
| `-c, --chunk-size` | Subtitle entries per translation chunk (default: 50) |
| `--instructions` | Custom translation instructions for Claude |

### Supported Languages

Uses ISO 639-1 language codes: `en`, `es`, `fr`, `de`, `it`, `pt`, `ru`, `ja`, `ko`, `zh`, `ar`, `hi`, `nl`, `pl`, `sv`, `da`, `no`, `fi`, `tr`, `el`, `he`, `th`, `vi`, `id`, `ms`, `cs`, `sk`, `hu`, `ro`, `uk`, `bg`, `hr`, `sr`

## License

MIT
