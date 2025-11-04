# Linux Printing Adapter Adoption Notes

The Linux adapter wraps CUPS/IPP to deliver parity features across GNOME, KDE, and headless environments. This guide explains prerequisites, configuration steps, and diagnostics.

## 1. Supported Targets
- **Distributions:** Ubuntu 22.04+, Fedora 39+, Arch (rolling). Any distro with CUPS 2.3+ should work.
- **Framework:** .NET 8+ with Avalonia 11.
- **Toolkits:** GTK 3/4 is auto-detected for native dialogs; the adapter falls back to a managed dialog when GTK is unavailable. Qt support is on the roadmap.

## 2. Installation & Wiring
```xml
<PackageReference Include="PrintingTools.Linux" Version="1.0.0-preview" />
```

```csharp
var options = new PrintingToolsOptions
{
    AdapterFactory = () => new LinuxPrintAdapter(),
    DiagnosticSink = evt => Console.WriteLine($"[Linux] {evt.Category}: {evt.Message}")
};
PrintServiceRegistry.Configure(options);
```

When running inside Flatpak or Snap, export `GTK_USE_PORTAL=1` and `GIO_USE_PORTALS=1` so dialogs travel through desktop portals.

## 3. Feature Snapshot

| Capability | Status |
| --- | --- |
| Printer discovery (`lpstat -p`) | ✅ |
| Capability discovery (`lpoptions -p -l`) | ✅ |
| Native dialog (GTK) | ✅ (auto-detected) |
| Managed dialog fallback | ✅ |
| PDF submission (`lp`) | ✅ (managed PDF export + CUPS) |
| N-up / booklet / poster | ✅ (metadata translated to `-o` arguments) |
| Job diagnostics (`lp` exit codes + `PrintDiagnostics`) | ✅ |
| Portal support (`org.freedesktop.portal.Print`) | ✅ (via environment variables) |

## 4. Runtime Behaviour
- The adapter shells out to `lp`/`lpoptions` through `CupsCommandClient`. Ensure these utilities exist on the PATH (they ship with the `cups-client` package).
- Capabilities and ticket defaults are merged into `PrintTicketModel.Extensions` (`cups.media`, `cups.print-color-mode`, `cups.sides`). Read them back to configure custom UI.
- N-up/booklet/poster layouts rely on `LayoutMetadata`. The adapter converts metadata into `-o number-up`, `-o sides=two-sided-long-edge`, and poster tiling arguments before calling `lp`.
- When GTK is missing or the session is headless (no `$DISPLAY`/`$WAYLAND_DISPLAY`), the managed Avalonia dialog prompts the user with core settings instead of proceeding silently.

## 5. Deployment Considerations
- **Sandboxing:** Flatpak/Snap users must enable the print portal. The harness sets `GTK_USE_PORTAL=1` by default; do the same in production builds released through these stores.
- **Permissions:** CUPS often restricts remote queue enumeration; ensure service accounts belong to the `lp` group when running headless services.
- **Ghostscript:** Driverless printers accept PDF. If you target legacy queues requiring PWG raster, bundle Ghostscript or cups-filters and extend the adapter to convert payloads.

## 6. Troubleshooting
| Symptom | Action |
| --- | --- |
| `lp` command missing | Install the `cups-client` package; verify `which lp` succeeds. |
| Capabilities list is empty | Check that the printer is accepting jobs and that `lpoptions -p <name> -l` returns attributes. |
| Dialog fails under Flatpak | Make sure `--talk-name=org.freedesktop.portal.Desktop` is in the Flatpak manifest and environment variables above are set. |
| Jobs stuck in queue | Inspect `journalctl -u cups` or `lpstat -W not-completed` to identify driver errors. The adapter logs `lp` stderr in `PrintDiagnostics`. |

## 7. References
- Harness walkthrough: [`docs/printing-sample-walkthroughs.md`](printing-sample-walkthroughs.md)
- Platform sandboxing: [`docs/linux-sandbox-harness.md`](linux-sandbox-harness.md)
- API overview: [`docs/printing-api-reference.md`](printing-api-reference.md)
- Migration guide: [`docs/printing-migration-guide.md`](printing-migration-guide.md)
- Keep `docs/platform-support-matrix.md` updated after each sandbox/container validation; link logs from `LinuxSandboxHarness` runs.
