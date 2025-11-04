# Linux Sandbox & Container Harness Notes

## Objectives
- Validate CUPS printing inside Flatpak, Snap, and containerized environments.
- Document required portals, permissions, and environment configuration for Avalonia apps using `PrintingTools.Linux`.
- Capture failure diagnostics (D-Bus permission errors, AppArmor denials) and feed them back into the parity plan.

## Harness Strategy
- Console harness located at `samples/LinuxSandboxHarness/` emits diagnostics, enumerates printers, and optionally triggers a print job using the Linux adapter. The visual payload is rendered via a lightweight Avalonia control so CUPS receives PDF/vector output.
- Platform manifests live alongside the harness (`Flatpak/`, `Snap/`, `Container/`). The build scripts reuse `dotnet publish` to stage the harness binary before packaging.
- Harness logging pipes `PrintDiagnostics` to STDOUT so container/sandbox logs capture capability queries and CUPS/portal responses.
- Reuse `ManagedPrintDialogFallback` whenever GTK is absent; this ensures Flatpak portal prompts appear even if native dialogs fail.

## Portal & Permission Requirements
- **Flatpak**: `--talk-name=org.freedesktop.portal.Desktop`, `--talk-name=org.freedesktop.portal.Print`, share `ipc`, `network`, optionally `cups` socket via `--filesystem=xdg-run/cups`.
- **Snap**: Connect the `cups-control` interface for direct CUPS access; consider `cups` snap slot for restricted environments.
- **AppArmor**: Ensure AppArmor profiles permit `/run/cups/cups.sock` access; log denials via `journalctl -t kernel`.

## Diagnostics Checklist
- Record output from `journalctl --user -xe` and `journalctl -t kernel` for permission denials.
- Capture portal responses by setting `GTK_USE_PORTAL=1` and exporting `GIO_USE_PORTALS=1`.
- Use `CUPS_DEBUG_FILE=/tmp/cups-debug.log` to collect detailed IPP traces.

## Follow-up Items
- Automate Flatpak packaging using `flatpak-builder` and store manifests under `samples/LinuxSandboxHarness/Flatpak/` (sample manifest provided).
- Provide Snap packaging scripts in `samples/LinuxSandboxHarness/Snap/` and document `snap connect` commands (sample `snapcraft.yaml` included).
- Draft CI job definitions for container-based validation (Docker/Podman) using rootless printing setups.
- Update `docs/feature-parity-matrix.md` with sandbox-specific caveats once harness runs confirm behavior.
