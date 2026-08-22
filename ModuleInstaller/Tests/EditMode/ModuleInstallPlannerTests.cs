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
    }
}
