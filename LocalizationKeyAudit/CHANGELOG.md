# Changelog

All notable changes to this package will be documented in this file.

## [1.0.0] - 2026-08-25

### Added

- Added a manual, Editor-only advisory audit for required-locale direct coverage and localization table integrity.
- Added the `Tools/Localization Key Audit/Open` window with explicit required Locale and Assets-only scope inputs, issue filters, details, and a 500-row display cap.
- Added distinct `MissingLocaleTable`, `MissingDirectEntry`, and `EmptyDirectValue` findings without inferring runtime translation availability.
- Added `NoStaticReferenceFoundWithinDeclaredScope` with explicit static-reference coverage instead of an unused-key conclusion.
- Added raw Shared Table Data preflight and terminal `ReadOnlyGuaranteeUnavailable` handling before typed loading.
- Added duplicate and orphan integrity findings without automatic repair or deletion.
- Documented excluded package, source-code, dynamic, Smart String nested, Addressables, external-data, and out-of-scope asset references.
- Declared Unity Localization 1.5.12 as the only direct package dependency.

### Boundaries

- No Runtime assembly or public API.
- No build blocking or automatic build integration.
- No asset mutation, autofix, or deletion.
