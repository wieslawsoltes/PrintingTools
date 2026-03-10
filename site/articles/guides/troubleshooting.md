---
title: "Troubleshooting"
---

# Troubleshooting

## First checks

1. Confirm the correct platform package is referenced.
2. Verify `PrintServiceRegistry` was configured before the first print request.
3. Enable diagnostics through `PrintingToolsOptions.DiagnosticSink`.
4. Reproduce the issue with the matching sample or harness when possible.

## Common symptoms

| Symptom | Likely cause | Next step |
| --- | --- | --- |
| `NotSupportedException` before preview or print | No adapter resolved for the current OS | Check package references and `AdapterFactory` wiring. |
| Preview works but print output is blank | Driver or backend issue with vector payloads | Retry with `UseVectorRenderer = false`. |
| Native dialog never opens | Headless environment or invalid window handle | Disable dialogs in CI, or verify the app owns a real top-level window. |
| No printers are returned | Platform permissions, spooler issues, or missing CLI tools | Check `lp`, AppKit printer visibility, or Windows queue permissions. |
| Golden or harness tests fail in CI | Platform, locale, or metrics drift | Re-run the harness locally and inspect the metrics JSON and logs. |

## Diagnostic sources

- `PrintDiagnostics`
- harness log files under `artifacts/<platform>/`
- GitHub Actions artifacts from `ci.yml`

## Platform-specific references

- [Windows](../platforms/windows.md)
- [macOS](../platforms/macos.md)
- [Linux](../platforms/linux.md)
