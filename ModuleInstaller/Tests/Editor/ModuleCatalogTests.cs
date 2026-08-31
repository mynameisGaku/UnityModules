// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ModuleInstaller.Editor.Tests
{
    internal sealed class ModuleCatalogTests
    {
        [Test]
        public void Catalog_UsesUniquePinnedPackageEntries()
        {
            Assert.That(ModuleCatalog.Entries.Count, Is.EqualTo(22));
            var packageNames = new HashSet<string>(StringComparer.Ordinal);
            var folderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < ModuleCatalog.Entries.Count; index++)
            {
                var entry = ModuleCatalog.Entries[index];
                Assert.That(packageNames.Add(entry.PackageName), Is.True, entry.PackageName);
                Assert.That(folderNames.Add(entry.FolderName), Is.True, entry.FolderName);
                Assert.That(entry.GitUrl, Does.Contain($"?path=/{entry.FolderName}#{entry.Tag}"));
                Assert.That(entry.GitUrl, Does.Not.Contain("#main"));
                Assert.That(entry.GitUrl, Does.Not.Contain("#dev"));
                Assert.That(entry.GuideUrl, Is.EqualTo($"https://github.com/mynameisGaku/UnityModules/blob/{entry.Tag}/{entry.FolderName}/{entry.GuideRelativePath}"));
                Assert.That(entry.GuideRelativePath, Does.Not.StartWith("/"));
                Assert.That(entry.GuideRelativePath, Does.Not.Contain(".."));
                Assert.That(entry.Version, Is.Not.Empty);
                Assert.That(entry.Tag, Does.EndWith($"-v{entry.Version}"));
                Assert.That(entry.Tag, Does.Match(@"-v\d+\.\d+\.\d+$"));
            }
        }

        [Test]
        public void Catalog_UsesJapaneseVisibleText()
        {
            for (var index = 0; index < ModuleCatalog.Entries.Count; index++)
            {
                var entry = ModuleCatalog.Entries[index];
                Assert.That(ContainsJapanese(entry.DisplayName), Is.True, entry.PackageName);
                Assert.That(ContainsJapanese(entry.Summary), Is.True, entry.PackageName);
            }

            for (var index = 0; index < ModuleCatalog.Bundles.Count; index++)
            {
                var bundle = ModuleCatalog.Bundles[index];
                Assert.That(ContainsJapanese(bundle.DisplayName), Is.True, bundle.Id);
                Assert.That(ContainsJapanese(bundle.Summary), Is.True, bundle.Id);
                Assert.That(ContainsJapanese(bundle.UseWhen), Is.True, bundle.Id);
                Assert.That(ContainsJapanese(bundle.FirstStep), Is.True, bundle.Id);
                Assert.That(ContainsJapanese(bundle.ChangeScope), Is.True, bundle.Id);
            }
        }

        [Test]
        public void Bundles_ReferenceKnownPackagesWithoutDuplicatesWithinBundle()
        {
            Assert.That(ModuleCatalog.Bundles.Count, Is.EqualTo(6));
            var bundleIds = new HashSet<string>(StringComparer.Ordinal);
            var recommendedCount = 0;
            var specializedCount = 0;

            for (var bundleIndex = 0; bundleIndex < ModuleCatalog.Bundles.Count; bundleIndex++)
            {
                var bundle = ModuleCatalog.Bundles[bundleIndex];
                Assert.That(bundleIds.Add(bundle.Id), Is.True, bundle.Id);
                Assert.That(bundle.UseWhen, Is.Not.Empty, bundle.Id);
                Assert.That(bundle.FirstStep, Is.Not.Empty, bundle.Id);
                Assert.That(bundle.ChangeScope, Does.Contain("導入操作"), bundle.Id);
                if (bundle.Tier == ModuleBundleTier.Recommended)
                {
                    recommendedCount++;
                }
                else
                {
                    specializedCount++;
                }

                Assert.That(bundle.PackageNames.Count, Is.GreaterThan(0));
                var packageNames = new HashSet<string>(StringComparer.Ordinal);
                for (var packageIndex = 0; packageIndex < bundle.PackageNames.Count; packageIndex++)
                {
                    var packageName = bundle.PackageNames[packageIndex];
                    Assert.That(packageNames.Add(packageName), Is.True, packageName);
                    Assert.That(ModuleCatalog.TryFindEntry(packageName, out _), Is.True, packageName);
                }
            }

            Assert.That(recommendedCount, Is.EqualTo(4));
            Assert.That(specializedCount, Is.EqualTo(2));
        }

        [Test]
        public void RecommendedInputBundle_DoesNotExposeLegacyMicroPackages()
        {
            Assert.That(ModuleCatalog.TryFindBundle("input-support", out var bundle), Is.True);
            Assert.That(bundle.PackageNames, Is.EquivalentTo(new[]
            {
                "com.studiogaku.input-assist",
                "com.studiogaku.input-command",
                "com.studiogaku.input-gate"
            }));
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.input-assist", out var inputAssist), Is.True);
            Assert.That(inputAssist.DisplayName, Is.EqualTo("入力補助"));
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.input-command", out var inputCommand), Is.True);
            Assert.That(inputCommand.DisplayName, Is.EqualTo("入力コマンド"));
            Assert.That(inputCommand.Summary, Does.Contain("刻み単位の並び判定").And.Contain("状態を持たない優先選択").And.Contain("入力標本を使う安定化"));
        }

        [Test]
        public void ProjectMaintenanceBundle_UsesSetupRepairAndBuildOrder()
        {
            Assert.That(ModuleCatalog.TryFindBundle("project-maintenance", out var bundle), Is.True);
            Assert.That(bundle.PackageNames, Is.EqualTo(new[]
            {
                "com.studiogaku.project-setup",
                "com.studiogaku.asset-import-audit",
                "com.studiogaku.inspector",
                "com.studiogaku.drawing",
                "com.studiogaku.build-guard",
                "com.studiogaku.reference-finder",
                "com.studiogaku.build-assistant"
            }));
        }

        [Test]
        public void SceneAndUiBundle_UsesWorkspaceBeforeRuntimeSceneFlowOrder()
        {
            Assert.That(ModuleCatalog.TryFindBundle("scene-and-ui", out var bundle), Is.True);
            Assert.That(bundle.PackageNames, Is.EqualTo(new[]
            {
                "com.studiogaku.scene-workspace",
                "com.studiogaku.play-mode-tuning",
                "com.studiogaku.scene-flow",
                "com.studiogaku.screen-transition",
                "com.studiogaku.adaptive-layout",
                "com.studiogaku.time-control",
                "com.studiogaku.startup-flow"
            }));
            Assert.That(bundle.Summary, Does.Contain("再生中調整").And.Contain("シーン切り替え"));
            Assert.That(bundle.FirstStep, Does.Contain("Tools > Scene Workspace > Open").And.Contain("Tools > Play Mode Tuning > Open").And.Contain("再生中に手動記録").And.Contain("再生終了後の差分確認"));
            Assert.That(bundle.ChangeScope, Does.Contain("導入操作が変更するのはパッケージ設定だけ").And.Contain("シーン順").And.Contain("未保存変更の破棄は行いません").And.Contain("有効な同一の確認済み計画").And.Contain("未保存状態").And.Contain("復元結果は別々に報告"));
        }

        [Test]
        public void ProjectSetup_UsesVersionControlAndAssemblyDefinitionCapableRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.project-setup", out var entry), Is.True);
            Assert.That(entry.Tag, Is.EqualTo("project-setup-v1.15.0"));
            Assert.That(entry.GuideRelativePath, Is.EqualTo("Documentation~/index.md"));
            Assert.That(entry.GuideUrl, Does.Not.Contain("project-setup-v1.14.0"));
            Assert.That(entry.Summary, Does.Contain("推奨フォルダー").And.Contain("試験用アセンブリ定義").And.Contain("版管理ファイル").And.Contain("管理コード削減強度").And.Contain("コード生成規則").And.Contain("複製時の命名規則").And.Contain("条件付きコンパイル記号").And.Contain("タグ").And.Contain("レイヤー").And.Contain("ビルド対象シーン").And.Contain("再生開始シーン"));
        }

        [Test]
        public void AssetImportAudit_UsesPlatformTextureAuditRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.asset-import-audit", out var entry), Is.True);
            Assert.That(entry.FolderName, Is.EqualTo("AssetImportAudit"));
            Assert.That(entry.Tag, Is.EqualTo("asset-import-audit-v1.1.0"));
            Assert.That(entry.DisplayName, Is.EqualTo("テクスチャー取込設定監査"));
            Assert.That(entry.Summary, Does.Contain("共通設定").And.Contain("Standalone").And.Contain("Android").And.Contain("iOS").And.Contain("差分を確認"));
        }

        [Test]
        public void BuildAssistant_UsesReviewedDesktopBuildRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.build-assistant", out var entry), Is.True);
            Assert.That(entry.FolderName, Is.EqualTo("BuildAssistant"));
            Assert.That(entry.Tag, Is.EqualTo("build-assistant-v1.0.0"));
            Assert.That(entry.DisplayName, Is.EqualTo("ビルド実行アシスタント"));
            Assert.That(entry.Summary, Does.Contain("デスクトップ向けビルド計画").And.Contain("新しい出力フォルダー").And.Contain("件数を制限した履歴").And.Contain("容量差").And.Contain("JSON形式の報告"));
        }

        [Test]
        public void SceneWorkspace_UsesReviewedMultiSceneEditorRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.scene-workspace", out var entry), Is.True);
            Assert.That(entry.FolderName, Is.EqualTo("SceneWorkspace"));
            Assert.That(entry.Tag, Is.EqualTo("scene-workspace-v1.0.0"));
            Assert.That(entry.DisplayName, Is.EqualTo("シーン作業セット"));
            Assert.That(entry.Summary, Does.Contain("複数シーンの順番").And.Contain("計画の有効性確認").And.Contain("切り替え後の検証").And.Contain("復元報告"));
        }

        [Test]
        public void Catalog_ExposesConsolidatedPackagesInsteadOfMicroPackages()
        {
            var consolidated = new[]
            {
                "com.studiogaku.input-assist",
                "com.studiogaku.input-command",
                "com.studiogaku.gameplay-rules",
                "com.studiogaku.deterministic-simulation"
            };

            for (var index = 0; index < consolidated.Length; index++)
            {
                Assert.That(ModuleCatalog.TryFindEntry(consolidated[index], out _), Is.True, consolidated[index]);
            }

            var retired = new[]
            {
                "com.studiogaku.resource-meter",
                "com.studiogaku.stat-modifier-stack",
                "com.studiogaku.damage-mitigation-evaluator",
                "com.studiogaku.simulation-clock",
                "com.studiogaku.deterministic-random",
                "com.studiogaku.generational-handle",
                "com.studiogaku.input-radial-dead-zone",
                "com.studiogaku.input-command-buffer"
            };

            for (var index = 0; index < retired.Length; index++)
            {
                Assert.That(ModuleCatalog.TryFindEntry(retired[index], out _), Is.False, retired[index]);
            }
        }

        [Test]
        public void ConsolidatedEntries_MapAllFortyFourLegacyPackagesAndFoldersExactly()
        {
            var allLegacyPackages = new HashSet<string>(StringComparer.Ordinal);
            var allLegacyFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AssertLegacyMapping(
                "com.studiogaku.input-assist",
                new[]
                {
                    "com.studiogaku.input-radial-dead-zone",
                    "com.studiogaku.input-vector-response-curve",
                    "com.studiogaku.input-vector-slew-limiter",
                    "com.studiogaku.input-vector-exponential-smoother",
                    "com.studiogaku.input-vector-direction-limiter",
                    "com.studiogaku.input-vector-weighted-mixer",
                    "com.studiogaku.input-direction-quantizer",
                    "com.studiogaku.input-quantizer",
                    "com.studiogaku.input-threshold-classifier",
                    "com.studiogaku.input-press-classifier",
                    "com.studiogaku.input-repeat",
                    "com.studiogaku.input-multi-tap-classifier"
                },
                new[]
                {
                    "InputRadialDeadZone",
                    "InputVectorResponseCurve",
                    "InputVectorSlewLimiter",
                    "InputVectorExponentialSmoother",
                    "InputVectorDirectionLimiter",
                    "InputVectorWeightedMixer",
                    "InputDirectionQuantizer",
                    "InputQuantizer",
                    "InputThresholdClassifier",
                    "InputPressClassifier",
                    "InputRepeat",
                    "InputMultiTapClassifier"
                },
                allLegacyPackages,
                allLegacyFolders);
            AssertLegacyMapping(
                "com.studiogaku.input-command",
                new[]
                {
                    "com.studiogaku.input-command-buffer",
                    "com.studiogaku.input-sequence-matcher",
                    "com.studiogaku.input-chord-matcher",
                    "com.studiogaku.input-command-arbiter",
                    "com.studiogaku.input-axis-conflict-resolver",
                    "com.studiogaku.input-stabilizer"
                },
                new[]
                {
                    "InputCommandBuffer",
                    "InputSequenceMatcher",
                    "InputChordMatcher",
                    "InputCommandArbiter",
                    "InputAxisConflictResolver",
                    "InputStabilizer"
                },
                allLegacyPackages,
                allLegacyFolders);
            AssertLegacyMapping(
                "com.studiogaku.gameplay-rules",
                new[]
                {
                    "com.studiogaku.resource-meter",
                    "com.studiogaku.resource-cost-evaluator",
                    "com.studiogaku.stat-modifier-stack",
                    "com.studiogaku.weighted-choice-table",
                    "com.studiogaku.weighted-integer-allocator",
                    "com.studiogaku.piecewise-linear-curve",
                    "com.studiogaku.rolling-sample-window",
                    "com.studiogaku.sample-statistics",
                    "com.studiogaku.linear-trend-estimator",
                    "com.studiogaku.threshold-tier-table",
                    "com.studiogaku.charge-cooldown",
                    "com.studiogaku.periodic-tick-planner",
                    "com.studiogaku.timed-stack-resolver",
                    "com.studiogaku.stack-transfer-planner",
                    "com.studiogaku.numeric-requirement-evaluator",
                    "com.studiogaku.utility-score-evaluator",
                    "com.studiogaku.stable-score-selector",
                    "com.studiogaku.damage-mitigation-evaluator",
                    "com.studiogaku.threat-score-resolver"
                },
                new[]
                {
                    "ResourceMeter",
                    "ResourceCostEvaluator",
                    "StatModifierStack",
                    "WeightedChoiceTable",
                    "WeightedIntegerAllocator",
                    "PiecewiseLinearCurve",
                    "RollingSampleWindow",
                    "SampleStatistics",
                    "LinearTrendEstimator",
                    "ThresholdTierTable",
                    "ChargeCooldown",
                    "PeriodicTickPlanner",
                    "TimedStackResolver",
                    "StackTransferPlanner",
                    "NumericRequirementEvaluator",
                    "UtilityScoreEvaluator",
                    "StableScoreSelector",
                    "DamageMitigationEvaluator",
                    "ThreatScoreResolver"
                },
                allLegacyPackages,
                allLegacyFolders);
            AssertLegacyMapping(
                "com.studiogaku.deterministic-simulation",
                new[]
                {
                    "com.studiogaku.simulation-clock",
                    "com.studiogaku.deterministic-random",
                    "com.studiogaku.state-fingerprint",
                    "com.studiogaku.replay-tape",
                    "com.studiogaku.canonical-payload",
                    "com.studiogaku.fixed-point",
                    "com.studiogaku.generational-handle"
                },
                new[]
                {
                    "SimulationClock",
                    "DeterministicRandom",
                    "StateFingerprint",
                    "ReplayTape",
                    "CanonicalPayload",
                    "FixedPoint",
                    "GenerationalHandle"
                },
                allLegacyPackages,
                allLegacyFolders);

            Assert.That(allLegacyPackages.Count, Is.EqualTo(44));
            Assert.That(allLegacyFolders.Count, Is.EqualTo(44));
            Assert.That(allLegacyPackages, Does.Contain("com.studiogaku.input-multi-tap-classifier"));
            Assert.That(allLegacyPackages, Does.Contain("com.studiogaku.threat-score-resolver"));
            Assert.That(allLegacyPackages, Does.Contain("com.studiogaku.generational-handle"));
            Assert.That(allLegacyFolders, Does.Contain("InputMultiTapClassifier"));
            Assert.That(allLegacyFolders, Does.Contain("ThreatScoreResolver"));
            Assert.That(allLegacyFolders, Does.Contain("GenerationalHandle"));

            for (var index = 0; index < ModuleCatalog.Entries.Count; index++)
            {
                var entry = ModuleCatalog.Entries[index];
                var consolidated = entry.PackageName == "com.studiogaku.input-assist"
                    || entry.PackageName == "com.studiogaku.input-command"
                    || entry.PackageName == "com.studiogaku.gameplay-rules"
                    || entry.PackageName == "com.studiogaku.deterministic-simulation";
                if (!consolidated)
                {
                    Assert.That(entry.LegacyPackageNames, Is.Empty, entry.PackageName);
                    Assert.That(entry.LegacyFolderNames, Is.Empty, entry.PackageName);
                }
            }
        }

        [Test]
        public void EveryCatalogEntry_IsReachableFromExactlyOneBundle()
        {
            for (var entryIndex = 0; entryIndex < ModuleCatalog.Entries.Count; entryIndex++)
            {
                var packageName = ModuleCatalog.Entries[entryIndex].PackageName;
                var bundleCount = 0;

                for (var bundleIndex = 0; bundleIndex < ModuleCatalog.Bundles.Count; bundleIndex++)
                {
                    var bundle = ModuleCatalog.Bundles[bundleIndex];

                    for (var packageIndex = 0; packageIndex < bundle.PackageNames.Count; packageIndex++)
                    {
                        if (string.Equals(bundle.PackageNames[packageIndex], packageName, System.StringComparison.Ordinal))
                        {
                            bundleCount++;
                        }
                    }
                }

                Assert.That(bundleCount, Is.EqualTo(1), packageName);
            }
        }

        private static void AssertLegacyMapping(
            string packageName,
            string[] expectedPackages,
            string[] expectedFolders,
            ISet<string> allLegacyPackages,
            ISet<string> allLegacyFolders)
        {
            Assert.That(ModuleCatalog.TryFindEntry(packageName, out var entry), Is.True);
            Assert.That(entry.LegacyPackageNames, Is.EqualTo(expectedPackages));
            Assert.That(entry.LegacyFolderNames, Is.EqualTo(expectedFolders));

            for (var index = 0; index < expectedPackages.Length; index++)
            {
                Assert.That(allLegacyPackages.Add(entry.LegacyPackageNames[index]), Is.True, entry.LegacyPackageNames[index]);
                Assert.That(allLegacyFolders.Add(entry.LegacyFolderNames[index]), Is.True, entry.LegacyFolderNames[index]);
            }
        }

        private static bool ContainsJapanese(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character >= '\u3040' && character <= '\u30ff'
                    || character >= '\u3400' && character <= '\u9fff')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
