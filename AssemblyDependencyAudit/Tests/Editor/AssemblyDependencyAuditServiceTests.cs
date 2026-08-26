using System.Linq;
using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// service が asmdef・asmref adapter と analyzer を一回の完全な監査として呼ぶことを検証します。
    /// </summary>
    internal sealed class AssemblyDependencyAuditServiceTests
    {
        /// <summary>
        /// 読み取った source と compiler path 解決を analyzer へ渡します。
        /// </summary>
        [Test]
        public void TryAudit_ReadsSourcesAndReturnsCompleteResult()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                Sources = new[]
                {
                    AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", "guid-a"),
                    AssemblyDependencyTestData.CreateSource("Assets/B.asmdef", "B", "guid-b", new[] { "Alias" })
                },
                AssemblyReferenceSources = new[]
                {
                    AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                        "Assets/Feature/A.asmref",
                        "11111111111111111111111111111111",
                        "A")
                }
            };
            adapter.ReferencePaths.Add("Alias", "Assets/A.asmdef");
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Dependencies[1], Is.EqualTo(new[] { 0 }));
            Assert.That(result.AssemblyReferences.Single().ResolvedTargetAssetPath, Is.EqualTo("Assets/A.asmdef"));
            Assert.That(adapter.ReadCallCount, Is.EqualTo(1));
            Assert.That(adapter.AssemblyReferenceReadCallCount, Is.EqualTo(1));
            Assert.That(adapter.ResolveCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// source 読み取り失敗を保持し、部分結果と参照解決を返しません。
        /// </summary>
        [Test]
        public void TryAudit_SourceReadFailureReturnsNoPartialResult()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                ReadSucceeds = false,
                ReadAuditError = AuditEditor.AssemblyDependencyAuditError.AssemblyAssetTotalBytesExceeded,
                ReadError = "read failed"
            };
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.AssemblyAssetTotalBytesExceeded));
            Assert.That(errorMessage, Is.EqualTo("read failed"));
            Assert.That(adapter.ReadCallCount, Is.EqualTo(1));
            Assert.That(adapter.AssemblyReferenceReadCallCount, Is.Zero);
            Assert.That(adapter.ResolveCallCount, Is.Zero);
        }

        /// <summary>
        /// reparse pointを通るtyped assembly assetを安全に監査できない場合も部分結果を返しません。
        /// </summary>
        [Test]
        public void TryAudit_UnsafeAssemblyAssetPathReturnsNoPartialResult()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                ReadSucceeds = false,
                ReadAuditError = AuditEditor.AssemblyDependencyAuditError.UnsafeAssemblyAssetPath,
                ReadError = "unsafe reparse path"
            };
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.UnsafeAssemblyAssetPath));
            Assert.That(errorMessage, Is.EqualTo("unsafe reparse path"));
            Assert.That(adapter.ReadCallCount, Is.EqualTo(1));
            Assert.That(adapter.AssemblyReferenceReadCallCount, Is.Zero);
            Assert.That(adapter.ResolveCallCount, Is.Zero);
        }

        /// <summary>
        /// asmref 読み取りの typed error と説明を保持し、asmdef 部分結果を返しません。
        /// </summary>
        [Test]
        public void TryAudit_AssemblyReferenceReadFailureReturnsNoPartialResult()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                Sources = new[]
                {
                    AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", "guid-a")
                },
                AssemblyReferenceReadSucceeds = false,
                AssemblyReferenceReadError = AuditEditor.AssemblyDependencyAuditError.AssemblyAssetTraversalLimitExceeded,
                AssemblyReferenceReadErrorMessage = "traversal limit"
            };
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error,
                Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.AssemblyAssetTraversalLimitExceeded));
            Assert.That(errorMessage, Is.EqualTo("traversal limit"));
            Assert.That(adapter.ReadCallCount, Is.EqualTo(1));
            Assert.That(adapter.AssemblyReferenceReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// asmref adapter を明示的に省略した互換経路は asmdef graph だけを完全結果として返します。
        /// </summary>
        [Test]
        public void TryAudit_NullAssemblyReferenceAdapterReturnsAssemblyOnlyResult()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                Sources = new[]
                {
                    AssemblyDependencyTestData.CreateSource("Assets/A.asmdef", "A", "guid-a")
                }
            };
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter, null);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result.Assemblies, Has.Count.EqualTo(1));
            Assert.That(result.AssemblyReferences, Is.Empty);
            Assert.That(adapter.AssemblyReferenceReadCallCount, Is.Zero);
        }

        /// <summary>
        /// asmref adapterなしでも同じfolderのasmdefだけによるowner競合を検出します。
        /// </summary>
        [Test]
        public void TryAudit_NullAssemblyReferenceAdapterDetectsAssemblyDefinitionOwners()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                Sources = new[]
                {
                    AssemblyDependencyTestData.CreateSource("Assets/Feature/A.asmdef", "A", "guid-a"),
                    AssemblyDependencyTestData.CreateSource("Assets/Feature/B.asmdef", "B", "guid-b")
                }
            };
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter, null);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result.Issues.Select(issue => issue.Kind), Is.EqualTo(new[]
            {
                AuditEditor.AssemblyDependencyIssueKind.MultipleAssemblyOwnersInFolder,
                AuditEditor.AssemblyDependencyIssueKind.MultipleAssemblyOwnersInFolder
            }));
            Assert.That(result.Issues.Select(issue => issue.AssetPath), Is.EqualTo(new[]
            {
                "Assets/Feature/A.asmdef",
                "Assets/Feature/B.asmdef"
            }));
            Assert.That(adapter.AssemblyReferenceReadCallCount, Is.Zero);
        }

        /// <summary>
        /// asmdefとasmrefを同じfolderに置いたcross-kind owner競合を完全監査で検出します。
        /// </summary>
        [Test]
        public void TryAudit_CombinedAdapterDetectsCrossKindOwners()
        {
            var adapter = new FakeAssemblyDependencySourceAdapter
            {
                Sources = new[]
                {
                    AssemblyDependencyTestData.CreateSource("Assets/Feature/A.asmdef", "A", "guid-a")
                },
                AssemblyReferenceSources = new[]
                {
                    AssemblyDependencyTestData.CreateAssemblyReferenceSource(
                        "Assets/Feature/B.asmref",
                        "11111111111111111111111111111111",
                        "A")
                }
            };
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result.AssemblyReferences.Single().ResolvedTargetAssetPath,
                Is.EqualTo("Assets/Feature/A.asmdef"));
            Assert.That(result.Issues.Select(issue => issue.AssetPath), Is.EqualTo(new[]
            {
                "Assets/Feature/A.asmdef",
                "Assets/Feature/B.asmref"
            }));
            Assert.That(result.Issues.All(issue =>
                issue.Kind == AuditEditor.AssemblyDependencyIssueKind.MultipleAssemblyOwnersInFolder), Is.True);
            Assert.That(adapter.AssemblyReferenceReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// null adapter は source へ触れず SourceUnavailable を返します。
        /// </summary>
        [Test]
        public void TryAudit_NullAdapterReturnsNoPartialResult()
        {
            var service = new AuditEditor.AssemblyDependencyAuditService(null);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.SourceUnavailable));
            Assert.That(errorMessage, Is.Not.Empty);
        }
    }
}
