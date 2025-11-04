# Linux Sandbox Harness

This harness exercises `PrintingTools.Linux` inside restricted environments (Flatpak, Snap, container) and captures diagnostics for CUPS/portal access.

## Running locally

```bash
dotnet run --project samples/LinuxSandboxHarness/LinuxSandboxHarness.csproj -- --print
```

Set `PRINTINGTOOLS_SANDBOX_PDF=/tmp/output.pdf` to redirect managed PDF export and collect artifacts.

## Flatpak

1. Install dependencies: `flatpak install org.freedesktop.Platform//23.08 org.freedesktop.Sdk//23.08`.
2. Build and install the bundle:
   ```bash
   flatpak-builder build-dir samples/LinuxSandboxHarness/Flatpak/com.example.PrintingTools.json --force-clean
   flatpak-builder --user --install build-dir samples/LinuxSandboxHarness/Flatpak/com.example.PrintingTools.json
   ```
3. Launch: `flatpak run com.example.PrintingToolsSandbox --print`.

## Snap

1. Build the snap:
   ```bash
   snapcraft -d --destructive-mode -f samples/LinuxSandboxHarness/Snap/snapcraft.yaml
   ```
2. Install and connect interfaces:
   ```bash
   sudo snap install printingtools-linux-sandbox_1.0_amd64.snap --dangerous
   sudo snap connect printingtools-linux-sandbox:cups-control
   ```
3. Run: `sudo snap start --enable printingtools-linux-sandbox` and inspect logs via `snap logs printingtools-linux-sandbox`.

## Container (Docker/Podman)

Use the `Container/Dockerfile` as a base image. Example:

```bash
podman build -t printingtools-sandbox -f samples/LinuxSandboxHarness/Container/Dockerfile .
podman run --rm --device=/dev/bus/usb --volume=/run/cups/cups.sock:/run/cups/cups.sock printingtools-sandbox
```

## Diagnostics

- Diagnostics flow through `PrintDiagnostics`; run with `RUST_LOG` style filtering using standard output.
- Portal prompts require a desktop session; ensure `XDG_RUNTIME_DIR` and Wayland/X11 sockets are shared into the sandbox.
- Capture D-Bus traffic with `dbus-monitor --session "interface='org.freedesktop.portal.Print'"` when debugging portal failures.
