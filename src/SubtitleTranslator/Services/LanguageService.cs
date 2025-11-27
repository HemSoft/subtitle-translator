// <copyright file="LanguageService.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Services;

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using SubtitleTranslator.Models;

/// <summary>
/// Handles language detection and provides language utilities.
/// </summary>
internal static class LanguageService
{
    private static readonly Dictionary<string, string> LanguageNames = new()
    {
        ["en"] = "English",
        ["es"] = "Spanish",
        ["fr"] = "French",
        ["de"] = "German",
        ["it"] = "Italian",
        ["pt"] = "Portuguese",
        ["ru"] = "Russian",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
        ["zh"] = "Chinese",
        ["ar"] = "Arabic",
        ["hi"] = "Hindi",
        ["nl"] = "Dutch",
        ["pl"] = "Polish",
        ["sv"] = "Swedish",
        ["da"] = "Danish",
        ["no"] = "Norwegian",
        ["fi"] = "Finnish",
        ["tr"] = "Turkish",
        ["el"] = "Greek",
        ["he"] = "Hebrew",
        ["th"] = "Thai",
        ["vi"] = "Vietnamese",
        ["id"] = "Indonesian",
        ["ms"] = "Malay",
        ["cs"] = "Czech",
        ["sk"] = "Slovak",
        ["hu"] = "Hungarian",
        ["ro"] = "Romanian",
        ["uk"] = "Ukrainian",
        ["bg"] = "Bulgarian",
        ["hr"] = "Croatian",
        ["sr"] = "Serbian",
    };

    /// <summary>
    /// Gets the list of common language codes.
    /// </summary>
    /// <returns>List of ISO language codes.</returns>
    public static IReadOnlyList<string> GetCommonLanguages() =>
        LanguageNames.Keys.OrderBy(k => LanguageNames[k]).ToList();

    /// <summary>
    /// Gets the full language name for a given ISO code.
    /// </summary>
    /// <param name="code">The ISO language code.</param>
    /// <returns>The full language name or the code if not found.</returns>
    public static string GetLanguageName(string code) =>
        LanguageNames.TryGetValue(code.ToLowerInvariant(), out var name) ? name : code;

    /// <summary>
    /// Detects the language of the given subtitles using Claude CLI.
    /// </summary>
    /// <param name="subtitles">The subtitles to analyze.</param>
    /// <returns>The detected ISO language code.</returns>
    public static async Task<string> DetectLanguageAsync(List<SubtitleEntry> subtitles)
    {
        var sampleText = string.Join(" ", subtitles.Take(10).Select(s => s.Text));

        var prompt = string.Format(
            CultureInfo.InvariantCulture,
            "Detect the language of the following text and respond with ONLY the ISO 639-1 two-letter language code (e.g., 'en', 'es', 'fr'). Do not include any other text in your response.\n\nText: {0}",
            sampleText);

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
        await process.WaitForExitAsync();

        var detectedCode = output.Trim().ToLowerInvariant();

        return LanguageNames.ContainsKey(detectedCode)
            ? detectedCode
            : "en";
    }
}
