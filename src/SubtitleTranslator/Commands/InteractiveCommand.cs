// <copyright file="InteractiveCommand.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Commands;

using Spectre.Console;
using Spectre.Console.Cli;

using SubtitleTranslator.Models;
using SubtitleTranslator.Services;

/// <summary>
/// Interactive command that prompts for all translation options.
/// </summary>
internal sealed class InteractiveCommand : AsyncCommand
{
    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        DisplayHeader();

        var inputFile = await PromptForInputFileAsync(cancellationToken);
        var subtitles = await ParseSubtitlesAsync(inputFile);

        AnsiConsole.MarkupLine($"[green]✓[/] Parsed [blue]{subtitles.Count}[/] subtitle entries");

        var sourceLanguage = await GetSourceLanguageAsync(subtitles, cancellationToken);
        var targetLanguage = await GetTargetLanguageAsync(sourceLanguage, cancellationToken);
        var chunkSize = await PromptForChunkSizeAsync(cancellationToken);
        var customInstructions = await PromptForCustomInstructionsAsync(cancellationToken);
        var outputFile = await PromptForOutputFileAsync(inputFile, targetLanguage, cancellationToken);

        DisplaySummary(inputFile, outputFile, sourceLanguage, targetLanguage, chunkSize, customInstructions);

        var confirmed = await AnsiConsole.PromptAsync(
            new ConfirmationPrompt("Proceed with translation?"),
            cancellationToken);

        if (!confirmed)
        {
            AnsiConsole.MarkupLine("[yellow]Translation cancelled.[/]");
            return 0;
        }

        var translatedSubtitles = await TranslateSubtitlesAsync(
            subtitles,
            sourceLanguage,
            targetLanguage,
            chunkSize,
            customInstructions);

        await SubtitleService.WriteAsync(outputFile, translatedSubtitles);
        AnsiConsole.MarkupLine($"\n[green]✓[/] Translation complete! Saved to: [blue]{outputFile}[/]");

        return 0;
    }

    private static void DisplayHeader()
    {
        AnsiConsole.Write(
            new FigletText("SubTrans")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[grey]Subtitle Translator - Interactive Mode[/]\n");
    }

    private static async Task<string> PromptForInputFileAsync(CancellationToken cancellationToken) =>
        await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Enter the path to the subtitle file:")
                .Validate(path =>
                    File.Exists(path)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]File not found[/]")),
            cancellationToken);

    private static async Task<List<SubtitleEntry>> ParseSubtitlesAsync(string inputFile) =>
        await AnsiConsole.Status()
            .StartAsync("Parsing subtitle file...", async _ => await SubtitleService.ParseAsync(inputFile));

    private static async Task<string> GetSourceLanguageAsync(List<SubtitleEntry> subtitles, CancellationToken cancellationToken)
    {
        var detectedLanguage = await AnsiConsole.Status()
            .StartAsync("Detecting source language...", async _ => await LanguageService.DetectLanguageAsync(subtitles));

        var sourceLanguage = await AnsiConsole.PromptAsync(
            new TextPrompt<string>($"Source language [grey](detected: {detectedLanguage})[/]:")
                .DefaultValue(detectedLanguage)
                .AllowEmpty(),
            cancellationToken);

        return string.IsNullOrWhiteSpace(sourceLanguage) ? detectedLanguage : sourceLanguage;
    }

    private static async Task<string> GetTargetLanguageAsync(string sourceLanguage, CancellationToken cancellationToken)
    {
        var commonLanguages = LanguageService.GetCommonLanguages()
            .Where(l => l != sourceLanguage)
            .ToList();

        var targetLanguage = await AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("Select target language:")
                .PageSize(10)
                .AddChoices(commonLanguages)
                .AddChoices("[Other]"),
            cancellationToken);

        if (targetLanguage == "[Other]")
        {
            targetLanguage = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Enter target language ISO code:"),
                cancellationToken);
        }

        return targetLanguage;
    }

    private static async Task<int> PromptForChunkSizeAsync(CancellationToken cancellationToken) =>
        await AnsiConsole.PromptAsync(
            new TextPrompt<int>("Chunk size (subtitle entries per translation request):")
                .DefaultValue(50)
                .Validate(n => n > 0 ? ValidationResult.Success() : ValidationResult.Error("Must be positive")),
            cancellationToken);

    private static async Task<string?> PromptForCustomInstructionsAsync(CancellationToken cancellationToken)
    {
        var addInstructions = await AnsiConsole.PromptAsync(
            new ConfirmationPrompt("Add custom translation instructions?") { DefaultValue = false },
            cancellationToken);

        if (!addInstructions)
        {
            return null;
        }

        return await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Enter custom instructions:")
                .AllowEmpty(),
            cancellationToken);
    }

    private static async Task<string> PromptForOutputFileAsync(string inputFile, string targetLanguage, CancellationToken cancellationToken)
    {
        var defaultOutputPath = SubtitleService.GenerateOutputPath(inputFile, targetLanguage);

        return await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Output file path:")
                .DefaultValue(defaultOutputPath),
            cancellationToken);
    }

    private static void DisplaySummary(
        string inputFile,
        string outputFile,
        string sourceLanguage,
        string targetLanguage,
        int chunkSize,
        string? customInstructions)
    {
        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Input", inputFile);
        table.AddRow("Output", outputFile);
        table.AddRow("Source Language", sourceLanguage);
        table.AddRow("Target Language", targetLanguage);
        table.AddRow("Chunk Size", chunkSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        table.AddRow("Custom Instructions", customInstructions ?? "[grey]None[/]");

        AnsiConsole.Write(table);
    }

    private static async Task<List<SubtitleEntry>> TranslateSubtitlesAsync(
        List<SubtitleEntry> subtitles,
        string sourceLanguage,
        string targetLanguage,
        int chunkSize,
        string? customInstructions)
    {
        var chunks = SubtitleService.ChunkSubtitles(subtitles, chunkSize);
        var translatedSubtitles = new List<SubtitleEntry>();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]Translating {chunks.Count} chunks[/]", maxValue: chunks.Count);

                foreach (var chunk in chunks)
                {
                    var translated = await TranslationService.TranslateChunkAsync(
                        chunk,
                        sourceLanguage,
                        targetLanguage,
                        customInstructions);

                    translatedSubtitles.AddRange(translated);
                    task.Increment(1);
                }
            });

        return translatedSubtitles;
    }
}
