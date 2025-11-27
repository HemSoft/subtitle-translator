// <copyright file="TranslateCommand.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Commands;

using System.ComponentModel;

using Spectre.Console;
using Spectre.Console.Cli;

using SubtitleTranslator.Models;
using SubtitleTranslator.Services;

/// <summary>
/// Command to translate subtitle files from one language to another.
/// </summary>
internal sealed class TranslateCommand : AsyncCommand<TranslateCommand.Settings>
{
    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.InputFile))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {settings.InputFile}");
            return 1;
        }

        var subtitles = await ParseSubtitlesAsync(settings.InputFile);
        AnsiConsole.MarkupLine($"[green]✓[/] Parsed [blue]{subtitles.Count}[/] subtitle entries");

        var sourceLanguage = await DetermineSourceLanguageAsync(settings.SourceLanguage, subtitles);
        var targetLanguage = DetermineTargetLanguage(settings.TargetLanguage, sourceLanguage);

        AnsiConsole.MarkupLine($"[green]✓[/] Translating from [blue]{sourceLanguage}[/] to [blue]{targetLanguage}[/]");

        var outputFile = settings.OutputFile ?? SubtitleService.GenerateOutputPath(settings.InputFile, targetLanguage);
        var translatedSubtitles = await TranslateSubtitlesAsync(subtitles, sourceLanguage, targetLanguage, settings);

        await SubtitleService.WriteAsync(outputFile, translatedSubtitles);
        AnsiConsole.MarkupLine($"[green]✓[/] Saved translated subtitles to: [blue]{outputFile}[/]");

        return 0;
    }

    private static async Task<List<SubtitleEntry>> ParseSubtitlesAsync(string inputFile) =>
        await AnsiConsole.Status()
            .StartAsync("Parsing subtitle file...", async _ => await SubtitleService.ParseAsync(inputFile));

    private static async Task<string> DetermineSourceLanguageAsync(string? sourceLanguage, List<SubtitleEntry> subtitles)
    {
        if (!string.IsNullOrEmpty(sourceLanguage))
        {
            return sourceLanguage;
        }

        var detected = await AnsiConsole.Status()
            .StartAsync("Detecting source language...", async _ => await LanguageService.DetectLanguageAsync(subtitles));

        AnsiConsole.MarkupLine($"[green]✓[/] Detected source language: [blue]{detected}[/]");
        return detected;
    }

    private static string DetermineTargetLanguage(string? targetLanguage, string sourceLanguage)
    {
        var target = targetLanguage ?? "en";

        if (target == sourceLanguage)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Detected source language ({sourceLanguage}) matches target. Using source language for translation context.");
        }

        return target;
    }

    private static async Task<List<SubtitleEntry>> TranslateSubtitlesAsync(
        List<SubtitleEntry> subtitles,
        string sourceLanguage,
        string targetLanguage,
        Settings settings)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Translating {subtitles.Count} entries...", async _ =>
                await TranslationService.TranslateAsync(
                    subtitles,
                    sourceLanguage,
                    targetLanguage,
                    settings.CustomInstructions));
    }

    /// <summary>
    /// Settings for the translate command.
    /// </summary>
    internal sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets the path to the subtitle file to translate.
        /// </summary>
        [Description("Path to the subtitle file to translate")]
        [CommandArgument(0, "<FILE>")]
        public required string InputFile { get; init; }

        /// <summary>
        /// Gets the source language ISO code.
        /// </summary>
        [Description("Source language ISO code (auto-detect if not specified)")]
        [CommandOption("-s|--source")]
        public string? SourceLanguage { get; init; }

        /// <summary>
        /// Gets the target language ISO code.
        /// </summary>
        [Description("Target language ISO code (default: en)")]
        [CommandOption("-t|--target")]
        public string? TargetLanguage { get; init; }

        /// <summary>
        /// Gets the output file path.
        /// </summary>
        [Description("Output file path (default: input file with target language code appended)")]
        [CommandOption("-o|--output")]
        public string? OutputFile { get; init; }

        /// <summary>
        /// Gets custom translation instructions for Claude.
        /// </summary>
        [Description("Custom translation instructions for Claude")]
        [CommandOption("--instructions")]
        public string? CustomInstructions { get; init; }
    }
}
