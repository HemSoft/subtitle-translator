---
title: "AGENTS.md"
version: "1.0.0"
lastModified: "2025-11-26"
author: "Franz Hemmer"
purpose: "Instructions for AI, LLM's, tools and agents."
---

# AGENTS.md - Instructions for AI LLM's, Agents and Tools

## Project Overview

**Subtitle Translator** - A .NET 10 CLI tool that translates subtitle files using Claude AI.

- **Stack**: .NET 10, Spectre.Console, Claude CLI
- **Supported formats**: `.srt`, `.vtt`

## Critical Constraints

- **Configuration Files**: Do not modify rules in `.editorconfig`, `SonarLint.xml`, or `stylecop.json` to resolve warnings. Fix the underlying code.
- **StyleCop Exemptions**: Do not add exemptions for StyleCop rules in `.editorconfig`. Fix the code to comply.
- **Versioning**: Increment version and update `lastModified` when modifying files with version metadata.

## Build Policy

- Build treats **all warnings as errors**.
- Run `dotnet build` after changes.
- Run `dotnet format` before committing.
- A build with warnings is NOT successful.

## Code Standards

- File-scoped namespaces.
- Using statements inside namespace.
- Use `var` when type is apparent.
- Prefer primary constructors.
- No trailing comments.
- Group private methods at bottom with `#region`.
