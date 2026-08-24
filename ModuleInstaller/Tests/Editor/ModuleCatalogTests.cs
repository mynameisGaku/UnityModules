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
                Assert.That(entry.ReadmeUrl, Is.EqualTo($"https://github.com/mynameisGaku/UnityModules/blob/{entry.Tag}/{entry.FolderName}/README.md"));
                Assert.That(entry.Version, Is.Not.Empty);
                Assert.That(entry.Tag, Does.EndWith($"-v{entry.Version}"));
                Assert.That(entry.Tag, Does.Match(@"-v\d+\.\d+\.\d+$"));
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
                Assert.That(bundle.ChangeScope, Does.Contain("Installation"), bundle.Id);
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
            Assert.That(inputAssist.DisplayName, Is.EqualTo("Input Assist"));
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.input-command", out var inputCommand), Is.True);
            Assert.That(inputCommand.DisplayName, Is.EqualTo("Input Command"));
            Assert.That(inputCommand.Summary, Does.Contain("tick-based sequence").And.Contain("stateless priority selection").And.Contain("sample-based stabilization"));
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
            Assert.That(bundle.Summary, Does.Contain("Play Mode tuning").And.Contain("saved Scenes"));
            Assert.That(bundle.FirstStep, Does.Contain("Tools > Scene Workspace > Open").And.Contain("Tools > Play Mode Tuning > Open").And.Contain("capture manually during Play").And.Contain("Preview After Play"));
            Assert.That(bundle.ChangeScope, Does.Contain("Installation changes Packages only").And.Contain("Scene order").And.Contain("never saves or discards Scene changes").And.Contain("non-stale reviewed plan").And.Contain("marks the Scene dirty without saving").And.Contain("rollback outcomes separately"));
        }

        [Test]
        public void ProjectSetup_UsesVersionControlAndAssemblyDefinitionCapableRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.project-setup", out var entry), Is.True);
            Assert.That(entry.Tag, Is.EqualTo("project-setup-v1.15.0"));
            Assert.That(entry.Summary, Does.Contain("project folders").And.Contain("test assembly definitions").And.Contain("version control files").And.Contain("managed stripping levels").And.Contain("code generation defaults").And.Contain("duplicate naming defaults").And.Contain("scripting define symbols").And.Contain("Tags").And.Contain("Layers").And.Contain("Build Scenes").And.Contain("Play Mode Start Scene"));
        }

        [Test]
        public void AssetImportAudit_UsesPlatformTextureAuditRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.asset-import-audit", out var entry), Is.True);
            Assert.That(entry.FolderName, Is.EqualTo("AssetImportAudit"));
            Assert.That(entry.Tag, Is.EqualTo("asset-import-audit-v1.1.0"));
            Assert.That(entry.DisplayName, Is.EqualTo("Texture Import Settings"));
            Assert.That(entry.Summary, Does.Contain("shared").And.Contain("Standalone").And.Contain("Android").And.Contain("iOS").And.Contain("reviewed preview"));
        }

        [Test]
        public void BuildAssistant_UsesReviewedDesktopBuildRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.build-assistant", out var entry), Is.True);
            Assert.That(entry.FolderName, Is.EqualTo("BuildAssistant"));
            Assert.That(entry.Tag, Is.EqualTo("build-assistant-v1.0.0"));
            Assert.That(entry.DisplayName, Is.EqualTo("Build Assistant"));
            Assert.That(entry.Summary, Does.Contain("reviewed desktop standalone builds").And.Contain("new output folders").And.Contain("bounded history").And.Contain("size changes").And.Contain("JSON reports"));
        }

        [Test]
        public void SceneWorkspace_UsesReviewedMultiSceneEditorRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.scene-workspace", out var entry), Is.True);
            Assert.That(entry.FolderName, Is.EqualTo("SceneWorkspace"));
            Assert.That(entry.Tag, Is.EqualTo("scene-workspace-v1.0.0"));
            Assert.That(entry.DisplayName, Is.EqualTo("Scene Workspace"));
            Assert.That(entry.Summary, Does.Contain("ordered multi-scene editor workspaces").And.Contain("stale-plan checks").And.Contain("post-verification").And.Contain("rollback reporting"));
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
    }
}
