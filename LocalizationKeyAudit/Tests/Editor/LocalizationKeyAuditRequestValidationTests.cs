using System;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// raw/typed source より前に request と coverage を fail-closed で検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditRequestValidationTests
    {
        /// <summary>
        /// required Locale、scope、recognized reference が明示された request を受理します。
        /// </summary>
        [Test]
        public void TryValidateRequest_AcceptsExplicitValidRequest()
        {
            var reference = CreateReference("Assets/Scenes/Main.unity");
            var request = CreateRequest(
                new[] { "en", "ja-JP" },
                new[] { "Assets/Scenes" },
                new[] { reference },
                true,
                string.Empty);

            var succeeded = AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var failure);

            Assert.That(succeeded, Is.True);
            Assert.That(failure, Is.Null);
        }

        /// <summary>
        /// 同一packageのroot、nested folder、direct fileを同じrequestで受理します。
        /// </summary>
        [Test]
        public void TryValidateRequest_AcceptsSamePackageDeclaredScopes()
        {
            var request = CreateRequest(
                declaredPaths: new[]
                {
                    "Packages/com.example/Runtime/Direct.asset",
                    "Packages/com.example/Runtime",
                    "Packages/com.example"
                },
                references: new[]
                {
                    CreateReference("Packages/com.example/Runtime/Nested.prefab", entryId: 30),
                    CreateReference("Packages/com.example/Runtime/Direct.asset", entryId: 20),
                    CreateReference("Packages/com.example/Root.prefab", entryId: 10)
                });

            var succeeded = AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var failure);

            Assert.That(succeeded, Is.True, failure?.Message);
            Assert.That(failure, Is.Null);
        }

        /// <summary>Assetsとpackage、または異なるpackageの混在をInvalidConfigurationにします。</summary>
        [TestCase("Assets", "Packages/com.example")]
        [TestCase("Packages/com.alpha", "Packages/com.beta")]
        public void TryValidateRequest_RejectsMixedLogicalRoots(
            string firstDeclaredPath,
            string secondDeclaredPath)
        {
            AssertInvalid(
                CreateRequest(declaredPaths: new[] { firstDeclaredPath, secondDeclaredPath }),
                "logical root");
        }

        /// <summary>
        /// null request、null coverage、空 required Locale を InvalidConfiguration にします。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsMissingRequestCoverageAndLocales()
        {
            AssertInvalid(null, "明示してください");
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(new[] { "en" }, null),
                "coverage");
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(Array.Empty<string>(), CreateCoverage()),
                "1 件以上");
        }

        /// <summary>
        /// Locale identifier の null、空、前後空白、大小文字違いの重複を拒否します。
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ja")]
        [TestCase("ja ")]
        public void TryValidateRequest_RejectsInvalidLocaleIdentifier(string locale)
        {
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(new[] { locale }, CreateCoverage()),
                "Locale identifier");
        }

        /// <summary>
        /// Locale identifier の重複は大文字小文字を無視し、上限超過も typed 前に拒否します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsDuplicateAndExcessiveLocales()
        {
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(new[] { "ja-JP", "JA-jp" }, CreateCoverage()),
                "重複");
            var tooMany = Enumerable.Range(
                    0,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumRequiredLocales + 1)
                .Select(index => $"locale-{index:D3}")
                .ToArray();
            AssertLimit(
                new AuditEditor.LocalizationKeyAuditRequest(tooMany, CreateCoverage()),
                "required Locale 数");
        }

        /// <summary>
        /// 完了 coverage は理由なし、未完了 coverage は具体的理由ありに限ります。
        /// </summary>
        [Test]
        public void TryValidateRequest_RequiresConsistentCoverageCompletionState()
        {
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(
                    new[] { "en" },
                    CreateCoverage(isComplete: true, incompleteReason: "cancelled")),
                "矛盾");
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(
                    new[] { "en" },
                    CreateCoverage(isComplete: false, incompleteReason: string.Empty)),
                "矛盾");
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(
                    new[] { "en" },
                    CreateCoverage(isComplete: false, incompleteReason: " cancelled")),
                "理由");

            var incomplete = new AuditEditor.LocalizationKeyAuditRequest(
                new[] { "en" },
                CreateCoverage(isComplete: false, incompleteReason: "scan cancelled"));
            Assert.That(
                AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(incomplete, out var failure),
                Is.True,
                failure?.Message);
        }

        /// <summary>
        /// scope 説明の空、前後空白、長さ超過を設定不正として拒否します。
        /// </summary>
        [TestCase("")]
        [TestCase(" Scenes")]
        [TestCase("Scenes ")]
        public void TryValidateRequest_RejectsInvalidScopeDescription(string description)
        {
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                description,
                new[] { "Assets/Scenes" },
                Array.Empty<AuditEditor.LocalizationKeyAuditStaticReference>(),
                true,
                string.Empty);

            AssertInvalid(new AuditEditor.LocalizationKeyAuditRequest(new[] { "en" }, coverage), "scope の説明");
        }

        /// <summary>
        /// declared path の欠落、重複、unsafe segment、区切り文字を拒否します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsInvalidDeclaredPaths()
        {
            AssertInvalid(CreateRequest(declaredPaths: Array.Empty<string>()), "1 件以上");
            AssertInvalid(CreateRequest(declaredPaths: new[] { "Assets/Scenes", "Assets/Scenes" }), "重複");
            AssertInvalid(
                CreateRequest(declaredPaths: new[] { "Packages/com.example", "Packages/com.example" }),
                "重複");
            AssertInvalid(CreateRequest(declaredPaths: new[] { "Assets/../Scenes" }), "不正");
            AssertInvalid(CreateRequest(declaredPaths: new[] { "Assets\\Scenes" }), "不正");
            AssertInvalid(CreateRequest(declaredPaths: new[] { "Packages" }), "不正");
            AssertInvalid(CreateRequest(declaredPaths: new[] { "Library/PackageCache/com.example/A.asset" }), "不正");
            AssertInvalid(CreateRequest(declaredPaths: new[] { "Packages/com.example/../A.asset" }), "不正");
            AssertInvalid(CreateRequest(declaredPaths: new[] { "Packages\\com.example\\A.asset" }), "不正");
        }

        /// <summary>package declared pathの予約文字と末尾dot/space segmentを拒否します。</summary>
        [TestCase("Packages/com.example/Generated~/A.asset")]
        [TestCase("Packages/com.example/Bad:Name/A.asset")]
        [TestCase("Packages/com.example/Trailing./A.asset")]
        [TestCase("Packages/com.example/Trailing /A.asset")]
        public void TryValidateRequest_RejectsUnsafeDeclaredPackageSegments(string declaredPath)
        {
            AssertInvalid(CreateRequest(declaredPaths: new[] { declaredPath }), "不正");
        }

        /// <summary>
        /// recognized reference は宣言 scope 内の non-empty GUID/ID と安全な表示文字列を要求します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsInvalidRecognizedReference()
        {
            AssertInvalid(CreateRequest(references: new[] { CreateReference("Assets/ScenesBackup/Main.unity") }), "static reference");
            AssertInvalid(CreateRequest(references: new[] { CreateReference("Assets/Scenes/Main.unity", Guid.Empty) }), "static reference");
            AssertInvalid(CreateRequest(references: new[] { CreateReference("Assets/Scenes/Main.unity", entryId: 0) }), "static reference");
            AssertInvalid(CreateRequest(references: new AuditEditor.LocalizationKeyAuditStaticReference[] { null }), "static reference");
            AssertInvalid(
                CreateRequest(
                    declaredPaths: new[] { "Packages/com.example" },
                    references: new[] { CreateReference("Packages/com.example") }),
                "static reference");
            AssertInvalid(
                CreateRequest(
                    declaredPaths: new[] { "Packages/com.example" },
                    references: new[] { CreateReference("Packages/com.other/Prefab.prefab") }),
                "static reference");
            AssertInvalid(
                CreateRequest(
                    declaredPaths: new[] { "Packages/com.example" },
                    references: new[] { CreateReference("Packages\\com.example\\Prefab.prefab") }),
                "static reference");
        }

        /// <summary>
        /// declared path 数は内容の重複検査より前に hard limit で拒否します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsDeclaredPathCountAboveLimit()
        {
            var paths = Enumerable.Repeat(
                    "Assets/Scenes",
                    AuditEditor.LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths + 1)
                .ToArray();
            var request = CreateRequest(declaredPaths: paths);

            AssertLimit(request, "declared asset path 数");
        }

        /// <summary>
        /// 同じ source path、collection GUID、entry ID の参照だけを重複として拒否します。
        /// </summary>
        [Test]
        public void TryValidateRequest_DuplicateReferenceUsesStableIdentity()
        {
            var first = CreateReference("Assets/Scenes/Main.unity", collectionName: "Old", entryKey: "OldKey");
            var displayHintChanged = CreateReference(
                "Assets/Scenes/Main.unity",
                collectionName: "New",
                entryKey: "NewKey");
            AssertInvalid(
                CreateRequest(references: new[] { first, displayHintChanged }),
                "複数回");

            var differentSource = CreateReference(
                "Assets/Scenes/Other.unity",
                collectionName: "New",
                entryKey: "NewKey");
            var valid = CreateRequest(references: new[] { first, differentSource });
            Assert.That(
                AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(valid, out var failure),
                Is.True,
                failure?.Message);
        }

        /// <summary>
        /// recognized reference 数は内容の重複検査より前に hard limit で拒否します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsStaticReferenceCountAboveLimit()
        {
            var reference = CreateReference("Assets/Scenes/Main.unity");
            var references = Enumerable.Repeat(
                    reference,
                    AuditEditor.LocalizationKeyAuditLimits.MaximumStaticReferences + 1)
                .ToArray();
            var request = CreateRequest(references: references);

            AssertLimit(request, "static reference 数");
        }

        /// <summary>指定値を持つ最小 valid request を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditRequest CreateRequest(
            string[] locales = null,
            string[] declaredPaths = null,
            AuditEditor.LocalizationKeyAuditStaticReference[] references = null,
            bool isComplete = true,
            string incompleteReason = "")
        {
            return new AuditEditor.LocalizationKeyAuditRequest(
                locales ?? new[] { "en" },
                CreateCoverage(declaredPaths, references, isComplete, incompleteReason));
        }

        /// <summary>指定値を持つ最小 valid coverage を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditCoverage CreateCoverage(
            string[] declaredPaths = null,
            AuditEditor.LocalizationKeyAuditStaticReference[] references = null,
            bool isComplete = true,
            string incompleteReason = "")
        {
            return new AuditEditor.LocalizationKeyAuditCoverage(
                "Scenes",
                declaredPaths ?? new[] { "Assets/Scenes" },
                references ?? Array.Empty<AuditEditor.LocalizationKeyAuditStaticReference>(),
                isComplete,
                incompleteReason);
        }

        /// <summary>指定 identity と表示 hint を持つ recognized reference を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditStaticReference CreateReference(
            string sourcePath,
            Guid? collectionGuid = null,
            long entryId = 10,
            string collectionName = "UI",
            string entryKey = "Start")
        {
            return new AuditEditor.LocalizationKeyAuditStaticReference(
                sourcePath,
                collectionGuid ?? LocalizationKeyAuditTestData.CollectionGuid,
                entryId,
                collectionName,
                entryKey);
        }

        /// <summary>InvalidConfiguration terminal issue の内容を検証します。</summary>
        private static void AssertInvalid(AuditEditor.LocalizationKeyAuditRequest request, string messagePart)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var failure);

            Assert.That(succeeded, Is.False);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Kind, Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
            StringAssert.Contains(messagePart, failure.Message);
        }

        /// <summary>LimitExceeded terminal issue の内容を検証します。</summary>
        private static void AssertLimit(AuditEditor.LocalizationKeyAuditRequest request, string messagePart)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var failure);

            Assert.That(succeeded, Is.False);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Kind, Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded));
            StringAssert.Contains(messagePart, failure.Message);
        }
    }
}
