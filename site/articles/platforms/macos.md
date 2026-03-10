---
title: "macOS"
---

# macOS

`PrintingTools.MacOS` bridges Avalonia into AppKit and Quartz through a packaged native library.

## Supported environment

- macOS 12 or later
- Intel and Apple Silicon
- desktop or sandboxed applications, provided the correct entitlements are present

## Feature snapshot

| Capability | Status |
| --- | --- |
| Printer discovery | Supported |
| Print panel | Supported |
| Page layout dialog | Supported |
| Native preview hosting | Supported |
| Quartz-backed PDF output | Supported |
| Headless CI mode | Supported |

## Sandbox notes

- Enable print entitlement support for sandboxed apps.
- Use user-selected or container-safe paths for PDF export.
- Headless CI runs should skip interactive dialogs and rely on `PdfOutputPath`.

## Native bridge

The package includes `PrintingToolsMacBridge.dylib` under `runtimes/osx/native`. When building from source on macOS, the bridge is rebuilt before the managed project compiles.

## Related

- [Page Setup and Preview UI](../guides/page-setup-and-preview-ui.md)
- [Support Matrix](support-matrix.md)
