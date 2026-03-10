---
title: "Installation"
---

# Installation

## Prerequisites

| Requirement | Notes |
| --- | --- |
| .NET SDK | Use the .NET 10 SDK version pinned in `global.json`. |
| Avalonia | The packages target Avalonia 11.x. |
| Windows | Win32 print spooler and XPS-capable system components. |
| macOS | Xcode command line tools for building the native preview bridge when building from source. |
| Linux | `cups-client` plus GTK libraries when native dialog integration is desired. |

## Package install examples

Typical Avalonia application:

```bash
dotnet add package PrintingTools
dotnet add package PrintingTools.UI
```

Direct platform integration without the packaged UI:

```bash
dotnet add package PrintingTools.Core
dotnet add package PrintingTools.Windows
```

Swap `PrintingTools.Windows` for `PrintingTools.MacOS` or `PrintingTools.Linux` as needed.

## Build from source

```bash
git clone https://github.com/wieslawsoltes/PrintingTools.git
cd PrintingTools
dotnet restore PrintingTool.sln
dotnet build PrintingTool.sln -c Release
```

## Native platform notes

- `PrintingTools.MacOS` builds `PrintingToolsMacBridge.dylib` on macOS and packs it under `runtimes/osx/native`.
- Linux headless runs rely on `lp` and `lpoptions` being available on the PATH.
- Windows packaging and runtime behavior assume desktop-style Win32 printing, not UWP or WinUI print contracts.

## Documentation toolchain

This repository uses Lunet for the documentation site:

```bash
dotnet tool restore
./build-docs.sh
./serve-docs.sh
```

Generated output is written to `site/.lunet/build/www`.

## Read next

- [Quickstart Avalonia](quickstart-avalonia.md)
- [Quickstart Headless](quickstart-headless.md)
- [Platform Support Matrix](../platforms/support-matrix.md)
