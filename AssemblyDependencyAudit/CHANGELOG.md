# Changelog

## [1.2.0] - 2026-08-26

- Added a path-level finding for every `.asmdef` or `.asmref` that shares its exact parent folder with another assembly owner candidate.
- Kept malformed and physical-only assembly assets visible in folder ownership checks while preserving their existing JSON and target findings.
- Preserved the dependency graph and asmref target resolution when reporting folder ownership conflicts.

## [1.1.0] - 2026-08-26

- Added a separate read-only list for every `.asmref` under Assets and registered Packages.
- Added strict JSON, missing target, unresolved target, and ambiguous target diagnostics for assembly references.
- Kept `.asmref` target ownership separate from the asmdef dependency graph so valid references never add dependency edges.
- Added bounded physical discovery, strict UTF-8 reads, deterministic ordering, and fail-closed source limits for `.asmref` files.
- Matched physical discovery to Unity's ignored file and directory rules so hidden assets never become audit findings.
- Matched assembly-name uniqueness and reference resolution to Unity's case-insensitive compiler behavior.
- Added an explicit terminal error for typed assembly assets whose physical paths cross reparse points.

## [1.0.0] - 2026-08-25

- Added a read-only three-column view of Assembly Definition dependencies under Assets and Packages.
- Added cycle, self-reference, Player-to-Editor reference, invalid definition, duplicate name, unresolved reference, ambiguous reference, mixed reference style, and platform configuration diagnostics.
- Added deterministic scanning, search, and issue filtering in an Editor-only window.
