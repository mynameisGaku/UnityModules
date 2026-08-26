# Changelog

All notable changes to this package will be documented in this file.

## [1.2.0] - 2026-08-26

### Added

- Added unfiltered finding counts for `Terminal`, `Required Locale Coverage`, `Static References`, and `Integrity`, with every existing issue kind assigned to exactly one category.
- Counted the complete result snapshot independently of Search, Category, and the 500-row display cap.

### Boundaries

- Kept `Complete`／`Incomplete` and static coverage completion authoritative; a zero category count in an incomplete result is not proof that the category is safe or absent.
- Kept the issue taxonomy, read-only Editor-only behavior, advisory scope, and zero Runtime or public API surface unchanged.

## [1.1.0] - 2026-08-26

### Added

- Added static-reference coverage for explicitly declared `Assets[/...]` or `Packages/<registered-name>[/...]` paths, using exactly one logical root per audit and allowing multiple paths only within that root.
- Added exact registered package-name resolution through `PackageInfo.resolvedPath` while keeping physical paths out of the window, audit result, errors, and clipboard; read errors expose only logical paths and exception types.

### Safety

- Reject bare `Packages`, direct `Library/PackageCache` paths, unregistered package names, mixed Assets/package roots, multiple package roots, and explicit paths with `~`, `:`, or a segment ending in dot or space before filesystem access.
- Apply discovery, file, byte, reference, and issue limits audit-wide across all declared paths under the single logical root.
- Fail closed without partial coverage results for normalized duplicate targets, reparse points on the root, any root ancestor, or a selected child path, and root escape.

### Boundaries

- Kept the raw preflight, typed snapshot, graph, and issue taxonomy unchanged.
- Kept the audit read-only, Editor-only, advisory, and free of Runtime or public API additions.

## [1.0.0] - 2026-08-25

### Added

- Added a manual, Editor-only advisory audit for required-locale direct coverage and localization table integrity.
- Added the `Tools/Localization Key Audit/Open` window with explicit required Locale and Assets-only scope inputs, issue filters, details, and a 500-row display cap.
- Added distinct `MissingLocaleTable`, `MissingDirectEntry`, and `EmptyDirectValue` findings without inferring runtime translation availability.
- Added `NoStaticReferenceFoundWithinDeclaredScope` with explicit static-reference coverage instead of an unused-key conclusion.
- Added raw Shared Table Data preflight and terminal `ReadOnlyGuaranteeUnavailable` handling before typed loading.
- Added duplicate and orphan integrity findings without automatic repair or deletion.
- Added String and Asset Table owner classification so Asset-only Shared Table Data does not become a String key finding; cross-type GUID collisions fail closed.
- Documented excluded package, source-code, dynamic, Smart String nested, Addressables, external-data, and out-of-scope asset references.
- Declared Unity Localization 1.5.12 as the only direct package dependency.

### Boundaries

- No Runtime assembly or public API.
- No build blocking or automatic build integration.
- No asset mutation, autofix, or deletion.
