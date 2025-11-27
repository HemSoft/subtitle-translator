// <copyright file="AutoCommand.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Commands;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Spectre.Console;
using Spectre.Console.Cli;

using SubtitleTranslator.Services;

/// <summary>
/// Command to automatically find and process media files without subtitles.
/// </summary>
internal sealed partial class AutoCommand : AsyncCommand<AutoCommand.Settings>
{
    private const string MediaFolder = "media";

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var mediaPath = Path.GetFullPath(settings.MediaPath ?? MediaFolder);

        if (!Directory.Exists(mediaPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Media folder not found: {mediaPath}");
            return 1;
        }

        var candidate = FindFirstCandidateAsync(mediaPath);
        if (candidate is null)
        {
            AnsiConsole.MarkupLine("[yellow]No media files found that need subtitles.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]Found:[/] {Path.GetFileNameWithoutExtension(candidate.Value.UrlFile)}");

        var url = await File.ReadAllTextAsync(candidate.Value.UrlFile, cancellationToken);
        url = url.Trim();

        var subtitleLang = await SelectSubtitleAsync(url, settings.PreferredSubtitleLang);
        if (subtitleLang is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No subtitles available for this video.");
            return 1;
        }

        var vttFile = await DownloadSubtitleAsync(url, subtitleLang, candidate.Value.BasePath);
        if (vttFile is null || !File.Exists(vttFile))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Failed to download subtitle.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Downloaded: [blue]{Path.GetFileName(vttFile)}[/]");

        return await TranslateSubtitleAsync(vttFile, settings.TargetLanguage);
    }

    private static (string UrlFile, string BasePath)? FindFirstCandidateAsync(string mediaPath)
    {
        var urlFiles = Directory.GetFiles(mediaPath, "*.url");

        foreach (var urlFile in urlFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(urlFile);
            var basePath = Path.Combine(mediaPath, baseName);

            var hasVideo = File.Exists(basePath + ".mp4");
            var hasVtt = Directory.GetFiles(mediaPath, baseName + "*.vtt").Length > 0;

            if (hasVideo && !hasVtt)
            {
                return (urlFile, basePath);
            }
        }

        return null;
    }

    private static async Task<string?> SelectSubtitleAsync(string url, string? preferredLang)
    {
        var subtitles = await ListSubtitlesAsync(url);

        if (subtitles.Count == 0)
        {
            return null;
        }

        if (subtitles.Count > 1)
        {
            AnsiConsole.MarkupLine("[blue]Available subtitles:[/]");
            foreach (var sub in subtitles)
            {
                AnsiConsole.MarkupLine($"  - {sub}");
            }
        }

        var selected = preferredLang is not null && subtitles.Contains(preferredLang)
            ? preferredLang
            : subtitles[0];

        AnsiConsole.MarkupLine($"[green]✓[/] Selected subtitle: [blue]{selected}[/]");
        return selected;
    }

    private static async Task<List<string>> ListSubtitlesAsync(string url)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "ytd" : "yt-dlp",
            Arguments = $"--list-subs \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        // yt-dlp outputs info to stderr, combine both
        return ParseSubtitleList(output + "\n" + stderr);
    }

    private static List<string> ParseSubtitleList(string output)
    {
        var subtitles = new List<string>();
        var inSubtitleSection = false;
        var skippedHeader = false;

        foreach (var line in output.Split('\n'))
        {
            if (line.Contains("Available subtitles", StringComparison.OrdinalIgnoreCase))
            {
                inSubtitleSection = true;
                continue;
            }

            if (!inSubtitleSection)
            {
                continue;
            }

            // Skip the "Language Formats" header line
            if (!skippedHeader)
            {
                skippedHeader = true;
                continue;
            }

            var match = SubtitleLineRegex().Match(line);
            if (match.Success)
            {
                subtitles.Add(match.Groups[1].Value);
            }
        }

        return subtitles;
    }

    [GeneratedRegex(@"^([a-z]{2}(?:[-_][a-z]+)?)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex SubtitleLineRegex();

    private static async Task<string?> DownloadSubtitleAsync(string url, string lang, string basePath)
    {
        var directory = Path.GetDirectoryName(basePath)!;
        var fileName = Path.GetFileName(basePath);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "ytd" : "yt-dlp",
            Arguments = $"--write-sub --sub-lang {lang} --skip-download -o \"{fileName}.%(ext)s\" \"{url}\"",
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var vttFiles = Directory.GetFiles(directory, $"{fileName}*.vtt");
        return vttFiles.Length > 0 ? vttFiles[0] : null;
    }

    private static async Task<int> TranslateSubtitleAsync(string vttFile, string targetLanguage)
    {
        var subtitles = await SubtitleService.ParseAsync(vttFile);
        AnsiConsole.MarkupLine($"[green]✓[/] Parsed [blue]{subtitles.Count}[/] subtitle entries");

        var sourceLanguage = await AnsiConsole.Status()
            .StartAsync("Detecting source language...", async _ => await LanguageService.DetectLanguageAsync(subtitles));

        AnsiConsole.MarkupLine($"[green]✓[/] Detected source language: [blue]{sourceLanguage}[/]");

        if (sourceLanguage == targetLanguage)
        {
            AnsiConsole.MarkupLine($"[yellow]Skipping:[/] Source language already matches target ({targetLanguage})");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Translating from [blue]{sourceLanguage}[/] to [blue]{targetLanguage}[/]");

        var translatedSubtitles = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Translating {subtitles.Count} entries...", async _ =>
                await TranslationService.TranslateAsync(subtitles, sourceLanguage, targetLanguage, null));

        var outputFile = SubtitleService.GenerateOutputPath(vttFile, targetLanguage);
        await SubtitleService.WriteAsync(outputFile, translatedSubtitles);
        AnsiConsole.MarkupLine($"[green]✓[/] Saved translated subtitles to: [blue]{outputFile}[/]");

        return 0;
    }

    /// <summary>
    /// Settings for the auto command.
    /// </summary>
    internal sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets the path to the media folder to scan.
        /// </summary>
        [Description("Path to media folder (default: media)")]
        [CommandOption("-m|--media")]
        public string? MediaPath { get; init; }

        /// <summary>
        /// Gets the preferred subtitle language to download.
        /// </summary>
        [Description("Preferred subtitle language code (default: first available)")]
        [CommandOption("-s|--sub-lang")]
        public string? PreferredSubtitleLang { get; init; }

        /// <summary>
        /// Gets the target language for translation.
        /// </summary>
        [Description("Target language ISO code (default: en)")]
        [CommandOption("-t|--target")]
        [DefaultValue("en")]
        public string TargetLanguage { get; init; } = "en";
    }
}
