using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// asmdef 解析、参照解決、graph 構築、診断、安全上限を検証します。
    /// </summary>
    internal sealed class AssemblyDependencyAnalyzerTests
    {
        /// <summary>
        /// assembly 名、GUID、compiler path の三経路を同じ参照先へ解決します。
        /// </summary>
        [Test]
        public void TryAnalyze_ResolvesNameGuidAndCompilerPathReferences()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter();
            adapter.ReferencePaths.Add("CompilerAlias", "Assets\\Alpha.asmdef");
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateSource(
                    "Assets/Consumer.asmdef",
                    "Consumer",
                    "guid-consumer",
                    new[] { "Alpha", "GUID:GUID-ALPHA", "CompilerAlias" }),
                AssemblyDependencyTestData.CreateSource("Assets/Alpha.asmdef", "Alpha", "guid-alpha")
            };

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                adapter,
                out var result,
                out var error,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result.Assemblies.Select(node => node.Name), Is.EqualTo(new[] { "Alpha", "Consumer" }));
            Assert.That(result.Assemblies[1].References.Select(reference => reference.ResolvedAssemblyIndex), Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(result.Assemblies[1].References.Select(reference => reference.Kind), Is.EqualTo(new[]
            {
                AuditEditor.AssemblyDependencyReferenceKind.Name,
                AuditEditor.AssemblyDependencyReferenceKind.Guid,
                AuditEditor.AssemblyDependencyReferenceKind.Name
            }));
            Assert.That(result.Dependencies[1], Is.EqualTo(new[] { 0 }));
            Assert.That(adapter.ResolveCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 重複 name/GUID は曖昧参照として扱い、未解決と自己参照も区別します。
        /// </summary>
        [Test]
        public void TryAnalyze_ReportsDuplicateAmbiguousUnresolvedAndSelfReferences()
        {
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateSource("Assets/DupA.asmdef", "Duplicate", "guid-a"),
                AssemblyDependencyTestData.CreateSource("Assets/DupB.asmdef", "Duplicate", "guid-b"),
                AssemblyDependencyTestData.CreateSource("Assets/GuidA.asmdef", "GuidA", "same-guid"),
                AssemblyDependencyTestData.CreateSource("Assets/GuidB.asmdef", "GuidB", "SAME-GUID"),
                AssemblyDependencyTestData.CreateSource(
                    "Assets/Consumer.asmdef",
                    "Consumer",
                    "guid-consumer",
                    new[] { "Duplicate", "GUID:same-guid", "Missing" }),
                AssemblyDependencyTestData.CreateSource("Assets/Self.asmdef", "Self", "guid-self", new[] { "Self" })
            };

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.DuplicateName, 2);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.DuplicateGuid, 2);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.AmbiguousReference, 2);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.UnresolvedReference, 1);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.SelfReference, 1);
            Assert.That(result.Cycles, Is.Empty, "単独の自己参照は複数 assembly の SCC ではありません。");
        }

        /// <summary>
        /// 無効 JSON と空の assembly 名を別々の問題として保持します。
        /// </summary>
        [Test]
        public void TryAnalyze_ReportsInvalidJsonAndMissingName()
        {
            var sources = new[]
            {
                new AuditEditor.AssemblyDefinitionSource("Assets/Invalid.asmdef", "guid-invalid", "not json"),
                new AuditEditor.AssemblyDefinitionSource("Assets/Missing.asmdef", "guid-missing", "{}")
            };

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.InvalidJson, 1);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.MissingName, 1);
            Assert.That(result.Assemblies.All(node => string.IsNullOrEmpty(node.Name)), Is.True);
            Assert.That(result.Assemblies.Single(node => node.AssetPath.EndsWith("Invalid.asmdef", StringComparison.Ordinal)).IsJsonValid, Is.False);
        }

        /// <summary>
        /// platform 矛盾、参照形式混在、Player から Editor 専用への edge を報告します。
        /// </summary>
        [Test]
        public void TryAnalyze_ReportsPlatformAndReferenceKindIssues()
        {
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateSource(
                    "Assets/EditorOnly.asmdef",
                    "EditorOnly",
                    "guid-editor",
                    includePlatforms: new[] { "Editor" }),
                AssemblyDependencyTestData.CreateSource(
                    "Assets/Player.asmdef",
                    "Player",
                    "guid-player",
                    new[] { "EditorOnly", "GUID:guid-editor" },
                    new[] { "Standalone" },
                    new[] { "WebGL" })
            };

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.MixedReferenceKinds, 1);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.IncludeAndExcludePlatforms, 1);
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.PlayerAssemblyReferencesEditorOnly, 1);
            Assert.That(result.Dependencies[1], Is.EqualTo(new[] { 0 }), "同じ参照先への edge は重複させません。");
        }

        /// <summary>
        /// 深い循環を再帰せず一つの SCC として返します。
        /// </summary>
        [Test]
        public void TryAnalyze_FindsDeepCycleAsOneDeterministicComponent()
        {
            const int cycleLength = 2048;
            var sources = new AuditEditor.AssemblyDefinitionSource[cycleLength];
            for (var index = 0; index < cycleLength; index++)
            {
                var next = (index + 1) % cycleLength;
                sources[index] = AssemblyDependencyTestData.CreateSource(
                    $"Assets/Cycle{index:D4}.asmdef",
                    $"Cycle{index:D4}",
                    $"guid-{index:D4}",
                    new[] { $"Cycle{next:D4}" });
            }

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Cycles, Has.Count.EqualTo(1));
            Assert.That(result.Cycles[0], Is.EqualTo(Enumerable.Range(0, cycleLength)));
            AssertIssueCount(result, AuditEditor.AssemblyDependencyIssueKind.DependencyCycle, 1);
        }

        /// <summary>
        /// 入力順と重複 edge に依存せず forward/reverse graph を昇順へ固定します。
        /// </summary>
        [Test]
        public void TryAnalyze_BuildsDeterministicForwardAndReverseGraphs()
        {
            var sources = new[]
            {
                AssemblyDependencyTestData.CreateSource("Assets/Z.asmdef", "Z", "guid-z", new[] { "B", "A", "A" }),
                AssemblyDependencyTestData.CreateSource("Assets/B.asmdef", "B", "guid-b", new[] { "A" }),
                AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", "guid-a")
            };

            var firstSucceeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var first,
                out _,
                out var firstError);
            var secondSucceeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources.Reverse().ToArray(),
                new FakeAssemblyDependencySourceAdapter(),
                out var second,
                out _,
                out var secondError);

            Assert.That(firstSucceeded, Is.True, firstError);
            Assert.That(secondSucceeded, Is.True, secondError);
            Assert.That(first.Assemblies.Select(node => node.Name), Is.EqualTo(new[] { "A", "B", "Z" }));
            Assert.That(first.Dependencies[0], Is.Empty);
            Assert.That(first.Dependencies[1], Is.EqualTo(new[] { 0 }));
            Assert.That(first.Dependencies[2], Is.EqualTo(new[] { 0, 1 }));
            Assert.That(first.Dependents[0], Is.EqualTo(new[] { 1, 2 }));
            Assert.That(first.Dependents[1], Is.EqualTo(new[] { 2 }));
            Assert.That(first.Dependents[2], Is.Empty);
            Assert.That(FlattenGraph(second.Dependencies), Is.EqualTo(FlattenGraph(first.Dependencies)));
            Assert.That(FlattenGraph(second.Dependents), Is.EqualTo(FlattenGraph(first.Dependents)));
        }

        /// <summary>
        /// null の入力または null source は SourceUnavailable として部分結果を破棄します。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsUnavailableSourcesWithoutPartialResult()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter();

            var nullListSucceeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                null,
                adapter,
                out var nullListResult,
                out var nullListError,
                out _);
            var nullAdapterSucceeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                Array.Empty<AuditEditor.AssemblyDefinitionSource>(),
                null,
                out var nullAdapterResult,
                out var nullAdapterError,
                out _);
            var nullItemSucceeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                new AuditEditor.AssemblyDefinitionSource[] { null },
                adapter,
                out var nullItemResult,
                out var nullItemError,
                out _);

            AssertFailure(nullListSucceeded, nullListResult, nullListError, AuditEditor.AssemblyDependencyAuditError.SourceUnavailable);
            AssertFailure(nullAdapterSucceeded, nullAdapterResult, nullAdapterError, AuditEditor.AssemblyDependencyAuditError.SourceUnavailable);
            AssertFailure(nullItemSucceeded, nullItemResult, nullItemError, AuditEditor.AssemblyDependencyAuditError.SourceUnavailable);
        }

        /// <summary>
        /// asmdef 数が上限を一件でも超えたら解析前に拒否します。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsTooManyAssemblyDefinitionsWithoutPartialResult()
        {
            var source = AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", "guid-a");
            var sources = Enumerable.Repeat(source, AuditEditor.AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions + 1).ToArray();

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out var error,
                out _);

            AssertFailure(succeeded, result, error, AuditEditor.AssemblyDependencyAuditError.TooManyAssemblyDefinitions);
        }

        /// <summary>
        /// 一件の JSON が文字数上限を超えたら内容を解析せず拒否します。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsOversizedSourceWithoutPartialResult()
        {
            var source = new AuditEditor.AssemblyDefinitionSource(
                "Assets/Large.asmdef",
                "guid-large",
                new string('x', AuditEditor.AssemblyDependencyAnalyzer.MaximumSourceCharacters + 1));

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                new[] { source },
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out var error,
                out _);

            AssertFailure(succeeded, result, error, AuditEditor.AssemblyDependencyAuditError.SourceTooLarge);
        }

        /// <summary>
        /// 一件の参照数が上限を超えたら graph を返しません。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsTooManyReferencesPerAssemblyWithoutPartialResult()
        {
            var references = Enumerable.Repeat("Missing", AuditEditor.AssemblyDependencyAnalyzer.MaximumReferencesPerAssembly + 1).ToArray();
            var source = AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", "guid-a", references);

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                new[] { source },
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out var error,
                out _);

            AssertFailure(succeeded, result, error, AuditEditor.AssemblyDependencyAuditError.TooManyReferencesPerAssembly);
        }

        /// <summary>
        /// 全 source の参照総数が上限を超えたら graph 構築前に拒否します。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsTooManyTotalReferencesWithoutPartialResult()
        {
            var references = Enumerable.Repeat("Missing", AuditEditor.AssemblyDependencyAnalyzer.MaximumReferencesPerAssembly).ToArray();
            var sourceCount = (AuditEditor.AssemblyDependencyAnalyzer.MaximumReferences / references.Length) + 1;
            var sources = new AuditEditor.AssemblyDefinitionSource[sourceCount];
            for (var index = 0; index < sources.Length; index++)
            {
                sources[index] = AssemblyDependencyTestData.CreateSource($"Assets/{index:D3}.asmdef", $"Assembly{index:D3}", $"guid-{index:D3}", references);
            }

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out var error,
                out _);

            AssertFailure(succeeded, result, error, AuditEditor.AssemblyDependencyAuditError.TooManyReferences);
        }

        /// <summary>
        /// 問題数が上限へ達したら問題一覧を含む部分結果を破棄します。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsTooManyIssuesWithoutPartialResult()
        {
            var sources = new AuditEditor.AssemblyDefinitionSource[AuditEditor.AssemblyDependencyAnalyzer.MaximumAssemblyDefinitions];
            for (var index = 0; index < sources.Length; index++)
            {
                sources[index] = AssemblyDependencyTestData.CreateSource(
                    $"Assets/{index:D5}.asmdef",
                    "Duplicate",
                    "duplicate-guid",
                    new[] { "Duplicate", "GUID:duplicate-guid" },
                    new[] { "Standalone" },
                    new[] { "WebGL" });
            }

            var succeeded = AuditEditor.AssemblyDependencyAnalyzer.TryAnalyze(
                sources,
                new FakeAssemblyDependencySourceAdapter(),
                out var result,
                out var error,
                out _);

            AssertFailure(succeeded, result, error, AuditEditor.AssemblyDependencyAuditError.TooManyIssues);
        }

        /// <summary>指定種別の問題数を検証します。</summary>
        private static void AssertIssueCount(
            AuditEditor.AssemblyDependencyAuditResult result,
            AuditEditor.AssemblyDependencyIssueKind kind,
            int expectedCount)
        {
            Assert.That(result.Issues.Count(issue => issue.Kind == kind), Is.EqualTo(expectedCount));
        }

        /// <summary>失敗時に結果が残らず、理由が一致することを検証します。</summary>
        private static void AssertFailure(
            bool succeeded,
            AuditEditor.AssemblyDependencyAuditResult result,
            AuditEditor.AssemblyDependencyAuditError actualError,
            AuditEditor.AssemblyDependencyAuditError expectedError)
        {
            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(actualError, Is.EqualTo(expectedError));
        }

        /// <summary>二次元 graph を比較しやすい文字列表現へ変換します。</summary>
        private static string FlattenGraph(IReadOnlyList<IReadOnlyList<int>> graph)
        {
            return string.Join("|", graph.Select(edges => string.Join(",", edges)));
        }
    }
}
