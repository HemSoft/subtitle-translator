// <copyright file="Program.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

using Spectre.Console.Cli;

using SubtitleTranslator.Commands;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("subtrans");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<TranslateCommand>("translate")
        .WithDescription("Translate a subtitle file from one language to another")
        .WithExample("translate", "movie.srt", "-t", "es")
        .WithExample("translate", "movie.srt", "-s", "en", "-t", "fr");

    config.AddCommand<InteractiveCommand>("interactive")
        .WithDescription("Launch interactive mode with prompts")
        .WithAlias("i");
});

return await app.RunAsync(args);
