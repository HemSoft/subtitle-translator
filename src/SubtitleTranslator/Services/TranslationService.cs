// <copyright file="TranslationService.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Services;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using SubtitleTranslator.Models;

/// <summary>
/// Handles translation of subtitle chunks using Claude CLI.
/// </summary>
internal static partial class TranslationService
{
    private const string DefaultInstructions = """
        You are a professional subtitle translator. Translate the subtitles accurately while:
        - Preserving the natural flow and timing of dialogue
        - Maintaining the original tone and style (formal, casual, humorous, etc.)
        - Keeping cultural references accessible when possible
        - Preserving any formatting tags (like <i> for italics)
        - Not translating proper nouns unless they have established translations
        """;

    /// <summary>
    /// Translates a chunk of subtitles using Claude CLI.
    /// </summary>
    /// <param name="chunk">The subtitle entries to translate.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <param name="customInstructions">Optional custom instructions for translation.</param>
    /// <returns>The translated subtitle entries.</returns>
    public static async Task<List<SubtitleEntry>> TranslateChunkAsync(
        List<SubtitleEntry> chunk,
        string sourceLanguage,
        string targetLanguage,
        string? customInstructions)
    {
        var prompt = BuildPrompt(chunk, sourceLanguage, targetLanguage, customInstructions);
        var response = await InvokeClaudeAsync(prompt);
        return ParseTranslationResponse(chunk, response);
    }

    [GeneratedRegex(@"\[(\d+)\]\s*(.*?)(?=\[\d+\]|$)", RegexOptions.Singleline)]
    private static partial Regex TranslationResponseRegex();

    private static string BuildPrompt(
        List<SubtitleEntry> chunk,
        string sourceLanguage,
        string targetLanguage,
        string? customInstructions)
    {
        var sb = new StringBuilder();

        sb.AppendLine(customInstructions ?? DefaultInstructions);
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Translate the following subtitles from {sourceLanguage} to {targetLanguage}.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: Return ONLY the translations in the exact same format:");
        sb.AppendLine("[index] translated text");
        sb.AppendLine();
        sb.AppendLine("Subtitles to translate:");
        sb.AppendLine();

        foreach (var entry in chunk)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{entry.Index}] {entry.Text}");
        }

        return sb.ToString();
    }

    private static async Task<string> InvokeClaudeAsync(string prompt)
    {
        var escapedPrompt = JsonSerializer.Serialize(prompt);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = $"-p {escapedPrompt} --output-format text",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Claude CLI failed: {error}");
        }

        return output;
    }

    private static List<SubtitleEntry> ParseTranslationResponse(List<SubtitleEntry> originalChunk, string response)
    {
        var translations = TranslationResponseRegex()
            .Matches(response)
            .Where(m => int.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out _))
            .ToDictionary(
                m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                m => m.Groups[2].Value.Trim());

        return originalChunk.Select(entry => entry with
        {
            Text = translations.TryGetValue(entry.Index, out var translatedText)
                ? translatedText
                : entry.Text,
        }).ToList();
    }
}
