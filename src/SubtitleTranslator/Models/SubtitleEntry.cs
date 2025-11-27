// <copyright file="SubtitleEntry.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace SubtitleTranslator.Models;

/// <summary>
/// Represents a single subtitle entry with timing and text.
/// </summary>
/// <param name="Index">The sequence number of the subtitle.</param>
/// <param name="StartTime">When the subtitle should appear.</param>
/// <param name="EndTime">When the subtitle should disappear.</param>
/// <param name="Text">The subtitle text content.</param>
internal sealed record SubtitleEntry(
    int Index,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Text);
