// SPDX-License-Identifier: MIT

using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 読込済みシーン1件へ、ビルドガードの全検査規則を適用します。
    /// </summary>
    internal static class BuildGuardSceneInspector
    {
        /// <summary>欠落スクリプトと、直列化済みの壊れたオブジェクト参照を収集します。</summary>
        internal static BuildGuardSceneInspection Inspect(Scene scene)
        {
            return new BuildGuardSceneInspection(
                MissingScriptSceneScanner.Scan(scene),
                MissingObjectReferenceSceneScanner.Scan(scene));
        }
    }
}
