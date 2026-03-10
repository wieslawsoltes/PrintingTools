---
title: "Lunet Docs Pipeline"
---

# Lunet Docs Pipeline

PrintingTools uses Lunet for the documentation site and generated API reference.

## Site structure

- `site/config.scriban`: site metadata, bundle setup, and `api.dotnet` configuration
- `site/menu.yml`: top-level navigation
- `site/articles/**`: narrative documentation
- `site/articles/**/menu.yml`: section sidebars
- `site/images/**`: project branding assets
- `site/.lunet/css/template-main.css`: precompiled template stylesheet
- `site/.lunet/css/site-overrides.css`: PrintingTools-specific styling

## Local commands

```bash
./build-docs.sh
./check-docs.sh
./serve-docs.sh
```

PowerShell equivalents are available as `build-docs.ps1`, `check-docs.ps1`, and `serve-docs.ps1`.

## API generation

The generated API reference is built from:

- `src/PrintingTools/PrintingTools.csproj`
- `src/PrintingTools.Core/PrintingTools.Core.csproj`
- `src/PrintingTools.UI/PrintingTools.UI.csproj`
- `src/PrintingTools.Windows/PrintingTools.Windows.csproj`
- `src/PrintingTools.MacOS/PrintingTools.MacOS.csproj`
- `src/PrintingTools.Linux/PrintingTools.Linux.csproj`

## CI and deployment

- `ci.yml` validates that the site builds successfully.
- `docs.yml` publishes `site/.lunet/build/www` to GitHub Pages from the primary branch.

## Output path

All generated site files are written to `site/.lunet/build/www`.
