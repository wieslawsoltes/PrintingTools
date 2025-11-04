# Page Setup Dialog Specification

## Goals

- Provide a cross-platform Avalonia dialog mirroring key WPF `PageSetupDialog` capabilities while respecting modern UX guidelines.
- Enable users to configure paper size, orientation, and margins with immediate visual feedback.
- Integrate seamlessly with the `PrintOptions` pipeline so configured values flow into subsequent preview/print workflows.

## Functional Requirements

1. **Paper Size Selection**
   - Present common paper sizes (Letter, Legal, Tabloid, A4, A5).
   - Allow injection of custom sizes via `PageSetupViewModel.PaperSizes`.
   - Display sizes in inches; metric equivalent can be added later.

2. **Orientation**
   - Toggle between portrait and landscape.
   - Preview updates to reflect orientation change.

3. **Margins**
   - Input fields for top, right, bottom, left in inches.
   - Clamped to 0–2 inches by default; allow override through view-model.

4. **Options**
   - `Use Printable Area`: toggles whether engine respects reported printable area from capabilities.
   - `Center Horizontally` / `Center Vertically`: layout alignment hints.
   - `Show Header/Footer`: placeholder toggle for future header/footer configuration.

5. **Layout Selection**
   - Dropdown exposing `Standard`, `N-up`, `Booklet`, and `Poster` values mapped to `PrintLayoutKind`.
   - N-up mode: configure rows, columns, and ordering (`NUpPageOrder`).
   - Booklet mode: toggle long-edge vs short-edge binding.
   - Poster mode: select total tile count; grid derived automatically to match page aspect ratio.

6. **Preview Panel**
   - Scaled visualization of selected paper size with margin outline.
   - Orientation and margins reflected instantly.

7. **Commands**
   - `Apply`: raises `RequestClose` event; returns updated `PrintOptions` via `ApplyTo` helper.
   - `Cancel`: closes without applying.

## Integration

- `PageSetupViewModel.ApplyTo(PrintOptions)` produces a cloned instance with dialog selections applied.
- Sample host (`PageSetupDialogHost`) embedded in Avalonia sample for manual testing.
- Main preview workflow now consumes updated `PrintOptions`; paginator and session ticket wiring ensure paper size, margins, orientation, and layout selections flow into preview and native adapters.
- Dialog surfaces layout selection with contextual settings (N-up grid/order, booklet binding, poster tiling), feeding directly into `PrintOptions`.
