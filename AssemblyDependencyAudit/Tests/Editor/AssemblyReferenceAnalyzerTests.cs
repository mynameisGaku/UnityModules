using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// asmref target 解決、曖昧性、決定論性、asmdef graph 不変、安全上限を検証します。
    /// </summary>
    internal sealed class AssemblyReferenceAnalyzerTests
    {
        /// <summary>target asmdef に使う有効な32桁16進 GUID です。</summary>
        private const string TargetGuid = "0123456789abcdef0123456789abcdef";

        /// <summary>別 asset に使う有効な32桁16進 GUID です。</summary>
        private const string OtherGuid = "fedcba9876543210fedcba9876543210";

        /// <summary>
        /// name と大小文字違い GUID を解決し、同じ target の複数 asmref を独立した結果として保持します。
        /// </summary>
        [Test]
        public void TryAnalyze_ResolvesNameAndGuidTargetsWithoutChangingAssemblyGraph()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", TargetGuid),
                AssemblyDependencyTestData.CreateSource(
                    "Assets/B.asmdef",
                    "B",
                    OtherGuid,
                    new[] { "A" }));
            var graphBefore = CreateGraphSignature(assemblyResult);
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/FeatureOne/Target.asmref",
                    "11111111111111111111111111111111",
                    "A"),
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/FeatureTwo/Target.asmref",
                    "22222222222222222222222222222222",
                    "guid:0123456789ABCDEF0123456789ABCDEF")
            };

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                sources,
                assemblyResult,
                out var result,
                out var error,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.AssemblyReferences.Select(target => target.AssetPath), Is.EqualTo(new[]
            {
                "Assets/FeatureOne/Target.asmref",
                "Assets/FeatureTwo/Target.asmref"
            }));
            Assert.That(result.AssemblyReferences.Select(target => target.Kind), Is.EqualTo(new[]
            {
                AuditEditor.AssemblyReferenceTargetKind.Name,
                AuditEditor.AssemblyReferenceTargetKind.Guid
            }));
            Assert.That(result.AssemblyReferences.Select(target => target.ResolvedTargetAssetPath),
                Is.EqualTo(new[] { "Assets/A.asmdef", "Assets/A.asmdef" }));
            Assert.That(result.AssemblyReferences.All(target => target.IsResolved), Is.True);
            Assert.That(CreateGraphSignature(result), Is.EqualTo(graphBefore));
        }

        /// <summary>
        /// GUID prefix がある不正値を assembly 名へ fallback せず Guid の未解決として保持します。
        /// </summary>
        [Test]
        public void TryAnalyze_MalformedGuidReferenceDoesNotFallBackToAssemblyName()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource("Assets/Fallback.asmdef", "GUID:short", TargetGuid));
            var source = AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                "Assets/Fallback.asmref",
                OtherGuid,
                "GUID:short");

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { source },
                assemblyResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.AssemblyReferences.Single().Kind, Is.EqualTo(AuditEditor.AssemblyReferenceTargetKind.Guid));
            Assert.That(result.AssemblyReferences.Single().IsResolved, Is.False);
            Assert.That(result.Issues.Single().Kind,
                Is.EqualTo(AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference));
        }

        /// <summary>
        /// JSON 不正、reference 欠落、target 未解決を別の finding と target 状態へ分類します。
        /// </summary>
        [Test]
        public void TryAnalyze_ReportsInvalidMissingAndUnresolvedAssemblyReferences()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource("Assets/Target.asmdef", "Target", TargetGuid));
            var sources = new[]
            {
                new AuditEditor.AssemblyReferenceSource("Assets/A.Invalid.asmref", OtherGuid, "{"),
                new AuditEditor.AssemblyReferenceSource("Assets/B.Missing.asmref", OtherGuid, "{}"),
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/C.Unresolved.asmref",
                    OtherGuid,
                    "MissingTarget")
            };

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                sources,
                assemblyResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Issues.Select(issue => issue.Kind), Is.EqualTo(new[]
            {
                AuditEditor.AssemblyDependencyIssueKind.InvalidAssemblyReferenceJson,
                AuditEditor.AssemblyDependencyIssueKind.MissingAssemblyReference,
                AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference
            }));
            Assert.That(result.AssemblyReferences.Select(target => target.Kind), Is.EqualTo(new[]
            {
                AuditEditor.AssemblyReferenceTargetKind.Unknown,
                AuditEditor.AssemblyReferenceTargetKind.Unknown,
                AuditEditor.AssemblyReferenceTargetKind.Name
            }));
            Assert.That(result.AssemblyReferences.All(target => !target.IsResolved), Is.True);
            Assert.That(result.AssemblyReferences[2].RawReference, Is.EqualTo("MissingTarget"));
        }

        /// <summary>
        /// 重複 asmdef name と GUID は対応する asmref target を Ambiguous とし、edge を追加しません。
        /// </summary>
        [Test]
        public void TryAnalyze_DuplicateAssemblyNameAndGuidProduceAmbiguousTargets()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource(
                    "Assets/DuplicateNameA.asmdef",
                    "duplicate",
                    "11111111111111111111111111111111"),
                AssemblyDependencyTestData.CreateSource(
                    "Assets/DuplicateNameB.asmdef",
                    "Duplicate",
                    "22222222222222222222222222222222"),
                AssemblyDependencyTestData.CreateSource("Assets/DuplicateGuidA.asmdef", "GuidA", TargetGuid),
                AssemblyDependencyTestData.CreateSource(
                    "Assets/DuplicateGuidB.asmdef",
                    "GuidB",
                    TargetGuid.ToUpperInvariant()));
            var graphBefore = CreateGraphSignature(assemblyResult);
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/ByName.asmref",
                    "33333333333333333333333333333333",
                    "DUPLICATE"),
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/ByGuid.asmref",
                    "44444444444444444444444444444444",
                    "GUID:" + TargetGuid)
            };

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                sources,
                assemblyResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Issues.Count(issue =>
                    issue.Kind == AuditEditor.AssemblyDependencyIssueKind.AmbiguousAssemblyReference),
                Is.EqualTo(2));
            Assert.That(result.AssemblyReferences.All(target => !target.IsResolved), Is.True);
            Assert.That(CreateGraphSignature(result), Is.EqualTo(graphBefore));
        }

        /// <summary>
        /// asmdef GUID と任意 asmref 自身の meta GUID が衝突した場合は GUID target だけを曖昧にします。
        /// </summary>
        [Test]
        public void TryAnalyze_AssemblyDefinitionAndAssemblyReferenceGuidCollisionIsAmbiguous()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource("Assets/Target.asmdef", "Target", TargetGuid));
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/A.CollisionOwner.asmref",
                    TargetGuid.ToUpperInvariant(),
                    "Target"),
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/B.GuidConsumer.asmref",
                    OtherGuid,
                    "GUID:" + TargetGuid)
            };

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                sources,
                assemblyResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.AssemblyReferences[0].IsResolved, Is.True, "name 解決まで衝突させません。");
            Assert.That(result.AssemblyReferences[1].Kind, Is.EqualTo(AuditEditor.AssemblyReferenceTargetKind.Guid));
            Assert.That(result.AssemblyReferences[1].IsResolved, Is.False);
            Assert.That(result.Issues.Count(issue =>
                    issue.Kind == AuditEditor.AssemblyDependencyIssueKind.AmbiguousAssemblyReference),
                Is.EqualTo(1));
        }

        /// <summary>
        /// GUIDが一意でもtarget asmdefのJSONまたはnameが無効なら、target path付きの未解決として返します。
        /// </summary>
        [TestCase("Assets/InvalidJson.asmdef", "{")]
        [TestCase("Assets/MissingName.asmdef", "{}")]
        public void TryAnalyze_UniqueGuidTargetMustBeAValidNamedAssemblyDefinition(
            string targetAssetPath,
            string targetJson)
        {
            var assemblyResult = CreateAssemblyResult(
                new AuditEditor.AssemblyDefinitionSource(targetAssetPath, TargetGuid, targetJson));
            var source = AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                "Assets/Target.asmref",
                OtherGuid,
                "GUID:" + TargetGuid);

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { source },
                assemblyResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.AssemblyReferences.Single().ResolvedTargetAssetPath, Is.Empty);
            var issue = result.Issues.Single(candidate =>
                candidate.Kind == AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference);
            Assert.That(issue.RelatedAssetPath, Is.EqualTo(targetAssetPath));
            Assert.That(issue.Reference, Is.EqualTo("GUID:" + TargetGuid));
        }

        /// <summary>
        /// 同一GUIDのinvalid・valid asmdefが併存する場合はcandidate validityより先にAmbiguousとします。
        /// </summary>
        [Test]
        public void TryAnalyze_InvalidAndValidDuplicateGuidTargetsRemainAmbiguous()
        {
            var assemblyResult = CreateAssemblyResult(
                new AuditEditor.AssemblyDefinitionSource("Assets/Invalid.asmdef", TargetGuid, "{"),
                AssemblyDependencyTestData.CreateSource("Assets/Valid.asmdef", "Valid", TargetGuid));
            var source = AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                "Assets/Target.asmref",
                OtherGuid,
                "GUID:" + TargetGuid);

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { source },
                assemblyResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.AssemblyReferences.Single().IsResolved, Is.False);
            var issue = result.Issues.Single(candidate =>
                candidate.Kind == AuditEditor.AssemblyDependencyIssueKind.AmbiguousAssemblyReference);
            Assert.That(issue.RelatedAssetPath, Is.Empty);
            Assert.That(result.Issues.Any(candidate =>
                candidate.Kind == AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference), Is.False);
        }

        /// <summary>
        /// invalid targetのGUIDとasmref自身のmeta GUIDが衝突した場合もvalidityより先にAmbiguousとします。
        /// </summary>
        [Test]
        public void TryAnalyze_InvalidTargetWithCrossKindGuidCollisionRemainsAmbiguous()
        {
            var assemblyResult = CreateAssemblyResult(
                new AuditEditor.AssemblyDefinitionSource("Assets/Invalid.asmdef", TargetGuid, "{"));
            var source = AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                "Assets/Collision.asmref",
                TargetGuid.ToUpperInvariant(),
                "GUID:" + TargetGuid);

            var succeeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { source },
                assemblyResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.AssemblyReferences.Single().IsResolved, Is.False);
            Assert.That(result.Issues.Count(candidate =>
                    candidate.Kind == AuditEditor.AssemblyDependencyIssueKind.AmbiguousAssemblyReference),
                Is.EqualTo(1));
            Assert.That(result.Issues.Any(candidate =>
                candidate.Kind == AuditEditor.AssemblyDependencyIssueKind.UnresolvedAssemblyReference), Is.False);
        }

        /// <summary>
        /// source 入力順を反転しても target と finding の全 field を同じ Ordinal 順で返します。
        /// </summary>
        [Test]
        public void TryAnalyze_IsDeterministicAcrossSourceOrder()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource("Assets/Target.asmdef", "Target", TargetGuid));
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateAssemblyReferenceSource("Assets/Z.asmref", OtherGuid, "Missing"),
                new AuditEditor.AssemblyReferenceSource("Assets/A.asmref", OtherGuid, "{}"),
                AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                    "Assets/M.asmref",
                    "11111111111111111111111111111111",
                    "Target")
            };

            var firstSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                sources,
                assemblyResult,
                out var first,
                out _,
                out var firstError);
            var secondSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                sources.Reverse().ToArray(),
                assemblyResult,
                out var second,
                out _,
                out var secondError);

            Assert.That(firstSucceeded, Is.True, firstError);
            Assert.That(secondSucceeded, Is.True, secondError);
            Assert.That(first.AssemblyReferences.Select(CreateTargetSignature),
                Is.EqualTo(second.AssemblyReferences.Select(CreateTargetSignature)));
            Assert.That(first.Issues.Select(CreateIssueSignature),
                Is.EqualTo(second.Issues.Select(CreateIssueSignature)));
            Assert.That(CreateGraphSignature(first), Is.EqualTo(CreateGraphSignature(second)));
        }

        /// <summary>
        /// null の source 一覧、base result、source item は SourceUnavailable として部分結果を破棄します。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsUnavailableInputsWithoutPartialResult()
        {
            var assemblyResult = CreateAssemblyResult();

            var nullSourcesSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                null,
                assemblyResult,
                out var nullSourcesResult,
                out var nullSourcesError,
                out _);
            var nullResultSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                Array.Empty<AuditEditor.AssemblyReferenceSource>(),
                null,
                out var nullResult,
                out var nullResultError,
                out _);
            var nullItemSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new AuditEditor.AssemblyReferenceSource[] { null },
                assemblyResult,
                out var nullItemResult,
                out var nullItemError,
                out _);

            AssertFailure(
                nullSourcesSucceeded,
                nullSourcesResult,
                nullSourcesError,
                AuditEditor.AssemblyDependencyAuditError.SourceUnavailable);
            AssertFailure(
                nullResultSucceeded,
                nullResult,
                nullResultError,
                AuditEditor.AssemblyDependencyAuditError.SourceUnavailable);
            AssertFailure(
                nullItemSucceeded,
                nullItemResult,
                nullItemError,
                AuditEditor.AssemblyDependencyAuditError.SourceUnavailable);
        }

        /// <summary>
        /// asmref 件数は exactly 10000 件を受理し、一件でも超えたら解析前に拒否します。
        /// </summary>
        [Test]
        public void TryAnalyze_EnforcesAssemblyReferenceCountBoundary()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource("Assets/Target.asmdef", "Target", TargetGuid));
            var source = AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                "Assets/Target.asmref",
                string.Empty,
                "Target");
            var exactSources = Enumerable.Repeat(
                    source,
                    AuditEditor.AssemblyReferenceAnalyzer.MaximumAssemblyReferences)
                .ToArray();
            var excessiveSources = Enumerable.Repeat(
                    source,
                    AuditEditor.AssemblyReferenceAnalyzer.MaximumAssemblyReferences + 1)
                .ToArray();

            var exactSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                exactSources,
                assemblyResult,
                out var exactResult,
                out var exactError,
                out var exactMessage);
            var excessiveSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                excessiveSources,
                assemblyResult,
                out var excessiveResult,
                out var excessiveError,
                out _);

            Assert.That(exactSucceeded, Is.True, exactMessage);
            Assert.That(exactError, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(exactResult.AssemblyReferences,
                Has.Count.EqualTo(AuditEditor.AssemblyReferenceAnalyzer.MaximumAssemblyReferences));
            AssertFailure(
                excessiveSucceeded,
                excessiveResult,
                excessiveError,
                AuditEditor.AssemblyDependencyAuditError.TooManyAssemblyReferences);
        }

        /// <summary>
        /// source 文字数上限 exactly は解析し、一文字超過では SourceTooLarge として拒否します。
        /// </summary>
        [Test]
        public void TryAnalyze_EnforcesSourceCharacterBoundary()
        {
            var assemblyResult = CreateAssemblyResult(
                AssemblyDependencyTestData.CreateSource("Assets/Target.asmdef", "Target", TargetGuid));
            var exactJson = CreateJsonWithLength(AuditEditor.AssemblyDependencyAnalyzer.MaximumSourceCharacters);
            var excessiveJson = CreateJsonWithLength(AuditEditor.AssemblyDependencyAnalyzer.MaximumSourceCharacters + 1);

            var exactSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { new AuditEditor.AssemblyReferenceSource("Assets/Exact.asmref", OtherGuid, exactJson) },
                assemblyResult,
                out var exactResult,
                out _,
                out var exactMessage);
            var excessiveSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { new AuditEditor.AssemblyReferenceSource("Assets/Excessive.asmref", OtherGuid, excessiveJson) },
                assemblyResult,
                out var excessiveResult,
                out var excessiveError,
                out _);

            Assert.That(exactJson, Has.Length.EqualTo(AuditEditor.AssemblyDependencyAnalyzer.MaximumSourceCharacters));
            Assert.That(exactSucceeded, Is.True, exactMessage);
            Assert.That(exactResult.AssemblyReferences.Single().IsResolved, Is.True);
            AssertFailure(
                excessiveSucceeded,
                excessiveResult,
                excessiveError,
                AuditEditor.AssemblyDependencyAuditError.SourceTooLarge);
        }

        /// <summary>
        /// base finding と asmref finding の合計は exactly 上限を受理し、一件超過では全結果を破棄します。
        /// </summary>
        [Test]
        public void TryAnalyze_EnforcesCombinedIssueBoundary()
        {
            var issue = new AuditEditor.AssemblyDependencyIssue(
                AuditEditor.AssemblyDependencyIssueKind.InvalidJson,
                "Assets/Base.asmdef",
                string.Empty,
                string.Empty,
                "base");
            var baseIssues = Enumerable.Repeat(
                    issue,
                    AuditEditor.AssemblyDependencyAnalyzer.MaximumIssues - 1)
                .ToArray();
            var assemblyResult = new AuditEditor.AssemblyDependencyAuditResult(
                Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                baseIssues,
                Array.Empty<IReadOnlyList<int>>(),
                Array.Empty<IReadOnlyList<int>>(),
                Array.Empty<IReadOnlyList<int>>());
            var invalidSource = new AuditEditor.AssemblyReferenceSource("Assets/Invalid.asmref", OtherGuid, "{");

            var exactSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { invalidSource },
                assemblyResult,
                out var exactResult,
                out _,
                out var exactMessage);
            var excessiveSucceeded = AuditEditor.AssemblyReferenceAnalyzer.TryAnalyze(
                new[] { invalidSource, invalidSource },
                assemblyResult,
                out var excessiveResult,
                out var excessiveError,
                out _);

            Assert.That(exactSucceeded, Is.True, exactMessage);
            Assert.That(exactResult.Issues, Has.Count.EqualTo(AuditEditor.AssemblyDependencyAnalyzer.MaximumIssues));
            AssertFailure(
                excessiveSucceeded,
                excessiveResult,
                excessiveError,
                AuditEditor.AssemblyDependencyAuditError.TooManyIssues);
        }

        /// <summary>asmdef analyzer から完全な base result を作ります。</summary>
        private static AuditEditor.AssemblyDependencyAuditResult CreateAssemblyResult(
            params AuditEditor.AssemblyDefinitionSource[] sources)
        {
            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            return result;
        }

        /// <summary>graph 三一覧を順序込みで比較できる文字列へ変換します。</summary>
        private static string CreateGraphSignature(AuditEditor.AssemblyDependencyAuditResult result)
        {
            return CreateNestedSignature(result.Dependencies) + "/" +
                CreateNestedSignature(result.Dependents) + "/" +
                CreateNestedSignature(result.Cycles);
        }

        /// <summary>二次元 index 一覧を区切り付き文字列へ変換します。</summary>
        private static string CreateNestedSignature(IReadOnlyList<IReadOnlyList<int>> values)
        {
            return string.Join("|", values.Select(value => string.Join(",", value)));
        }

        /// <summary>target の全 field を決定論比較用文字列へ変換します。</summary>
        private static string CreateTargetSignature(AuditEditor.AssemblyReferenceTarget target)
        {
            return $"{target.AssetPath}|{target.RawReference}|{target.Kind}|{target.ResolvedTargetAssetPath}";
        }

        /// <summary>finding の全 field を決定論比較用文字列へ変換します。</summary>
        private static string CreateIssueSignature(AuditEditor.AssemblyDependencyIssue issue)
        {
            return $"{issue.AssetPath}|{issue.Kind}|{issue.RelatedAssetPath}|{issue.Reference}|{issue.Message}";
        }

        /// <summary>reference と padding を持つ正当な JSON を指定文字数 exactly で作ります。</summary>
        private static string CreateJsonWithLength(int length)
        {
            const string prefix = "{\"reference\":\"Target\",\"padding\":\"";
            const string suffix = "\"}";
            return prefix + new string('x', length - prefix.Length - suffix.Length) + suffix;
        }

        /// <summary>失敗、null result、typed error を同時に確認します。</summary>
        private static void AssertFailure(
            bool succeeded,
            AuditEditor.AssemblyDependencyAuditResult result,
            AuditEditor.AssemblyDependencyAuditError error,
            AuditEditor.AssemblyDependencyAuditError expectedError)
        {
            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo(expectedError));
        }
    }
}
