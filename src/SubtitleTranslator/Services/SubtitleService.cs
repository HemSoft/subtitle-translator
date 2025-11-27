// <copyright file="SubtitleService.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Services;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using SubtitleTranslator.Models;

/// <summary>
/// Handles parsing and writing subtitle files.
/// </summary>
internal static partial class SubtitleService
{
    /// <summary>
    /// Parses a subtitle file and returns a list of subtitle entries.
    /// </summary>
    /// <param name="filePath">Path to the subtitle file.</param>
    /// <returns>List of parsed subtitle entries.</returns>
    public static async Task<List<SubtitleEntry>> ParseAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var extension = Path.GetExtension(filePath).ToUpperInvariant();

        return extension switch
        {
            ".SRT" => ParseSrt(content),
            ".VTT" => ParseVtt(content),
            _ => throw new NotSupportedException($"Unsupported subtitle format: {extension}"),
        };
    }

    /// <summary>
    /// Splits subtitles into chunks of the specified size.
    /// </summary>
    /// <param name="subtitles">The subtitles to chunk.</param>
    /// <param name="chunkSize">Number of entries per chunk.</param>
    /// <returns>List of subtitle chunks.</returns>
    public static List<List<SubtitleEntry>> ChunkSubtitles(List<SubtitleEntry> subtitles, int chunkSize) =>
        subtitles
            .Chunk(chunkSize)
            .Select(chunk => chunk.ToList())
            .ToList();

    /// <summary>
    /// Generates an output file path with the target language code appended.
    /// </summary>
    /// <param name="inputPath">The original input file path.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <returns>The generated output path.</returns>
    public static string GenerateOutputPath(string inputPath, string targetLanguage)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? ".";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);

        return Path.Combine(directory, $"{nameWithoutExt}.{targetLanguage}{extension}");
    }

    /// <summary>
    /// Writes subtitle entries to a file.
    /// </summary>
    /// <param name="filePath">The output file path.</param>
    /// <param name="subtitles">The subtitles to write.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task WriteAsync(string filePath, List<SubtitleEntry> subtitles)
    {
        var extension = Path.GetExtension(filePath).ToUpperInvariant();
        var content = extension switch
        {
            ".SRT" => GenerateSrt(subtitles),
            ".VTT" => GenerateVtt(subtitles),
            _ => throw new NotSupportedException($"Unsupported subtitle format: {extension}"),
        };

        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
    }

    [GeneratedRegex(@"(\d+)\r?\n(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})\r?\n([\s\S]*?)(?=\r?\n\r?\n|\r?\n?$)", RegexOptions.Compiled)]
    private static partial Regex SrtEntryRegex();

    private static List<SubtitleEntry> ParseSrt(string content)
    {
        var matches = SrtEntryRegex().Matches(content);

        return matches.Select(match => new SubtitleEntry(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            ParseSrtTime(match.Groups[2].Value),
            ParseSrtTime(match.Groups[3].Value),
            match.Groups[4].Value.Trim())).ToList();
    }

    private static List<SubtitleEntry> ParseVtt(string content)
    {
        var entries = new List<SubtitleEntry>();
        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var index = 0;
        var lineIndex = 0;

        while (lineIndex < lines.Length)
        {
            if (lines[lineIndex].StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase) ||
                lines[lineIndex].StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(lines[lineIndex]))
            {
                lineIndex++;
                continue;
            }

            if (!lines[lineIndex].Contains("-->", StringComparison.Ordinal))
            {
                lineIndex++;
                continue;
            }

            var timeParts = lines[lineIndex].Split("-->");
            var startTime = ParseVttTime(timeParts[0].Trim());
            var endTime = ParseVttTime(timeParts[1].Trim().Split(' ')[0]);

            var textBuilder = new StringBuilder();
            lineIndex++;
            while (lineIndex < lines.Length && !string.IsNullOrWhiteSpace(lines[lineIndex]))
            {
                if (textBuilder.Length > 0)
                {
                    textBuilder.AppendLine();
                }

                textBuilder.Append(lines[lineIndex]);
                lineIndex++;
            }

            index++;
            entries.Add(new SubtitleEntry(index, startTime, endTime, textBuilder.ToString()));
        }

        return entries;
    }

    private static TimeSpan ParseSrtTime(string time)
    {
        var parts = time.Replace(',', '.').Split(':');
        var hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var secondsParts = parts[2].Split('.');
        var seconds = int.Parse(secondsParts[0], CultureInfo.InvariantCulture);
        var milliseconds = int.Parse(secondsParts[1], CultureInfo.InvariantCulture);

        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }

    private static TimeSpan ParseVttTime(string time)
    {
        var parts = time.Split(':');
        int hours;
        int minutes;
        string secondsPart;

        if (parts.Length == 3)
        {
            hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
            minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
            secondsPart = parts[2];
        }
        else
        {
            hours = 0;
            minutes = int.Parse(parts[0], CultureInfo.InvariantCulture);
            secondsPart = parts[1];
        }

        var secondsParts = secondsPart.Split('.');
        var seconds = int.Parse(secondsParts[0], CultureInfo.InvariantCulture);
        var milliseconds = secondsParts.Length > 1 ? int.Parse(secondsParts[1].PadRight(3, '0')[..3], CultureInfo.InvariantCulture) : 0;

        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }

    private static string GenerateSrt(List<SubtitleEntry> subtitles)
    {
        var sb = new StringBuilder();

        foreach (var entry in subtitles)
        {
            sb.AppendLine(entry.Index.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(CultureInfo.InvariantCulture, $"{FormatSrtTime(entry.StartTime)} --> {FormatSrtTime(entry.EndTime)}");
            sb.AppendLine(entry.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateVtt(List<SubtitleEntry> subtitles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();

        foreach (var entry in subtitles)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{FormatVttTime(entry.StartTime)} --> {FormatVttTime(entry.EndTime)}");
            sb.AppendLine(entry.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatSrtTime(TimeSpan time) =>
        $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";

    private static string FormatVttTime(TimeSpan time) =>
        $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds:D3}";
}
