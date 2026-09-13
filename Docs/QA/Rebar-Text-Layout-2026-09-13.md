# Rebar Text and Layout QA

Date: 2026-09-13

Status: working-tree implementation. Not installed in Revit, not pushed, and
not certified for structural design or live model operation.

## Defects Addressed

- Slab language selector overlapped Assign Data; replaced absolute footer
  positions with a layout-managed language selector and right-aligned actions.
- Slab/edge editors retained a 65px empty header offset and fixed content
  coordinates; replaced with filling layouts and explicit grid toolbar rows.
- Foundation dowel labels, switches and inputs competed for the same row.
  Parameters now have individual label/input rows and full-width switches.
- Slab mesh, support hats, spacer and anchor settings now use auto-height
  rows. Long pages scroll vertically without hiding the action buttons.
- Column options flowed into hidden columns; they now stack vertically.
  Numeric inputs keep their native preferred height. General settings are
  scrollable sections, and Project Cover buttons have adequate height.
- Column template controls now resize with their labels, including English.
  Narrow column editors put the preview below the settings; wide editors
  retain settings and preview side by side.
- Beam parameter labels no longer overlap their input boxes. Main/additional
  bar editors, stirrup distribution and side bars have separated fields.
  Overflowing content and the elevation diagram remain scrollable.
- Cover setup uses complete headers, aligned auto-height rows and plain category names. Removed
  unsupported emoji markers from column selection counts.
- Rebar validation strips reserve their own bottom area. The ErrorProvider
  is disposed with the form.
- Rebar type dropdowns expand for long family names and expose selected
  text through tooltips. Shape descriptions wrap; RFA paths have tooltips.

No rebar generation, anchorage/lap calculations, host containment, family
mapping or Eurocode rules were changed.

## Verification

- net48 Release with Revit 2023 references: passed, zero warnings/errors.
- net8.0-windows Release: passed, zero warnings/errors.
- Command metadata: 78 entry points and 17 workspace bindings passed.
- WPF source fixture: 43 renders, 10 surfaces and 96 embedded icons passed.
- Print layout regression: 57 bounds checks, three window sizes passed.
- Rebar UI state: 53 checks passed (cover toggles, mutually exclusive
  column/stirrup modes, all six Beam editor selections and disabled creation
  without a model/host).
- Source/packaging audit: 9/9 passed, including 44 regression tests and
  14/14 nested MSI source checks. This is not an MSI installation test.
- Rebar full layout matrix: passed, 7,356 control/container checks and 192
  renders. After the final cover-row and edge-caption alignment changes,
  the two affected dialogs were rerun at all six size/scale combinations:
  192 further checks and 12 refreshed renders passed.
- Both target-framework builds and the 53 input-state checks were rerun
  after those final adjustments and passed.
- git diff --check: passed.

The dedicated Rebar fixture constructs the actual production controls without
loading a Document. It covers seven WinForms dialogs, all main tabs, all six
Beam modes, Vietnamese/English rectangular-column labels, minimum/wide
windows and 1x/1.5x/2x scaling simulations. It checks control bounds, preferred
text height, sibling overlap, expanded dropdown width and nonblank renders.
Column previews also have scrolled screenshots.
Container bounds are checked as well as input bounds, specifically to catch
entire rows being placed outside a dialog. Only trailing padding in WinForms
autosized scroll stacks is excluded from the container bound comparison.

These are offline WinForms layout tests, not actual Windows DPI changes.
GDI drawing fonts, native combo painting, real model names, selection and
Revit modal/external-event behavior still need in-Revit inspection.

## Reproduce

From the repository root:

```powershell
dotnet build KhimTools/KhimTools.csproj -c Release -f net48 -p:RevitVersionForReference=2023
powershell -NoProfile -STA -File Tools/Verify-RebarLayout.ps1
powershell -NoProfile -STA -File Tools/Verify-RebarInputState.ps1
```

Artifacts are generated under `artifacts/ui-qa/rebar/` (ignored by Git).
`issues.txt` reports the most recent requested matrix and is empty on success. The script keeps language
changes in the fixture process and does not save the user's language config.
It does not invoke any model-modifying command.

## Remaining Runtime Gate

1. Test the rebuilt DLL through the existing MSI-managed deployment route;
   do not create a second K-TOOLS add-in registration.
2. Open every Rebar dialog in Revit 2023 at actual Windows 100/150/200% DPI,
   with long real family/type names and populated selection lists.
3. Resize, switch tabs/language, scroll, change inputs, test validation,
   keyboard navigation, cancel and reopen. Confirm no occluded controls.
4. Test generation separately on disposable fixtures and inspect resulting
   geometry, dimensions, hosts and shapes. Layout tests cannot certify this.

The Beam source also contains three empty secondary stirrup tabs
(Additional Stirrup, Hanger bar For 2nd Beam, Stirrup Shape). This pass does
not implement those functions or claim that those tabs work.

No installed bundle, MSI, Revit/ETABS application files or user models were
changed by this pass.
