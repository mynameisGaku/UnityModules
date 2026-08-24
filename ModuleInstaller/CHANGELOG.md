# Changelog

## [1.5.0] - 2026-08-24

- 65個から統合された24 packageのうち、公開tagを持つ22件を6 bundle（`7 / 7 / 3 / 3 / 1 / 1`）としてcatalogへ整理しました。
- Input Assist 2.0.0、Input Command 1.0.0、Gameplay Rules 1.0.0、Deterministic Simulation 1.0.0へ、旧44 packageの機能をnamespaceと公開型名を維持したまま集約しました。
- Scene and UIへPlay Mode Tuning 1.0.0を追加し、7件構成へ更新しました。対象選択、Play Mode中の手動取り込み、非変更Preview、確認、同一・未使用計画の単回反映、Scene dirty、保存は手動、ApplyとRollbackの個別結果を案内します。
- Input SupportへInput Command 1.0.0を追加し、Input Assist、Input Command、Input Gateの3件構成へ更新しました。
- 統合前のUPM package IDまたは`Assets/Modules/<旧Folder>`が残る場合、導入・更新ともPackage Managerの変更前に停止する競合検査を追加しました。旧packageやsource copyは自動削除せず、利用者が内容を確認して手動で移行します。
- 個別一覧を22件へ更新し、bundle、更新一覧、競合表示、README導線の回帰testを新しいcatalogへ合わせました。

## [1.4.9] - 2026-08-23

- 「シーン作業セット」v1.0.0をcatalogとScene and UIへ追加しました。
- Scene and UIを6件、個別一覧を43件へ拡張し、EditorのScene作業構成をRuntimeのScene遷移より前へ配置しました。
- package導入、Profile作成・編集・Capture、確認済みScene構成の切り替えが変更する範囲を分けて明記しました。
- catalog、bundle順、Editor windowの表示順と個別導線を回帰testで固定しました。
- 導入済みprereleaseをSemVer順で比較し、公開済み安定版への更新を誤って不要扱いしないようにしました。

## [1.4.8] - 2026-08-23

- 「ビルド実行アシスタント」v1.0.0をcatalogとProject Maintenanceへ追加しました。
- Project Maintenanceを7件、個別一覧を42件へ拡張し、build実行をworkflowの最後に配置しました。
- package導入だけではProjectや出力先を変更せず、Build Assistantで確認済みbuildを実行した場合だけ新しい出力folderと履歴を作る範囲を明記しました。
- catalog、bundle順、Editor windowの個別導線を回帰testで固定しました。

## [1.4.7] - 2026-08-23

- Added Asset Import Audit v1.1.0 to the catalog and Project Maintenance workflow.
- Moved each workflow install action below its guide and package list so the window reads from selection to confirmation to installation.
- Clarified that texture importer settings change only after Asset Import Audit's own preview and apply actions.
- Expanded catalog and Editor window regression coverage to 41 pinned modules.
- Added real Editor screenshots and a numbered top-to-bottom operation guide.

## [1.4.6] - 2026-08-23

- Corrected the documentation heading and pinned package URLs for the v1.4.6 release.

## [1.4.5] - 2026-08-23

- Updated Project Setup to v1.15.0 for build-target IL2CPP Code Generation preview, backup, apply, and restore.
- Added speed-versus-size guidance to the Project Maintenance workflow.

## [1.4.4] - 2026-08-22

- Updated Project Setup to v1.14.0 for build-target Managed Stripping Level preview, backup, apply, and restore.
- Added managed code stripping guidance to the Project Maintenance workflow.

## [1.4.3] - 2026-08-22

- Updated Project Setup to v1.13.0 for build-target API Compatibility Level preview, backup, apply, and restore.
- Added .NET Standard and .NET Framework guidance to the Project Maintenance workflow.

## [1.4.2] - 2026-08-22

- Updated Project Setup to v1.12.0 for build-target Scripting Backend preview, backup, apply, and restore.
- Added Scripting Backend guidance to the Project Maintenance workflow.

## [1.4.1] - 2026-08-22

- Updated Project Setup to v1.11.0 for build-target application identifier preview, backup, apply, and restore.
- Added application identifier guidance to the Project Maintenance workflow.

## [1.4.0] - 2026-08-22

- Grouped the default window around four practical workflows and moved deterministic and game-rule libraries into a collapsed specialized section.
- Added a quick guide to every workflow with use cases, the first action after installation, and the possible change scope.
- Added pinned README links to every individual module row.

## [1.3.9] - 2026-08-22

- Updated Project Setup to v1.10.0 for Unity-ready .gitignore and .gitattributes setup.
- Clarified that existing files are preserved and Restore removes only unchanged files created by Project Setup.

## [1.3.8] - 2026-08-22

- Updated Project Setup to v1.9.0 for optional EditMode and PlayMode test assembly definition setup.
- Clarified the generated test assembly names, references, and safe restore behavior.

## [1.3.7] - 2026-08-22

- Updated Project Setup to v1.8.0 for safe Runtime and Editor assembly definition setup.
- Clarified that Project Setup never overwrites existing assembly definitions and restores only unchanged files it created.

## [1.3.6] - 2026-08-22

- Updated Project Setup to v1.7.0 for profile-owned recommended project folders.
- Clarified that restore removes only empty folders created by Project Setup and preserves existing or used folders.

## [1.3.5] - 2026-08-22

- Updated Project Setup to v1.6.0 for profile-owned duplicate GameObject and Asset naming defaults.
- Clarified that duplicate naming affects future duplicate operations and does not rename existing objects or assets.

## [1.3.4] - 2026-08-22

- Updated Project Setup to v1.5.0 for profile-owned C# root namespace and new-script line ending defaults.
- Clarified that Project Maintenance applies and restores code generation defaults together with existing project settings.

## [1.3.3] - 2026-08-22

- Updated Project Setup to v1.4.0 for additive scripting define symbols with target-aware backup restoration.
- Clarified that Project Maintenance can configure compile conditions without replacing existing symbols.

## [1.3.2] - 2026-08-22

- Updated Project Setup to v1.3.0 for a profile-owned Play Mode Start Scene in addition to ordered Player Build Scenes.
- Clarified that the two Scene settings serve different workflows and remain independently optional.

## [1.3.1] - 2026-08-22

- Updated Project Setup to v1.2.0 for ordered Build Scenes with enabled-state restoration.
- Clarified which workflow bundle to choose before opening the compatibility-focused individual list.
- Simplified the catalog version assertion so future semantic versions remain covered.

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
