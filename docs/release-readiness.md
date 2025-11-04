# Release Readiness Snapshot

This checklist captures what is already in place for an initial public release of PrintingTools and what remains to be addressed before tagging a versioned build.

## Verified
- ✅ Repository builds and packs successfully on macOS: `dotnet pack PrintingTool.sln --configuration Release` (local run `$(date -u +"%Y-%m-%d")`).
- ✅ Automated tests pass: `dotnet test PrintingTool.sln --configuration Release` (12 tests, no failures).
- ✅ NuGet packaging metadata applied to core, platform, UI, and aggregate projects (see `Directory.Build.props`, `src/PrintingTools*/PrintingTools*.csproj`).
- ✅ GitHub Actions workflows for `build-and-pack` (PR/main) and `release` (tag push) compile native components per platform, stage artifacts, and pack all NuGets.
- ✅ Release workflow pushes packages to nuget.org when `NUGET_API_KEY` is present and publishes a GitHub release with packaged artifacts.

## Outstanding Before First Release
- 🔲 Set the final semantic version via `VersionPrefix`/`PackageVersion` in `Directory.Build.props` (current default is `0.1.0`).
- 🔲 Populate `NUGET_API_KEY` repository secret (required for automated NuGet publishing).
- 🔲 Decide on target .NET SDK (currently `10.0.100-rc.2`; upgrade to GA before RTM or note preview dependency in release notes).
- 🔲 Run platform harnesses (`linux`, `macOS`, `windows`) to produce fresh metrics/PDFs and confirm CI parity.
- 🔲 Draft release notes / changelog entry summarising features, supported platforms, and known limitations.
- 🔲 Verify macOS native bridge signing/notarisation requirements if distributing outside development environments.
- 🔲 Smoke-test packaged NuGets in a clean sample (e.g., create a minimal Avalonia app consuming `PrintingTools` package).

## Optional Enhancements
- ☐ Add automated changelog generation or keep a manual `CHANGELOG.md`.
- ☐ Configure package icon/description polish on NuGet for better discoverability.
- ☐ Add validation that native artifacts exist in Linux/Windows builds (currently stubs copy only when content is present).
- ☐ Expand unit/integration coverage for pagination edge cases and platform adapters.

Update this document as each action is completed to maintain a clear go/no-go view for release readiness.***
