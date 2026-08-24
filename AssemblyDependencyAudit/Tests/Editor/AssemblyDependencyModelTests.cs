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
            var result = new AuditEditor.AssemblyDependencyAuditResult(assemblies, issues, dependencies, dependents, cycles);

            assemblies.Clear();
            issues.Clear();
            ((List<int>)dependencies[0]).Clear();
            dependencies.Clear();
            ((List<int>)dependents[0]).Clear();
            dependents.Clear();
            ((List<int>)cycles[0]).Clear();
            cycles.Clear();

            Assert.That(node.IncludePlatforms, Is.EqualTo(new[] { "Editor" }));
            Assert.That(node.ExcludePlatforms, Is.EqualTo(new[] { "WebGL" }));
            Assert.That(node.References, Has.Count.EqualTo(1));
            Assert.That(result.Assemblies, Has.Count.EqualTo(1));
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Dependencies[0], Is.EqualTo(new[] { 0 }));
            Assert.That(result.Dependents[0], Is.EqualTo(new[] { 0 }));
            Assert.That(result.Cycles[0], Is.EqualTo(new[] { 0, 1 }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)node.IncludePlatforms).Add("Player"));
            Assert.Throws<NotSupportedException>(() => ((IList<AuditEditor.AssemblyDependencyNode>)result.Assemblies).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<int>)result.Dependencies[0]).Add(1));
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
            var reference = new AuditEditor.AssemblyDependencyReference(null, AuditEditor.AssemblyDependencyReferenceKind.Name, -1);
            var issue = new AuditEditor.AssemblyDependencyIssue(
                AuditEditor.AssemblyDependencyIssueKind.UnresolvedReference,
                null,
                null,
                null,
                null);

            Assert.That(source.AssetPath, Is.EqualTo("Assets/A.asmdef"));
            Assert.That(source.Guid, Is.Empty);
            Assert.That(source.Json, Is.Empty);
            Assert.That(reference.Value, Is.Empty);
            Assert.That(reference.IsResolved, Is.False);
            Assert.That(issue.AssetPath, Is.Empty);
            Assert.That(issue.RelatedAssetPath, Is.Empty);
            Assert.That(issue.Reference, Is.Empty);
            Assert.That(issue.Message, Is.Empty);
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
