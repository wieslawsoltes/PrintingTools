# Platform Support Matrix

Tracks which operating systems, distributions, and packaging environments have been validated for PrintingTools parity.

| Platform / Distro | UI Dialogs | CUPS / IPP Access | Managed PDF Export | Harness Coverage | Notes |
| --- | --- | --- | --- | --- | --- |
| Windows 11 (x64) | ✅ Win32 print dialog (native) | ✅ Win32 spooler | ✅ XPS/PDF via Skia | `PrintingTools Harnesses` workflow (`windows-harness` job) | CI exports managed PDF artifacts; extend to cover native spool submission and dialog automation. |
| macOS 14 (arm64) | ✅ AppKit sheet (`NSPrintPanel`) | ✅ `NSPrintOperation` | ✅ Quartz PDF | `MacSandboxHarness` (headless CI + manual notarized runs) | Sandbox entitlement matrix captured in `docs/macos-sandbox-harness.md`; CI job captures preview diagnostics in headless mode. |
| Ubuntu 22.04 (Wayland) | ✅ GTK portal fallback | ✅ CUPS socket | ✅ Managed PDF | `PrintingTools Harnesses` workflow (`linux-harness` job) | Validate Snap interface connections; capture portal logs. |
| Fedora 39 (Wayland) | ⚠️ GTK dialog (needs portal validation) | ✅ CUPS | ✅ Managed PDF | Planned | Collect SELinux/AppArmor rules when harness executes. |
| Arch Linux (X11) | ➖ Pending | ⚠️ CUPS (rootless) | ✅ Managed PDF | Planned | Target for continuous regression once Arch container image added. |
| Flatpak (org.freedesktop.Platform 23.08) | ✅ Portal dialog (`GtkPrintUnixDialog`) | ⚠️ Requires `--filesystem=xdg-run/cups` | ✅ Managed PDF | `LinuxSandboxHarness` manifest | Capture results in parity log after first run. |
| Snap (core24) | ⚠️ Managed fallback dialog | ⚠️ Needs `cups-control` plug | ✅ Managed PDF | `LinuxSandboxHarness` snapcraft | Document interface connection commands and attach CI logs once snap job executes. |
| Container (Docker/Podman) | ❌ Dialog suppressed | ⚠️ Depends on mounted `/run/cups/cups.sock` | ✅ Managed PDF | `LinuxSandboxHarness` Dockerfile | Focus on headless regression (print-to-PDF + IPP mock). |

## Usage Notes
- Update this matrix after each validation run and link supporting logs or CI job IDs.
- Treat "Harness Coverage" column as the canonical pointer to automated or manual scripts that reproduce the environment.
- Use ⚠️ when partial functionality works but additional configuration is required.
- Use ➖ when validation has not been attempted yet.
