# Changelog

## [1.6.0] - 2026-08-22

- Added one profile-owned Duplicate Naming setting for GameObject suffix style, minimum number digits, and Asset copy spacing.
- Extended preview, backup schema v7, Apply verification, Restore, Editor UI, and real EditorSettings coverage to duplicate naming defaults.

## [1.5.0] - 2026-08-22

- Added profile-owned Root Namespace and new C# script line endings.
- Extended preview, backup schema v6, Apply verification, and Restore to code generation defaults.
- Added a real EditorSettings round-trip test and namespace validation coverage.

## [1.4.0] - 2026-08-22

- Added profile-owned scripting define symbols for the active build target.
- Preserved existing symbols during Apply while recording the exact pre-Apply list for Restore.
- Added validation, preview, backup schema v5, and Editor window regression coverage.

## [1.3.0] - 2026-08-22

- Added a GUID-tracked Play Mode Start Scene to reusable project profiles.
- Extended preview, backup schema, verification, rollback, and restore to the Editor start Scene.
- Added real Scene integration tests for applying and restoring the Play Mode Start Scene.
- Clarified the difference between the Editor-only start Scene and Player Build Scenes.

## [1.2.0] - 2026-08-22

- Added ordered Build Scene profiles with per-Scene enabled state and GUID-based move tracking.
- Added active Build Profile and global Build Scene target handling.
- Extended preview, backup, verification, rollback, and restore flows to Build Scenes.
- Added validation for missing, duplicate, empty, and disabled startup Scenes.
- Reworked the README around outcomes, shortest usage, side effects, and restore steps.

## [1.1.0] - 2026-08-22

- Added additive custom Tag, user Layer, and Sorting Layer setup from reusable profiles.
- Added capacity and duplicate-name validation before TagManager changes.
- Extended backups to restore TagManager names, slots, order, and Sorting Layer identifiers exactly.
- Kept schema version 1 backups readable without changing TagManager data.

## [1.0.0] - 2026-08-22

- Added reusable Project Settings profile assets.
- Added deterministic change previews and validated application.
- Added last-state backup and restore support.
- Added an Editor window for profile creation, capture, preview, apply, and restore.
