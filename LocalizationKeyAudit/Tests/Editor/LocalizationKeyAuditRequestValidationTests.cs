using System;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 未加工取得元と型として読み取る取得元より前に、要求と網羅情報を安全側で失敗するよう検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditRequestValidationTests
    {
        /// <summary>
        /// 必須ロケール、範囲、認識済み参照が明示された要求を受理します。
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
        /// 同じパッケージのルート、入れ子フォルダー、直接指定ファイルを同じ要求で受理します。
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

        /// <summary>Assetsとパッケージ、または異なるパッケージの混在をInvalidConfigurationにします。</summary>
        [TestCase("Assets", "Packages/com.example")]
        [TestCase("Packages/com.alpha", "Packages/com.beta")]
        public void TryValidateRequest_RejectsMixedLogicalRoots(
            string firstDeclaredPath,
            string secondDeclaredPath)
        {
            AssertInvalid(
                CreateRequest(declaredPaths: new[] { firstDeclaredPath, secondDeclaredPath }),
                "論理ルート");
        }

        /// <summary>
        /// 参照なしの要求、参照なしの網羅情報、空の必須ロケールをInvalidConfigurationにします。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsMissingRequestCoverageAndLocales()
        {
            AssertInvalidExact(null, "必須ロケールと静的参照網羅情報を指定してください。");
            AssertInvalidExact(
                new AuditEditor.LocalizationKeyAuditRequest(new[] { "en" }, null),
                "必須ロケールと静的参照網羅情報を指定してください。");
            AssertInvalidExact(
                new AuditEditor.LocalizationKeyAuditRequest(Array.Empty<string>(), CreateCoverage()),
                "必須ロケールを1件以上指定してください。");
        }

        /// <summary>
        /// ロケール識別子の参照なし、空、前後空白、大小文字違いの重複を拒否します。
        /// </summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ja")]
        [TestCase("ja ")]
        public void TryValidateRequest_RejectsInvalidLocaleIdentifier(string locale)
        {
            AssertInvalid(
                new AuditEditor.LocalizationKeyAuditRequest(new[] { locale }, CreateCoverage()),
                "ロケール識別子");
        }

        /// <summary>
        /// ロケール識別子の重複は大文字小文字を無視し、上限超過も型付き取得前に拒否します。
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
                "必須ロケール数");
        }

        /// <summary>
        /// 網羅完了は理由なし、網羅未完了は具体的な理由ありに限ります。
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
        /// 範囲説明の空、前後空白、長さ超過を設定不正として拒否します。
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

            AssertInvalid(new AuditEditor.LocalizationKeyAuditRequest(new[] { "en" }, coverage), "走査範囲を説明");
        }

        /// <summary>
        /// 宣言パスの欠落、重複、安全でない区切り要素、区切り文字を拒否します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsInvalidDeclaredPaths()
        {
            AssertInvalid(CreateRequest(declaredPaths: Array.Empty<string>()), "1件以上");
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

        /// <summary>パッケージ宣言パスの予約文字と、末尾がドットまたは空白の区切り要素を拒否します。</summary>
        [TestCase("Packages/com.example/Generated~/A.asset")]
        [TestCase("Packages/com.example/Bad:Name/A.asset")]
        [TestCase("Packages/com.example/Trailing./A.asset")]
        [TestCase("Packages/com.example/Trailing /A.asset")]
        public void TryValidateRequest_RejectsUnsafeDeclaredPackageSegments(string declaredPath)
        {
            AssertInvalid(CreateRequest(declaredPaths: new[] { declaredPath }), "不正");
        }

        /// <summary>
        /// 認識済み参照には、宣言範囲内の空でないGUID／IDと安全な表示文字列を要求します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsInvalidRecognizedReference()
        {
            AssertInvalid(CreateRequest(references: new[] { CreateReference("Assets/ScenesBackup/Main.unity") }), "静的参照");
            AssertInvalid(CreateRequest(references: new[] { CreateReference("Assets/Scenes/Main.unity", Guid.Empty) }), "静的参照");
            AssertInvalid(CreateRequest(references: new[] { CreateReference("Assets/Scenes/Main.unity", entryId: 0) }), "静的参照");
            AssertInvalid(CreateRequest(references: new AuditEditor.LocalizationKeyAuditStaticReference[] { null }), "静的参照");
            AssertInvalid(
                CreateRequest(
                    declaredPaths: new[] { "Packages/com.example" },
                    references: new[] { CreateReference("Packages/com.example") }),
                "静的参照");
            AssertInvalid(
                CreateRequest(
                    declaredPaths: new[] { "Packages/com.example" },
                    references: new[] { CreateReference("Packages/com.other/Prefab.prefab") }),
                "静的参照");
            AssertInvalid(
                CreateRequest(
                    declaredPaths: new[] { "Packages/com.example" },
                    references: new[] { CreateReference("Packages\\com.example\\Prefab.prefab") }),
                "静的参照");
        }

        /// <summary>
        /// 宣言パス数は、内容の重複検査より前に固定上限で拒否します。
        /// </summary>
        [Test]
        public void TryValidateRequest_RejectsDeclaredPathCountAboveLimit()
        {
            var paths = Enumerable.Repeat(
                    "Assets/Scenes",
                    AuditEditor.LocalizationKeyAuditLimits.MaximumDeclaredAssetPaths + 1)
                .ToArray();
            var request = CreateRequest(declaredPaths: paths);

            AssertLimit(request, "宣言済みアセットパス数");
        }

        /// <summary>
        /// 同じ取得元パス、コレクション識別子（GUID）、項目識別子の参照だけを重複として拒否します。
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
        /// 認識済み参照数は、内容の重複検査より前に固定上限で拒否します。
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

            AssertLimit(request, "静的参照数");
        }

        /// <summary>指定値を持つ最小の正常な要求を作ります。</summary>
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

        /// <summary>指定値を持つ最小の正常な網羅情報を作ります。</summary>
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

        /// <summary>指定した識別情報と表示補助を持つ認識済み参照を作ります。</summary>
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

        /// <summary>InvalidConfigurationの監査停止問題の内容を検証します。</summary>
        private static void AssertInvalid(AuditEditor.LocalizationKeyAuditRequest request, string messagePart)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var failure);

            Assert.That(succeeded, Is.False);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Kind, Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
            StringAssert.Contains(messagePart, failure.Message);
        }

        /// <summary>設定不正を表す監査停止問題の本文を完全一致で検証します。</summary>
        private static void AssertInvalidExact(AuditEditor.LocalizationKeyAuditRequest request, string expectedMessage)
        {
            var succeeded = AuditEditor.LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var failure);

            Assert.That(succeeded, Is.False);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Kind, Is.EqualTo(AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration));
            Assert.That(failure.Message, Is.EqualTo(expectedMessage));
        }

        /// <summary>LimitExceededの監査停止問題の内容を検証します。</summary>
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
