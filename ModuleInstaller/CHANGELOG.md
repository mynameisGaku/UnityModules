# Changelog

## [1.3.0] - 2026-08-22

- Renamed the visible tool to Module Manager while keeping the package ID and legacy menu path compatible.
- Added an update preview and one-request update for installed catalog packages below the pinned version.
- Prevented automatic downgrades and overwrites of newer or custom package versions.
- Added update planning, domain reload recovery, and Editor window regression tests.

## [1.2.0] - 2026-08-22

- Updated Project Setup to v1.1.0 for Tags, Layers, and Sorting Layers.
- Added a catalog regression test for the pinned Project Setup release.
- Kept individual module descriptions and install buttons inside the minimum window width.

## [1.1.0] - 2026-08-22

- Added Project Setup to the catalog and Project Maintenance bundle.
- Added a catalog regression test for the complete Project Maintenance workflow.

## [1.0.0] - 2026-08-22

### Added

- Added a task-based catalog of 39 pinned UnityModules packages.
- Added six practical bundles for project maintenance, Scene and UI, game services, input support, deterministic simulation, and game rules.
- Added installed-package filtering and Assets/Modules copy conflict detection.
- Added one-request bundle installation with SessionState recovery across domain reloads.
- Added an Editor window with bundle-first and advanced individual installation views.
- Added button states for remaining package counts, completed bundles, and Assets copy conflicts.
