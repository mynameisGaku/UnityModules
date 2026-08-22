# Changelog

## [1.15.0] - 2026-08-23

- Added profile-owned IL2CPP Code Generation selection for OptimizeSpeed or OptimizeSize on the active build target.
- Added target-aware preview, backup schema v15, exact Restore, and target-switch protection.
- Added deterministic planner, backup, service, window, and real PlayerSettings coverage.

## [1.14.0] - 2026-08-22

- Added profile-owned Managed Stripping Level setup for the active build target.
- Added target-aware preview, backup schema v14, exact Restore, and target-switch protection.
- Added deterministic planner, backup, service, window, and real PlayerSettings coverage.

## [1.13.0] - 2026-08-22

- Added profile-owned .NET Standard and .NET Framework API Compatibility Level setup for the active build target.
- Added target-aware preview, backup schema v13, exact Restore, and target-switch protection.
- Added deterministic planner, backup, service, window, and PlayerSettings coverage.

## [1.12.0] - 2026-08-22

- Added profile-owned Mono and IL2CPP Scripting Backend setup for the active build target.
- Added target-aware preview, backup schema v12, exact Restore, and target-switch protection.
- Added deterministic planner, backup, service, window, and real PlayerSettings round-trip coverage.

## [1.11.0] - 2026-08-22

- Added profile-owned Application Identifier setup for the active build target.
- Added portable reverse-domain validation, target-aware preview, backup schema v11, exact Restore, and target-switch protection.
- Added deterministic planner, backup, service, window, and real PlayerSettings round-trip coverage.

## [1.10.0] - 2026-08-22

- Added one Version Control Files workflow that creates Unity-ready .gitignore and .gitattributes files.
- Preserved every existing root file and recorded content hashes for unchanged-only Restore behavior.
- Excluded unconfirmed files created by another process from rollback ownership.
- Added deterministic template, file-system safety, backup schema v10, planner, service, and UI coverage.

## [1.9.0] - 2026-08-22

- Extended Script Assemblies with optional EditMode and PlayMode test Assembly Definitions.
- Added deterministic TestAssemblies references, Editor-only EditMode restrictions, and profile-owned test folder creation.
- Kept the existing no-overwrite and unchanged-content-only restore guarantees for all four generated Assembly Definitions.

## [1.8.0] - 2026-08-22

- Added one Script Assemblies workflow that creates a Runtime Assembly Definition and a matching Editor Assembly Definition with the correct reference.
- Added strict assembly-name and folder validation while preserving every pre-existing Assembly Definition.
- Added backup schema v9 ownership records so Restore removes only tool-created Assembly Definitions whose contents are unchanged.
- Added deterministic planner, serializer, service, and UI regression coverage plus an isolated real AssetDatabase round trip.

## [1.7.0] - 2026-08-22

- Added profile-owned Project Folders with missing-parent preview and exact path validation.
- Added backup schema v8 ownership records so Restore removes only empty folders created by the last Apply.
- Added real AssetDatabase round-trip coverage for empty, used, and pre-existing folders.

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
