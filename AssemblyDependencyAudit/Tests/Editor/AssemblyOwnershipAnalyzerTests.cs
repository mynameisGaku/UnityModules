using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// 同じfolderのasmdef・asmref owner検出、決定論性、結果不変、安全上限を検証します。
    /// </summary>
    internal sealed class AssemblyOwnershipAnalyzerTests
    {
        /// <summary>
        /// asmdefだけ、asmrefだけ、3 ownerの混在groupで各ownerに1件ずつ問題を返します。
        /// </summary>
        [Test]
        public void TryAnalyze_ReportsOneIssuePerOwnerAcrossAllOwnerKinds()
        {
            var sourceResult = CreateResult(
                new[]
                {
                    CreateNode("Assets/Asmdefs/A.asmdef", "A"),
                    CreateNode("Assets/Asmdefs/B.asmdef", "B"),
                    CreateNode("Assets/Mixed/A.asmdef", "MixedA")
                },
                new[]
                {
                    CreateTarget("Assets/Asmrefs/A.asmref", "A"),
                    CreateTarget("Assets/Asmrefs/B.asmref", "B"),
                    CreateTarget("Assets/Mixed/B.asmref", "MixedA"),
                    CreateTarget("Assets/Mixed/C.asmref", "MixedA")
                });

            var succeeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                sourceResult,
                out var result,
                out var error,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result.Issues, Has.Count.EqualTo(7));
            Assert.That(result.Issues.All(issue =>
                issue.Kind == AuditEditor.AssemblyDependencyIssueKind.MultipleAssemblyOwnersInFolder), Is.True);
            Assert.That(result.Issues.Select(issue => issue.AssetPath), Is.EqualTo(new[]
            {
                "Assets/Asmdefs/A.asmdef",
                "Assets/Asmdefs/B.asmdef",
                "Assets/Asmrefs/A.asmref",
                "Assets/Asmrefs/B.asmref",
                "Assets/Mixed/A.asmdef",
                "Assets/Mixed/B.asmref",
                "Assets/Mixed/C.asmref"
            }));
            Assert.That(result.Issues.Select(issue => issue.RelatedAssetPath), Is.EqualTo(new[]
            {
                "Assets/Asmdefs/B.asmdef",
                "Assets/Asmdefs/A.asmdef",
                "Assets/Asmrefs/B.asmref",
                "Assets/Asmrefs/A.asmref",
                "Assets/Mixed/B.asmref",
                "Assets/Mixed/A.asmdef",
                "Assets/Mixed/A.asmdef"
            }));
            Assert.That(result.Issues.Select(issue => issue.Reference), Is.EqualTo(new[]
            {
                "Assets/Asmdefs",
                "Assets/Asmdefs",
                "Assets/Asmrefs",
                "Assets/Asmrefs",
                "Assets/Mixed",
                "Assets/Mixed",
                "Assets/Mixed"
            }));
        }

        /// <summary>
        /// JSON不正asmdefとUnknown asmrefもscript所属ownerとして数えます。
        /// </summary>
        [Test]
        public void TryAnalyze_CountsInvalidAssemblyDefinitionAndUnknownAssemblyReference()
        {
            var sourceResult = CreateResult(
                new[] { CreateNode("Assets/Broken/A.asmdef", string.Empty, false) },
                new[]
                {
                    new AuditEditor.AssemblyReferenceTarget(
                        "Assets/Broken/B.asmref",
                        string.Empty,
                        AuditEditor.AssemblyReferenceTargetKind.Unknown,
                        string.Empty)
                });

            var succeeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                sourceResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Issues, Has.Count.EqualTo(2));
            Assert.That(result.Issues.Select(issue => issue.AssetPath), Is.EqualTo(new[]
            {
                "Assets/Broken/A.asmdef",
                "Assets/Broken/B.asmref"
            }));
        }

        /// <summary>
        /// 親子folder、別folder、同じtargetを指す別folderのasmrefを競合扱いしません。
        /// </summary>
        [Test]
        public void TryAnalyze_DoesNotReportNestedOrDifferentFoldersOrSharedTarget()
        {
            var sourceResult = CreateResult(
                new[]
                {
                    CreateNode("Assets/Parent/A.asmdef", "A"),
                    CreateNode("Assets/Parent/Child/B.asmdef", "B")
                },
                new[]
                {
                    CreateTarget("Assets/FeatureOne/A.asmref", "A"),
                    CreateTarget("Assets/FeatureTwo/B.asmref", "A")
                });

            var succeeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                sourceResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Issues, Is.Empty);
        }

        /// <summary>
        /// separatorは正規化し、folderの大小文字は区別し、同じowner pathは重複して数えません。
        /// </summary>
        [Test]
        public void TryAnalyze_NormalizesPathsWithoutMergingOrdinalFoldersOrDuplicateOwners()
        {
            var sourceResult = CreateResult(
                new[]
                {
                    CreateNode("Assets\\Normalized\\A.asmdef", "NormalizedA"),
                    CreateNode("Assets/Normalized/B.asmdef", "NormalizedB"),
                    CreateNode("Assets/Case/A.asmdef", "CaseA"),
                    CreateNode("Assets/case/B.asmdef", "CaseB")
                },
                new[]
                {
                    CreateTarget("Assets/Duplicate/A.asmref", "NormalizedA"),
                    CreateTarget("Assets/Duplicate/A.asmref", "NormalizedA"),
                    CreateTarget("Assets/Duplicate/B.asmref", "NormalizedB")
                });

            var succeeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                sourceResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Issues.Select(issue => issue.AssetPath), Is.EqualTo(new[]
            {
                "Assets/Duplicate/A.asmref",
                "Assets/Duplicate/B.asmref",
                "Assets/Normalized/A.asmdef",
                "Assets/Normalized/B.asmdef"
            }));
            Assert.That(result.Issues.Select(issue => issue.Reference), Is.EqualTo(new[]
            {
                "Assets/Duplicate",
                "Assets/Duplicate",
                "Assets/Normalized",
                "Assets/Normalized"
            }));
        }

        /// <summary>
        /// asmdef・asmref・既存問題の入力順を逆転しても全fieldの出力順を変えません。
        /// </summary>
        [Test]
        public void TryAnalyze_IsDeterministicAcrossInputOrder()
        {
            var assemblies = new[]
            {
                CreateNode("Assets/Owners/A.asmdef", "A"),
                CreateNode("Assets/Owners/B.asmdef", "B")
            };
            var targets = new[]
            {
                CreateTarget("Assets/Owners/C.asmref", "A"),
                CreateTarget("Assets/Owners/D.asmref", "B")
            };
            var issues = new[]
            {
                CreateIssue("Assets/Z.asmdef", "Z"),
                CreateIssue("Assets/A.asmdef", "A")
            };

            var firstSucceeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                CreateResult(assemblies, targets, issues),
                out var first,
                out _,
                out var firstMessage);
            var secondSucceeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                CreateResult(assemblies.Reverse().ToArray(), targets.Reverse().ToArray(), issues.Reverse().ToArray()),
                out var second,
                out _,
                out var secondMessage);

            Assert.That(firstSucceeded, Is.True, firstMessage);
            Assert.That(secondSucceeded, Is.True, secondMessage);
            Assert.That(first.Issues.Select(CreateIssueSignature), Is.EqualTo(second.Issues.Select(CreateIssueSignature)));
        }

        /// <summary>
        /// owner問題だけを追加し、assembly、graph、cycle、target、既存問題を完全に保持します。
        /// </summary>
        [Test]
        public void TryAnalyze_PreservesGraphCyclesTargetsAndExistingIssues()
        {
            var firstNode = CreateNode("Assets/Graph/A.asmdef", "A");
            var secondNode = CreateNode("Assets/Graph/B.asmdef", "B");
            var target = CreateTarget("Assets/Reference/C.asmref", "A");
            var existingIssue = CreateIssue("Assets/Graph/B.asmdef", "Missing");
            IReadOnlyList<IReadOnlyList<int>> dependencies = new IReadOnlyList<int>[]
            {
                new[] { 1 },
                Array.Empty<int>()
            };
            IReadOnlyList<IReadOnlyList<int>> dependents = new IReadOnlyList<int>[]
            {
                Array.Empty<int>(),
                new[] { 0 }
            };
            IReadOnlyList<IReadOnlyList<int>> cycles = new IReadOnlyList<int>[] { new[] { 0, 1 } };
            var sourceResult = new AuditEditor.AssemblyDependencyAuditResult(
                new[] { firstNode, secondNode },
                new[] { existingIssue },
                dependencies,
                dependents,
                cycles,
                new[] { target });
            var graphBefore = CreateGraphSignature(sourceResult);
            var targetBefore = sourceResult.AssemblyReferences.Select(CreateTargetSignature).ToArray();
            var existingIssueBefore = CreateIssueSignature(existingIssue);

            var succeeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                sourceResult,
                out var result,
                out _,
                out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(result.Assemblies, Is.EqualTo(new[] { firstNode, secondNode }));
            Assert.That(CreateGraphSignature(result), Is.EqualTo(graphBefore));
            Assert.That(result.AssemblyReferences.Select(CreateTargetSignature), Is.EqualTo(targetBefore));
            Assert.That(result.AssemblyReferences.Single(), Is.SameAs(target));
            Assert.That(result.Issues.Count(issue => ReferenceEquals(issue, existingIssue)), Is.EqualTo(1));
            Assert.That(CreateIssueSignature(existingIssue), Is.EqualTo(existingIssueBefore));
            Assert.That(result.Issues.Count(issue =>
                issue.Kind == AuditEditor.AssemblyDependencyIssueKind.MultipleAssemblyOwnersInFolder), Is.EqualTo(2));
        }

        /// <summary>
        /// null、null item、親folderを持たないpathを部分結果なしのSourceUnavailableにします。
        /// </summary>
        [Test]
        public void TryAnalyze_RejectsUnavailableOrInvalidInputsWithoutPartialResult()
        {
            var invalidInputs = new AuditEditor.AssemblyDependencyAuditResult[]
            {
                null,
                CreateResult(new AuditEditor.AssemblyDependencyNode[] { null }),
                CreateResult(
                    Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                    new AuditEditor.AssemblyReferenceTarget[] { null }),
                CreateResult(
                    Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                    Array.Empty<AuditEditor.AssemblyReferenceTarget>(),
                    new AuditEditor.AssemblyDependencyIssue[] { null }),
                CreateResult(new[] { CreateNode("Root.asmdef", "Root") }),
                CreateResult(
                    Array.Empty<AuditEditor.AssemblyDependencyNode>(),
                    new[] { CreateTarget("Root.asmref", "Root") })
            };

            for (var index = 0; index < invalidInputs.Length; index++)
            {
                var succeeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                    invalidInputs[index],
                    out var result,
                    out var error,
                    out var errorMessage);

                Assert.That(succeeded, Is.False, $"case {index}");
                Assert.That(result, Is.Null, $"case {index}");
                Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.SourceUnavailable), $"case {index}");
                Assert.That(errorMessage, Is.Not.Empty, $"case {index}");
            }
        }

        /// <summary>
        /// 既存問題との合計が上限exactlyなら受理し、1件超過なら全結果を破棄します。
        /// </summary>
        [Test]
        public void TryAnalyze_EnforcesCombinedIssueBoundary()
        {
            var repeatedIssue = CreateIssue("Assets/Base.asmdef", "Base");
            var maximumIssues = Enumerable.Repeat(
                    repeatedIssue,
                    AuditEditor.AssemblyDependencyAnalyzer.MaximumIssues)
                .ToArray();
            var exactIssues = Enumerable.Repeat(
                    repeatedIssue,
                    AuditEditor.AssemblyDependencyAnalyzer.MaximumIssues - 2)
                .ToArray();
            var excessiveIssues = Enumerable.Repeat(
                    repeatedIssue,
                    AuditEditor.AssemblyDependencyAnalyzer.MaximumIssues - 1)
                .ToArray();
            var owners = new[]
            {
                CreateNode("Assets/Owners/A.asmdef", "A"),
                CreateNode("Assets/Owners/B.asmdef", "B")
            };

            var maximumWithoutConflictSucceeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                CreateResult(Array.Empty<AuditEditor.AssemblyDependencyNode>(), issues: maximumIssues),
                out var maximumWithoutConflictResult,
                out var maximumWithoutConflictError,
                out var maximumWithoutConflictMessage);
            var exactSucceeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                CreateResult(owners, issues: exactIssues),
                out var exactResult,
                out var exactError,
                out var exactMessage);
            var excessiveSucceeded = AuditEditor.AssemblyOwnershipAnalyzer.TryAnalyze(
                CreateResult(owners, issues: excessiveIssues),
                out var excessiveResult,
                out var excessiveError,
                out _);

            Assert.That(maximumWithoutConflictSucceeded, Is.True, maximumWithoutConflictMessage);
            Assert.That(maximumWithoutConflictError, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(maximumWithoutConflictResult.Issues,
                Has.Count.EqualTo(AuditEditor.AssemblyDependencyAnalyzer.MaximumIssues));
            Assert.That(exactSucceeded, Is.True, exactMessage);
            Assert.That(exactError, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(exactResult.Issues, Has.Count.EqualTo(AuditEditor.AssemblyDependencyAnalyzer.MaximumIssues));
            Assert.That(excessiveSucceeded, Is.False);
            Assert.That(excessiveResult, Is.Null);
            Assert.That(excessiveError, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.TooManyIssues));
        }

        /// <summary>owner解析器をEditor assemblyのinternal surfaceに限定します。</summary>
        [Test]
        public void AssemblyOwnershipAuditTypes_DoNotExpandPublicSurface()
        {
            var types = new[]
            {
                typeof(AuditEditor.AssemblyOwnershipAnalyzer),
                typeof(AuditEditor.AssemblyDependencyIssueKind)
            };

            for (var index = 0; index < types.Length; index++)
            {
                Assert.That(types[index].IsPublic || types[index].IsNestedPublic, Is.False, types[index].FullName);
            }
        }

        /// <summary>指定pathを持つ最小asmdef nodeを作ります。</summary>
        private static AuditEditor.AssemblyDependencyNode CreateNode(
            string assetPath,
            string name,
            bool isJsonValid = true)
        {
            return new AuditEditor.AssemblyDependencyNode(
                name,
                assetPath,
                assetPath + ".guid",
                isJsonValid,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<AuditEditor.AssemblyDependencyReference>());
        }

        /// <summary>指定pathとtarget表記を持つ最小asmref targetを作ります。</summary>
        private static AuditEditor.AssemblyReferenceTarget CreateTarget(string assetPath, string rawReference)
        {
            return new AuditEditor.AssemblyReferenceTarget(
                assetPath,
                rawReference,
                AuditEditor.AssemblyReferenceTargetKind.Name,
                "Assets/Target.asmdef");
        }

        /// <summary>owner検出前から存在する最小問題を作ります。</summary>
        private static AuditEditor.AssemblyDependencyIssue CreateIssue(string assetPath, string reference)
        {
            return new AuditEditor.AssemblyDependencyIssue(
                AuditEditor.AssemblyDependencyIssueKind.UnresolvedReference,
                assetPath,
                string.Empty,
                reference,
                "fixture issue");
        }

        /// <summary>指定一覧と空graphを持つ解析入力を作ります。</summary>
        private static AuditEditor.AssemblyDependencyAuditResult CreateResult(
            IReadOnlyList<AuditEditor.AssemblyDependencyNode> assemblies,
            IReadOnlyList<AuditEditor.AssemblyReferenceTarget> assemblyReferences = null,
            IReadOnlyList<AuditEditor.AssemblyDependencyIssue> issues = null)
        {
            var graph = new IReadOnlyList<int>[assemblies?.Count ?? 0];
            for (var index = 0; index < graph.Length; index++)
            {
                graph[index] = Array.Empty<int>();
            }

            return new AuditEditor.AssemblyDependencyAuditResult(
                assemblies,
                issues ?? Array.Empty<AuditEditor.AssemblyDependencyIssue>(),
                graph,
                graph,
                Array.Empty<IReadOnlyList<int>>(),
                assemblyReferences);
        }

        /// <summary>graph三一覧を順序込みで比較できる文字列へ変換します。</summary>
        private static string CreateGraphSignature(AuditEditor.AssemblyDependencyAuditResult result)
        {
            return CreateNestedSignature(result.Dependencies) + "/" +
                CreateNestedSignature(result.Dependents) + "/" +
                CreateNestedSignature(result.Cycles);
        }

        /// <summary>二次元index一覧を区切り付き文字列へ変換します。</summary>
        private static string CreateNestedSignature(IReadOnlyList<IReadOnlyList<int>> values)
        {
            return string.Join("|", values.Select(value => string.Join(",", value)));
        }

        /// <summary>asmref targetの全fieldを決定論比較用文字列へ変換します。</summary>
        private static string CreateTargetSignature(AuditEditor.AssemblyReferenceTarget target)
        {
            return $"{target.AssetPath}|{target.RawReference}|{target.Kind}|{target.ResolvedTargetAssetPath}";
        }

        /// <summary>問題の全fieldを決定論比較用文字列へ変換します。</summary>
        private static string CreateIssueSignature(AuditEditor.AssemblyDependencyIssue issue)
        {
            return $"{issue.AssetPath}|{issue.Kind}|{issue.RelatedAssetPath}|{issue.Reference}|{issue.Message}";
        }
    }
}
