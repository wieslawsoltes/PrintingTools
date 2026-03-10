---
title: "PrintingTools"
layout: simple
og_type: website
---

<div class="pt-hero">
  <div class="pt-eyebrow"><i class="bi bi-printer-fill" aria-hidden="true"></i> Cross-Platform .NET Printing</div>
  <h1>PrintingTools</h1>

  <p class="lead"><strong>PrintingTools</strong> delivers printer discovery, native dialogs, preview generation, pagination, diagnostics, and managed/vector print output across Windows, macOS, and Linux.</p>

  <div class="pt-hero-actions">
    <a class="btn btn-primary btn-lg" href="articles/getting-started/overview"><i class="bi bi-rocket-takeoff" aria-hidden="true"></i> Start Getting Started</a>
    <a class="btn btn-outline-secondary btn-lg" href="api"><i class="bi bi-braces" aria-hidden="true"></i> Browse API</a>
    <a class="btn btn-outline-secondary btn-lg" href="https://github.com/wieslawsoltes/PrintingTools"><i class="bi bi-github" aria-hidden="true"></i> GitHub Repository</a>
  </div>
</div>

## Start Here

<div class="pt-link-grid">
  <a class="pt-link-card" href="articles/getting-started/overview">
    <span class="pt-link-card-title"><i class="bi bi-signpost-split" aria-hidden="true"></i> Overview</span>
    <p>Understand the package split, platform model, and the end-to-end print workflow.</p>
  </a>
  <a class="pt-link-card" href="articles/getting-started/installation">
    <span class="pt-link-card-title"><i class="bi bi-download" aria-hidden="true"></i> Installation</span>
    <p>Set up the right packages, prerequisites, and native dependencies for each platform.</p>
  </a>
  <a class="pt-link-card" href="articles/getting-started/quickstart-avalonia">
    <span class="pt-link-card-title"><i class="bi bi-window-sidebar" aria-hidden="true"></i> Avalonia Quickstart</span>
    <p>Wire `UsePrintingTools`, open preview UI, and submit a print job from a real window.</p>
  </a>
  <a class="pt-link-card" href="articles/getting-started/quickstart-headless">
    <span class="pt-link-card-title"><i class="bi bi-file-earmark-pdf" aria-hidden="true"></i> Headless Quickstart</span>
    <p>Generate previews or PDF output in test harnesses, CI, and service-style workflows.</p>
  </a>
</div>

## Documentation Sections

<div class="pt-link-grid pt-link-grid--wide">
  <a class="pt-link-card" href="articles/concepts">
    <span class="pt-link-card-title"><i class="bi bi-diagram-3" aria-hidden="true"></i> Concepts</span>
    <p>Architecture, session lifecycle, pagination, preview models, and diagnostics fundamentals.</p>
  </a>
  <a class="pt-link-card" href="articles/guides">
    <span class="pt-link-card-title"><i class="bi bi-journal-code" aria-hidden="true"></i> Guides</span>
    <p>Scenario-driven docs for UI integration, harnesses, migration, and troubleshooting.</p>
  </a>
  <a class="pt-link-card" href="articles/platforms">
    <span class="pt-link-card-title"><i class="bi bi-pc-display-horizontal" aria-hidden="true"></i> Platforms</span>
    <p>Adapter-specific behavior, deployment notes, and support expectations for Windows, macOS, and Linux.</p>
  </a>
  <a class="pt-link-card" href="articles/advanced">
    <span class="pt-link-card-title"><i class="bi bi-speedometer2" aria-hidden="true"></i> Advanced</span>
    <p>CI validation, golden metrics, release packaging, and extension points for power users.</p>
  </a>
  <a class="pt-link-card" href="articles/reference">
    <span class="pt-link-card-title"><i class="bi bi-collection" aria-hidden="true"></i> Reference</span>
    <p>Package maps, namespace indexes, docs pipeline details, and project-level operational reference.</p>
  </a>
  <a class="pt-link-card" href="api">
    <span class="pt-link-card-title"><i class="bi bi-braces-asterisk" aria-hidden="true"></i> API Documentation</span>
    <p>Generated .NET API pages for the six ship-ready assemblies that make up PrintingTools.</p>
  </a>
</div>

## Package Families

| Package | Purpose |
| --- | --- |
| `PrintingTools` | Avalonia bootstrapper that picks a platform adapter and wires shared options into the app startup path. |
| `PrintingTools.Core` | Contracts, sessions, pagination, preview models, rendering helpers, and diagnostics. |
| `PrintingTools.UI` | Optional Avalonia page setup and preview controls. |
| `PrintingTools.Windows` / `PrintingTools.MacOS` / `PrintingTools.Linux` | Native or platform-aware adapters for queue discovery, capabilities, dialogs, and print submission. |

## Repository

- Source code and issues: [github.com/wieslawsoltes/PrintingTools](https://github.com/wieslawsoltes/PrintingTools)
- Published site base URL: [wieslawsoltes.github.io/PrintingTools](https://wieslawsoltes.github.io/PrintingTools)
