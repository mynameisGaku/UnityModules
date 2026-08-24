using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// service が source adapter と analyzer を一回の完全な監査として呼ぶことを検証します。
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
                }
            };
            adapter.ReferencePaths.Add("Alias", "Assets/A.asmdef");
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.True, errorMessage);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.None));
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Dependencies[1], Is.EqualTo(new[] { 0 }));
            Assert.That(adapter.ReadCallCount, Is.EqualTo(1));
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
                ReadError = "read failed"
            };
            var service = new AuditEditor.AssemblyDependencyAuditService(adapter);

            var succeeded = service.TryAudit(out var result, out var error, out var errorMessage);

            Assert.That(succeeded, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.EqualTo(AuditEditor.AssemblyDependencyAuditError.SourceUnavailable));
            Assert.That(errorMessage, Is.EqualTo("read failed"));
            Assert.That(adapter.ReadCallCount, Is.EqualTo(1));
            Assert.That(adapter.ResolveCallCount, Is.Zero);
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
