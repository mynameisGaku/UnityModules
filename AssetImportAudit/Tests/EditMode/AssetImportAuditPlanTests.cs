using NUnit.Framework;

namespace AssetImportAudit.Tests
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
    }
}
