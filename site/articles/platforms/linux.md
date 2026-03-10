---
title: "Linux"
---

# Linux

`PrintingTools.Linux` targets CUPS-backed printing environments and uses GTK or managed dialog fallbacks depending on what is available at runtime.

## Supported environment

- CUPS 2.3 or newer
- Ubuntu, Fedora, Arch, and other distributions with `lp` and `lpoptions`
- GTK-backed desktops or headless/service environments

## Feature snapshot

| Capability | Status |
| --- | --- |
| Printer discovery via CUPS utilities | Supported |
| Capability discovery via `lpoptions` | Supported |
| GTK dialog integration | Supported when available |
| Managed dialog fallback | Supported |
| Portal-aware execution | Supported via environment variables |
| Managed PDF export plus `lp` submission | Supported |

## Runtime notes

- Export `GTK_USE_PORTAL=1` and `GIO_USE_PORTALS=1` for Flatpak and Snap-style environments.
- When no display server is available, the adapter stays functional for non-interactive export workflows.
- CUPS stderr is surfaced through diagnostics and should be preserved in logs.

## Related

- [Samples and Harnesses](../guides/samples-and-harnesses.md)
- [Support Matrix](support-matrix.md)
