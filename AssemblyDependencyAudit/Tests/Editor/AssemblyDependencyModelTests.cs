using System;
using System.Collections.Generic;
using NUnit.Framework;
using AuditEditor = AssemblyDependencyAudit.Editor;

namespace AssemblyDependencyAudit.Tests
{
    /// <summary>
    /// 監査結果 DTO が入力 collection から独立した読み取り専用値になることを検証します。
    /// </summary>
    internal sealed class AssemblyDependencyModelTests
    {
        /// <summary>
        /// node と result は元 collection の変更を受けず、返した一覧も変更できません。
        /// </summary>
        [Test]
        public void Constructors_DefensivelyCopyAllCollections()
        {
            var includePlatforms = new List<string> { "Editor" };
            var excludePlatforms = new List<string> { "WebGL" };
            var references = new List<AuditEditor.AssemblyDependencyReference>
            {
                new AuditEditor.AssemblyDependencyReference("A", AuditEditor.AssemblyDependencyReferenceKind.Name, 0)
            };
            var node = new AuditEditor.AssemblyDependencyNode(
                "A",
                "Assets/A.asmdef",
                "guid-a",
                true,
                includePlatforms,
                excludePlatforms,
                references);
            includePlatforms.Add("Standalone");
            excludePlatforms.Clear();
            references.Clear();

            var assemblies = new List<AuditEditor.AssemblyDependencyNode> { node };
            var issues = new List<AuditEditor.AssemblyDependencyIssue>
            {
                new AuditEditor.AssemblyDependencyIssue(
                    AuditEditor.AssemblyDependencyIssueKind.SelfReference,
                    "Assets/A.asmdef",
                    "Assets/A.asmdef",
                    "A",
                    "self")
            };
            var dependencies = new List<IReadOnlyList<int>> { new List<int> { 0 } };
            var dependents = new List<IReadOnlyList<int>> { new List<int> { 0 } };
            var cycles = new List<IReadOnlyList<int>> { new List<int> { 0, 1 } };
            var assemblyReferences = new List<AuditEditor.AssemblyReferenceTarget>
            {
                new AuditEditor.AssemblyReferenceTarget(
                    "Assets/A.asmref",
                    "A",
                    AuditEditor.AssemblyReferenceTargetKind.Name,
                    "Assets/A.asmdef")
            };
            var result = new AuditEditor.AssemblyDependencyAuditResult(
                assemblies,
                issues,
                dependencies,
                dependents,
                cycles,
                assemblyReferences);

            assemblies.Clear();
            issues.Clear();
            ((List<int>)dependencies[0]).Clear();
            dependencies.Clear();
            ((List<int>)dependents[0]).Clear();
            dependents.Clear();
            ((List<int>)cycles[0]).Clear();
            cycles.Clear();
            assemblyReferences.Clear();

            Assert.That(node.IncludePlatforms, Is.EqualTo(new[] { "Editor" }));
            Assert.That(node.ExcludePlatforms, Is.EqualTo(new[] { "WebGL" }));
            Assert.That(node.References, Has.Count.EqualTo(1));
            Assert.That(result.Assemblies, Has.Count.EqualTo(1));
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Dependencies[0], Is.EqualTo(new[] { 0 }));
            Assert.That(result.Dependents[0], Is.EqualTo(new[] { 0 }));
            Assert.That(result.Cycles[0], Is.EqualTo(new[] { 0, 1 }));
            Assert.That(result.AssemblyReferences, Has.Count.EqualTo(1));
            Assert.That(result.AssemblyReferences[0].ResolvedTargetAssetPath, Is.EqualTo("Assets/A.asmdef"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)node.IncludePlatforms).Add("Player"));
            Assert.Throws<NotSupportedException>(() => ((IList<AuditEditor.AssemblyDependencyNode>)result.Assemblies).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<int>)result.Dependencies[0]).Add(1));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<AuditEditor.AssemblyReferenceTarget>)result.AssemblyReferences).Clear());
        }

        /// <summary>
        /// Editor 専用判定は includePlatforms が Editor 一件だけの場合に限ります。
        /// </summary>
        [Test]
        public void IsEditorOnly_RequiresExactlyOneEditorPlatform()
        {
            var editorOnly = CreateNode(new[] { "editor" });
            var playerCapable = CreateNode(Array.Empty<string>());
            var mixed = CreateNode(new[] { "Editor", "Standalone" });

            Assert.That(editorOnly.IsEditorOnly, Is.True);
            Assert.That(playerCapable.IsEditorOnly, Is.False);
            Assert.That(mixed.IsEditorOnly, Is.False);
        }

        /// <summary>
        /// source path と null 値を constructor 境界で正規化します。
        /// </summary>
        [Test]
        public void ValueObjects_NormalizePathsAndNullStrings()
        {
            var source = new AuditEditor.AssemblyDefinitionSource("Assets\\A.asmdef", null, null);
            var assemblyReferenceSource = new AuditEditor.AssemblyReferenceSource("Assets\\A.asmref", null, null);
            var reference = new AuditEditor.AssemblyDependencyReference(null, AuditEditor.AssemblyDependencyReferenceKind.Name, -1);
            var unresolvedAssemblyReference = new AuditEditor.AssemblyReferenceTarget(
                null,
                null,
                AuditEditor.AssemblyReferenceTargetKind.Unknown,
                null);
            var resolvedAssemblyReference = new AuditEditor.AssemblyReferenceTarget(
                "Assets/A.asmref",
                "A",
                AuditEditor.AssemblyReferenceTargetKind.Name,
                "Assets/A.asmdef");
            var issue = new AuditEditor.AssemblyDependencyIssue(
                AuditEditor.AssemblyDependencyIssueKind.UnresolvedReference,
                null,
                null,
                null,
                null);

            Assert.That(source.AssetPath, Is.EqualTo("Assets/A.asmdef"));
            Assert.That(source.Guid, Is.Empty);
            Assert.That(source.Json, Is.Empty);
            Assert.That(assemblyReferenceSource.AssetPath, Is.EqualTo("Assets/A.asmref"));
            Assert.That(assemblyReferenceSource.Guid, Is.Empty);
            Assert.That(assemblyReferenceSource.Json, Is.Empty);
            Assert.That(reference.Value, Is.Empty);
            Assert.That(reference.IsResolved, Is.False);
            Assert.That(unresolvedAssemblyReference.AssetPath, Is.Empty);
            Assert.That(unresolvedAssemblyReference.RawReference, Is.Empty);
            Assert.That(unresolvedAssemblyReference.ResolvedTargetAssetPath, Is.Empty);
            Assert.That(unresolvedAssemblyReference.IsResolved, Is.False);
            Assert.That(resolvedAssemblyReference.IsResolved, Is.True);
            Assert.That(issue.AssetPath, Is.Empty);
            Assert.That(issue.RelatedAssetPath, Is.Empty);
            Assert.That(issue.Reference, Is.Empty);
            Assert.That(issue.Message, Is.Empty);
        }

        /// <summary>
        /// asmref 監査の実装型を Editor assembly の internal surface に限定します。
        /// </summary>
        [Test]
        public void AssemblyReferenceAuditTypes_DoNotExpandPublicSurface()
        {
            var types = new[]
            {
                typeof(AuditEditor.AssemblyReferenceSource),
                typeof(AuditEditor.AssemblyReferenceTarget),
                typeof(AuditEditor.AssemblyReferenceTargetKind),
                typeof(AuditEditor.AssemblyReferenceJsonParseStatus),
                typeof(AuditEditor.AssemblyReferenceJsonParser),
                typeof(AuditEditor.AssemblyReferenceAnalyzer),
                typeof(AuditEditor.IAssemblyReferenceSourceAdapter)
            };

            for (var index = 0; index < types.Length; index++)
            {
                var type = types[index];
                Assert.That(type.IsPublic || type.IsNestedPublic, Is.False, type.FullName);
            }
        }

        /// <summary>指定 platform を持つ最小 node を作ります。</summary>
        private static AuditEditor.AssemblyDependencyNode CreateNode(IReadOnlyList<string> includePlatforms)
        {
            return new AuditEditor.AssemblyDependencyNode(
                "A",
                "Assets/A.asmdef",
                "guid-a",
                true,
                includePlatforms,
                Array.Empty<string>(),
                Array.Empty<AuditEditor.AssemblyDependencyReference>());
        }
    }
}
