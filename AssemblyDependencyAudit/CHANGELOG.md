# Changelog

## [1.5.0] - 2026-08-26

- Added 500-row paging for every issue related to the selected `.asmdef` or `.asmref`, preserving result order and allowing the existing 50,000-issue limit to be reached in at most 100 pages.
- Deduplicated repeated references to the same result index while retaining distinct issues that have identical content.
- Made each page change select its first issue and reset the issue-list and detail scroll positions; selection, Refresh, and result clearing reset paging, while an unchanged filtered selection is clamped to its remaining pages.
- Added fail-visible, all-or-nothing validation for invalid related-issue cache data before any related issue is shown.
- Kept the analyzer, service, model, dependency graph, issue taxonomy, existing clipboard output, public surface, Runtime surface, and build behavior unchanged.

## [1.4.0] - 2026-08-26

- Added a paged `Cycle Component Members` section for the selected `.asmdef`, showing the complete strongly connected component in ordinal asset-path order without presenting that order as a cycle path.
- Added 500-row pages across the existing 10,000-asmdef limit, allowing all members to be reached in at most 20 pages while keeping self-references in their existing issue flow.
- Added fail-visible, all-or-nothing validation for null, out-of-range, duplicate, multiply assigned, or unsafe logical-path data before any cycle-component member is shown.
- Kept the analyzer, model, dependency graph, issue taxonomy, existing clipboard output, 1.3 declared-reference details, public surface, Runtime surface, and build behavior unchanged.

## [1.3.0] - 2026-08-26

- Added paged declared-reference details for the selected `.asmdef`, preserving declaration order and duplicate entries while showing each raw Name/GUID reference and its uniquely resolved target.
- Added 500-row pages across the existing 4,096-reference-per-assembly limit, with surrogate-safe 160-character values and explicit fail-visible rows for null, unknown, or invalid result data.
- Kept `-1` resolution non-conclusive as `Not uniquely resolved`; existing issue details continue to distinguish unresolved and ambiguous references.
- Kept the analyzer, model, deduplicated dependency graph, asmref selection, existing clipboard output, public surface, and Runtime surface unchanged.

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
