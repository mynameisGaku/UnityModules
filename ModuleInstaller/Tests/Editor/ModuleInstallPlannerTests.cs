// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ModuleInstaller.Editor.Tests
{
    internal sealed class ModuleInstallPlannerTests
    {
        [Test]
        public void Build_SkipsInstalledAndDeduplicatesSelection()
        {
            var plan = ModuleInstallPlanner.Build(
                new[]
                {
                    "com.studiogaku.build-guard",
                    "com.studiogaku.reference-finder",
                    "com.studiogaku.build-guard"
                },
                new HashSet<string>(StringComparer.Ordinal) { "com.studiogaku.build-guard" },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.True);
            Assert.That(plan.InstalledCount, Is.EqualTo(1));
            Assert.That(plan.Entries.Count, Is.EqualTo(1));
            Assert.That(plan.Entries[0].PackageName, Is.EqualTo("com.studiogaku.reference-finder"));
            Assert.That(plan.Issues, Is.Empty);
        }

        [Test]
        public void Build_BlocksAssetCopyConflictBeforePackageMutation()
        {
            var plan = ModuleInstallPlanner.Build(
                new[] { "com.studiogaku.build-guard" },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BuildGuard" });

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.Entries, Is.Empty);
            Assert.That(plan.Issues.Count, Is.EqualTo(1));
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.AssetCopyConflict));
            Assert.That(plan.Issues[0].Message, Does.Contain("Assets/Modules/BuildGuard"));
        }

        [Test]
        public void Build_ReportsUnknownPackageWithoutChangingKnownOrder()
        {
            var plan = ModuleInstallPlanner.Build(
                new[]
                {
                    "com.studiogaku.scene-flow",
                    "com.example.unknown",
                    "com.studiogaku.time-control"
                },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.Entries.Count, Is.EqualTo(2));
            Assert.That(plan.Entries[0].PackageName, Is.EqualTo("com.studiogaku.scene-flow"));
            Assert.That(plan.Entries[1].PackageName, Is.EqualTo("com.studiogaku.time-control"));
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.UnknownPackage));
        }

        [Test]
        public void BuildUpdates_SelectsOnlyInstalledPackagesBelowPinnedVersion()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[]
                {
                    "com.studiogaku.project-setup",
                    "com.studiogaku.scene-flow",
                    "com.studiogaku.time-control"
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.project-setup"] = "1.0.0",
                    ["com.studiogaku.scene-flow"] = "1.0.0"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.True);
            Assert.That(plan.Entries.Count, Is.EqualTo(1));
            Assert.That(plan.Entries[0].PackageName, Is.EqualTo("com.studiogaku.project-setup"));
            Assert.That(plan.Entries[0].Version, Is.EqualTo("1.15.0"));
            Assert.That(plan.InstalledCount, Is.EqualTo(1));
            Assert.That(plan.Issues, Is.Empty);
        }

        [Test]
        public void BuildUpdates_SelectsInstalledPrereleaseBelowPinnedStableVersion()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.project-setup" },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.project-setup"] = "1.15.0-preview.1"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.True);
            Assert.That(plan.Entries.Count, Is.EqualTo(1));
            Assert.That(plan.Entries[0].PackageName, Is.EqualTo("com.studiogaku.project-setup"));
            Assert.That(plan.InstalledCount, Is.Zero);
            Assert.That(plan.Issues, Is.Empty);
        }

        [TestCase("1.14.0-preview.1", "1.15.0", true)]
        [TestCase("1.15.0-preview.1", "1.15.0", true)]
        [TestCase("1.15.0-preview.2", "1.15.0-preview.10", true)]
        [TestCase("1.15.0-preview.10", "1.15.0-preview.2", false)]
        [TestCase("1.15.0+build.7", "1.15.0", false)]
        [TestCase("1.16.0-preview.1", "1.15.0", false)]
        [TestCase("custom", "1.15.0", false)]
        public void IsUpdateRequired_UsesSemanticVersionPrecedence(
            string installedVersion,
            string targetVersion,
            bool expected)
        {
            Assert.That(ModuleInstallPlanner.IsUpdateRequired(installedVersion, targetVersion), Is.EqualTo(expected));
        }

        [Test]
        public void BuildUpdates_ReportsUnknownPackageWithoutSelectingMissingPackages()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.example.unknown", "com.studiogaku.time-control" },
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.Entries, Is.Empty);
            Assert.That(plan.Issues.Count, Is.EqualTo(1));
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.UnknownPackage));
        }

        [Test]
        public void BuildUpdates_DoesNotDowngradeNewerOrCustomVersions()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.project-setup", "com.studiogaku.scene-flow" },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.project-setup"] = "2.0.0",
                    ["com.studiogaku.scene-flow"] = "custom"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.Entries, Is.Empty);
            Assert.That(plan.InstalledCount, Is.EqualTo(2));
            Assert.That(plan.Issues, Is.Empty);
        }

        [Test]
        public void Build_BlocksLegacyPackageAndAssetCopiesBeforeMutation()
        {
            var plan = ModuleInstallPlanner.Build(
                new[] { "com.studiogaku.input-command" },
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "com.studiogaku.input-command-buffer"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "InputStabilizer"
                });

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.Entries, Is.Empty);
            Assert.That(plan.Issues.Count, Is.EqualTo(1));
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.LegacyModuleConflict));
            Assert.That(plan.Issues[0].Value, Is.EqualTo("com.studiogaku.input-command"));
            Assert.That(plan.Issues[0].Message, Does.Contain("com.studiogaku.input-command-buffer"));
            Assert.That(plan.Issues[0].Message, Does.Contain("Assets/Modules/InputStabilizer"));
            Assert.That(plan.Issues[0].Message, Does.Contain("旧モジュールを自動削除しません"));
        }

        [Test]
        public void Build_BlocksLegacyConflictEvenWhenConsolidatedPackageIsInstalled()
        {
            var plan = ModuleInstallPlanner.Build(
                new[] { "com.studiogaku.input-assist" },
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "com.studiogaku.input-assist",
                    "com.studiogaku.input-repeat"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.InstalledCount, Is.Zero);
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.LegacyModuleConflict));
        }

        [Test]
        public void BuildUpdates_BlocksInputAssistUpgradeWhileLegacyPackageRemains()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.input-assist" },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.input-assist"] = "1.0.0",
                    ["com.studiogaku.input-direction-quantizer"] = "1.0.0"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.Entries, Is.Empty);
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.LegacyModuleConflict));
            Assert.That(plan.Issues[0].Message, Does.Contain("com.studiogaku.input-direction-quantizer"));
        }

        [Test]
        public void BuildUpdates_BlocksConsolidatedUpgradeWhenLegacyAssetCopyRemains()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.input-assist" },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.input-assist"] = "1.0.0"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "InputMultiTapClassifier"
                });

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.Entries, Is.Empty);
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.LegacyModuleConflict));
            Assert.That(plan.Issues[0].Message, Does.Contain("Assets/Modules/InputMultiTapClassifier"));
        }

        [Test]
        public void BuildUpdates_BlocksTargetAssetCopyBeforePackageMutation()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.input-assist" },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.input-assist"] = "1.0.0"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "InputAssist"
                });

            Assert.That(plan.CanStart, Is.False);
            Assert.That(plan.Entries, Is.Empty);
            Assert.That(plan.Issues[0].Kind, Is.EqualTo(ModuleInstallIssueKind.AssetCopyConflict));
            Assert.That(plan.Issues[0].Message, Does.Contain("Assets/Modules/InputAssist"));
        }

        [Test]
        public void BuildUpdates_AllowsConsolidatedUpgradeWithoutLegacyModules()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.input-assist" },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.input-assist"] = "1.0.0"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.That(plan.CanStart, Is.True);
            Assert.That(plan.Entries.Count, Is.EqualTo(1));
            Assert.That(plan.Entries[0].Version, Is.EqualTo("2.0.0"));
            Assert.That(plan.Issues, Is.Empty);
        }

        [Test]
        public void BuildUpdates_IgnoresLegacyModulesWhenConsolidatedTargetIsNotInstalled()
        {
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[]
                {
                    "com.studiogaku.input-assist",
                    "com.studiogaku.project-setup"
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["com.studiogaku.input-repeat"] = "1.0.0",
                    ["com.studiogaku.project-setup"] = "1.0.0"
                },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "InputMultiTapClassifier"
                });

            Assert.That(plan.CanStart, Is.True);
            Assert.That(plan.Entries.Count, Is.EqualTo(1));
            Assert.That(plan.Entries[0].PackageName, Is.EqualTo("com.studiogaku.project-setup"));
            Assert.That(plan.Issues, Is.Empty);
        }
    }
}
