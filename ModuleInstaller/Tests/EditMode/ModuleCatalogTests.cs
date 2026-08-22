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
            Assert.That(ModuleCatalog.Entries.Count, Is.EqualTo(40));
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

            for (var bundleIndex = 0; bundleIndex < ModuleCatalog.Bundles.Count; bundleIndex++)
            {
                var bundle = ModuleCatalog.Bundles[bundleIndex];
                Assert.That(bundleIds.Add(bundle.Id), Is.True, bundle.Id);
                Assert.That(bundle.PackageNames.Count, Is.GreaterThan(0));
                var packageNames = new HashSet<string>(StringComparer.Ordinal);
                for (var packageIndex = 0; packageIndex < bundle.PackageNames.Count; packageIndex++)
                {
                    var packageName = bundle.PackageNames[packageIndex];
                    Assert.That(packageNames.Add(packageName), Is.True, packageName);
                    Assert.That(ModuleCatalog.TryFindEntry(packageName, out _), Is.True, packageName);
                }
            }
        }

        [Test]
        public void RecommendedInputBundle_DoesNotExposeLegacyMicroPackages()
        {
            Assert.That(ModuleCatalog.TryFindBundle("input-support", out var bundle), Is.True);
            Assert.That(bundle.PackageNames, Is.EquivalentTo(new[]
            {
                "com.studiogaku.input-assist",
                "com.studiogaku.input-gate"
            }));
        }

        [Test]
        public void ProjectMaintenanceBundle_IncludesSetupAndRepairWorkflows()
        {
            Assert.That(ModuleCatalog.TryFindBundle("project-maintenance", out var bundle), Is.True);
            Assert.That(bundle.PackageNames, Is.EquivalentTo(new[]
            {
                "com.studiogaku.project-setup",
                "com.studiogaku.inspector",
                "com.studiogaku.drawing",
                "com.studiogaku.build-guard",
                "com.studiogaku.reference-finder"
            }));
        }

        [Test]
        public void ProjectSetup_UsesPlayModeStartSceneCapableRelease()
        {
            Assert.That(ModuleCatalog.TryFindEntry("com.studiogaku.project-setup", out var entry), Is.True);
            Assert.That(entry.Tag, Is.EqualTo("project-setup-v1.4.0"));
            Assert.That(entry.Summary, Does.Contain("scripting define symbols").And.Contain("Tags").And.Contain("Layers").And.Contain("Build Scenes").And.Contain("Play Mode Start Scene"));
        }
    }
}
