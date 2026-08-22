# Changelog

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
