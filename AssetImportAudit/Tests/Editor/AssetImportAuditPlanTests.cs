using System.Collections.Generic;
using NUnit.Framework;

namespace AssetImportAudit.Editor.Tests
{
    public sealed class AssetImportAuditPlanTests
    {
        [Test]
        public void Plan_PreservesSortedIssueContract()
        {
            var issues = new[]
            {
                new AssetImportAudit.Editor.AssetImportAuditIssue("Assets/B.png", "mipmapEnabled", "True", "False"),
                new AssetImportAudit.Editor.AssetImportAuditIssue("Assets/A.png", "sRGBTexture", "False", "True")
            };
            var entries = new[]
            {
                new AssetImportAudit.Editor.AssetImportAuditPlanEntry("Assets/A.png", "snapshot-a"),
                new AssetImportAudit.Editor.AssetImportAuditPlanEntry("Assets/B.png", "snapshot-b")
            };
            var plan = new AssetImportAudit.Editor.AssetImportAuditPlan("Assets", AssetImportAudit.Editor.AssetImportAuditTextureSettings.Default, issues, entries);

            Assert.That(plan.RootFolder, Is.EqualTo("Assets"));
            Assert.That(plan.IsEmpty, Is.False);
            Assert.That(plan.Issues[0].AssetPath, Is.EqualTo("Assets/B.png"));
            Assert.That(plan.Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void Plan_CopiesMutableInputCollections()
        {
            var issues = new List<AssetImportAudit.Editor.AssetImportAuditIssue>
            {
                new AssetImportAudit.Editor.AssetImportAuditIssue("Assets/A.png", "mipmapEnabled", "True", "False")
            };
            var entries = new List<AssetImportAudit.Editor.AssetImportAuditPlanEntry>
            {
                new AssetImportAudit.Editor.AssetImportAuditPlanEntry("Assets/A.png", "snapshot-a")
            };
            var plan = new AssetImportAudit.Editor.AssetImportAuditPlan("Assets", AssetImportAudit.Editor.AssetImportAuditTextureSettings.Default, issues, entries);

            issues.Clear();
            entries.Clear();

            Assert.That(plan.Issues.Count, Is.EqualTo(1));
            Assert.That(plan.Entries.Count, Is.EqualTo(1));
            Assert.That(plan.IsEmpty, Is.False);
        }

        [Test]
        public void PlanEntry_CopiesMutablePlatformCollection()
        {
            var platforms = new List<AssetImportAudit.Editor.AssetImportAuditTexturePlatform>
            {
                AssetImportAudit.Editor.AssetImportAuditTexturePlatform.Android
            };
            var entry = new AssetImportAudit.Editor.AssetImportAuditPlanEntry("Assets/A.png", "snapshot-a", platforms);

            platforms.Clear();

            Assert.That(entry.Platforms.Count, Is.EqualTo(1));
            Assert.That(entry.Platform, Is.EqualTo(AssetImportAudit.Editor.AssetImportAuditTexturePlatform.Android));
        }
    }
}
