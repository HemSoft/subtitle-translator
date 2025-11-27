# Subtitle Translator Runner
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$projectPath = Join-Path $PSScriptRoot "src\SubtitleTranslator"
dotnet run --project $projectPath -- @Arguments
