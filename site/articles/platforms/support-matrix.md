---
title: "Support Matrix"
---

# Support Matrix

| Platform / Environment | UI Dialogs | Print Backend | Managed PDF Export | Harness Coverage | Notes |
| --- | --- | --- | --- | --- | --- |
| Windows desktop | Native Win32 dialog | Win32 spooler | Yes | `Harness (Windows)` | Best current parity story for desktop deployments. |
| macOS 14 | AppKit print panel | `NSPrintOperation` | Yes | `Harness (macOS)` | Headless CI path skips interactive UI. |
| Ubuntu 22.04 | GTK or managed fallback | CUPS | Yes | `Harness (Linux)` | Portal-aware environment variables used in CI. |
| Fedora 39 | Partial validation | CUPS | Yes | Planned | Additional portal and SELinux validation needed. |
| Flatpak | Portal-backed | CUPS or portal path | Yes | Partial | Requires correct portal permissions and env vars. |
| Snap | Managed fallback or portal path | CUPS | Yes | Planned | Requires interface wiring such as `cups-control`. |
| Headless container | No UI | Mounted or reachable CUPS | Yes | Partial | Best suited for PDF generation and regression checks. |

## Maintenance note

Update this matrix whenever a new platform or packaging environment is validated. The matching harness job or manual log should remain the evidence source.
