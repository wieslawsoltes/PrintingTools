---
title: "Windows"
---

# Windows

`PrintingTools.Windows` integrates with the Win32 print subsystem and supports managed or vector-backed output.

## Supported environment

- Windows 10 1809 or later
- x64 and ARM64 desktop workloads
- Win32 spooler access with XPS-capable rendering paths

## Feature snapshot

| Capability | Status |
| --- | --- |
| Printer enumeration | Supported |
| Capability discovery | Supported |
| Native dialog (`PrintDlgEx`) | Supported |
| Managed PDF export | Supported |
| XPS-style print submission | Supported |
| Job monitoring | Supported |

## Operational notes

- Vector output is the default and usually gives the smallest spool files.
- Some older drivers behave better with raster output; switch off `UseVectorRenderer` when needed.
- Capability downgrades are reflected back into the merged ticket and should be logged through diagnostics.

## Related

- [Troubleshooting](../guides/troubleshooting.md)
- [Support Matrix](support-matrix.md)
