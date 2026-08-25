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
        /// v1.0.0 の機能は internal に閉じ、利用者向け型を公開しません。
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
                "UnityEditor.Build.IPreprocessBuildWithReport",
                "UnityEditor.Build.IPostprocessBuildWithReport"
            };
            var callbackTypes = FindEditorAssembly()
                .GetTypes()
                .Where(type => type.GetInterfaces().Any(
                    implemented => callbackInterfaceNames.Contains(implemented.FullName, StringComparer.Ordinal)))
                .ToArray();

            Assert.That(callbackTypes, Is.Empty);
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
