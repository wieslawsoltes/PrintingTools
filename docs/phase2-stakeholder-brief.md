# Phase 2 Stakeholder Brief – Printing Parity

## Objective
Align on the WPF-to-Avalonia printing parity scope, confirm tiered feature priorities, and secure approval to proceed with platform-specific implementation work.

## Key Artefacts
- `docs/feature-parity-matrix.md` – detailed baseline/enhanced/advanced capability mapping with current Avalonia and macOS statuses.
- `docs/ui-reference-printing.md` – upcoming Windows UX capture reference to support dialog and workflow design decisions.
- `docs/wpf-printing-parity-plan.md` – master roadmap outlining phases, tasks, and cross-platform dependencies.

## Proposed Milestones
1. **Baseline Parity (Tier 0)** – Deliver Avalonia dialog, queue enumeration, paginator integration, and unified job submission pipeline on Windows + macOS. *Target:* Q4 2024 preview.
2. **Enhanced Fidelity (Tier 1)** – Add duplex/copies/scaling controls with capability detection, vector-first rendering parity, and macOS accessory panels. *Target:* Q1 2025 beta.
3. **Advanced Extensions (Tier 2)** – Optional job monitoring, vendor ticket editing, and export workflows. *Target:* Backlog grooming for post-1.0 releases.

## Success Criteria & Deviations
- Tier 0 features must match WPF semantics for FlowDocument/FixedDocument scenarios; any gaps require documented workarounds and regression coverage.
- Tier 1 deviations permitted only when platform APIs differ (e.g., macOS native panels); provide mitigation notes and follow-up issues.
- Tier 2 work tracked via discrete backlog items with risk assessment and resource estimates.

## Open Risks
- **Windows integration:** Need clarity on leveraging existing WPF dialogs via interop vs. building Avalonia-native replacements.
- **Capability detection:** Requires upstream Avalonia hooks for printer metadata and device capabilities.
- **Resource planning:** Dedicated engineering for native bridges (Objective-C, Win32) and test automation. 

## Decision Requests
1. Confirm tier priorities and milestone targets.
2. Approve baseline feature scope and documented deviations.
3. Assign stakeholder owners for Windows and macOS implementation tracks.

## Next Actions
- Review meeting booked for 2024-07-24 16:00 UTC with platform leads and UX.
- Finalize UX capture (`docs/ui-reference-printing.md`) prior to the session.
- Collect feedback and update `docs/wpf-printing-parity-plan.md` Phase 2 tasks accordingly.
