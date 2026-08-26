using System;
using System.Linq;
using NUnit.Framework;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// Editor assembly が公開 API や build callback を追加しないことを検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditPublicSurfaceTests
    {
        /// <summary>
        /// package coverageを含む機能はinternalに閉じ、利用者向け型を公開しません。
        /// </summary>
        [Test]
        public void EditorAssembly_ExportsNoPublicTypes()
        {
            var assembly = FindEditorAssembly();

            Assert.That(assembly.GetExportedTypes(), Is.Empty);
        }

        /// <summary>
        /// 手動 advisory 監査へ build 前後の自動実行 hook を混在させません。
        /// </summary>
        [Test]
        public void EditorAssembly_DefinesNoBuildCallbacks()
        {
            var callbackInterfaceNames = new[]
            {
                "UnityEditor.Build.IOrderedCallback",
                "UnityEditor.Build.IPreprocessBuildWithReport",
                "UnityEditor.Build.IPostprocessBuildWithReport",
                "UnityEditor.Build.IProcessSceneWithReport"
            };
            var callbackTypes = FindEditorAssembly()
                .GetTypes()
                .Where(type => type.GetInterfaces().Any(
                    implemented => callbackInterfaceNames.Contains(implemented.FullName, StringComparer.Ordinal)))
                .ToArray();

            Assert.That(callbackTypes, Is.Empty);
        }

        /// <summary>
        /// test assemblyを除くLocalizationKeyAudit assemblyはEditor専用の一件だけに固定します。
        /// </summary>
        [Test]
        public void LoadedProductAssemblies_ContainNoRuntimeAssembly()
        {
            var productAssemblyNames = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name)
                .Where(name => name != null &&
                    name.StartsWith("LocalizationKeyAudit.", StringComparison.Ordinal) &&
                    !name.EndsWith(".Tests", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(productAssemblyNames, Is.EqualTo(new[] { "LocalizationKeyAudit.Editor" }));
        }

        /// <summary>読み込み済み assembly から監査 Editor assembly を一件だけ取得します。</summary>
        private static System.Reflection.Assembly FindEditorAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Single(
                assembly => string.Equals(
                    assembly.GetName().Name,
                    "LocalizationKeyAudit.Editor",
                    StringComparison.Ordinal));
        }
    }
}
