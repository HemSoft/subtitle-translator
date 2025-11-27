// <copyright file="TranslationService.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Services;

using System.Diagnostics;
using System.Globalization;
using System.Text;
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
        - Preserving line breaks within subtitle entries (use \n for newlines)
        """;

    /// <summary>
    /// Translates all subtitles using Claude CLI in a single request.
    /// </summary>
    /// <param name="subtitles">The subtitle entries to translate.</param>
    /// <param name="sourceLanguage">The source language code.</param>
    /// <param name="targetLanguage">The target language code.</param>
    /// <param name="customInstructions">Optional custom instructions for translation.</param>
    /// <returns>The translated subtitle entries.</returns>
    public static async Task<List<SubtitleEntry>> TranslateAsync(
        List<SubtitleEntry> subtitles,
        string sourceLanguage,
        string targetLanguage,
        string? customInstructions)
    {
        var prompt = BuildPrompt(subtitles, sourceLanguage, targetLanguage, customInstructions);
        var response = await InvokeClaudeAsync(prompt);
        return ParseTranslationResponse(subtitles, response);
    }

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

    [GeneratedRegex(@"^\[(\d+)\]\s*(.+)$", RegexOptions.Multiline)]
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
        sb.AppendLine("CRITICAL FORMATTING RULES:");
        sb.AppendLine("1. Return ONLY the translations, nothing else");
        sb.AppendLine("2. Use this exact format: [index] translated text");
        sb.AppendLine("3. Keep each translation on a SINGLE line (use \\n for line breaks within subtitles)");
        sb.AppendLine("4. Include ALL entries - do not skip any");
        sb.AppendLine();
        sb.AppendLine("Subtitles to translate:");
        sb.AppendLine();

        foreach (var entry in chunk)
        {
            var escapedText = entry.Text.Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
            sb.AppendLine(CultureInfo.InvariantCulture, $"[{entry.Index}] {escapedText}");
        }

        return sb.ToString();
    }

    private static async Task<string> InvokeClaudeAsync(string prompt)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "claude",
            Arguments = OperatingSystem.IsWindows()
                ? "/c claude -p --output-format text"
                : "-p --output-format text",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();

        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

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
                m => m.Groups[2].Value.Trim().Replace("\\n", "\n", StringComparison.Ordinal));

        return originalChunk.Select(entry => entry with
        {
            Text = translations.TryGetValue(entry.Index, out var translatedText)
                ? translatedText
                : entry.Text,
        }).ToList();
    }
}
