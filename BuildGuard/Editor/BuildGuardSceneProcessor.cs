// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// プレイヤービルドが処理する各シーンを検査し、欠落参照があればビルドを停止します。
    /// </summary>
    [BuildCallbackVersion(1)]
    internal sealed class BuildGuardSceneProcessor : IProcessSceneWithReport
    {
        /// <summary>
        /// 通常のシーン処理より前に実行する処理順です。
        /// </summary>
        internal const int CallbackOrder = -10000;

        /// <summary>
        /// Unityのビルド処理順を返します。
        /// </summary>
        public int callbackOrder => CallbackOrder;

        /// <summary>
        /// プレイヤービルド中のシーンだけを検査し、通常のプレイモード読込は対象外にします。
        /// </summary>
        /// <param name="scene">Unityがビルド用に処理しているシーンです。</param>
        /// <param name="report">プレイヤービルドの報告情報です。対象外の読込では空です。</param>
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            ValidateScene(scene);
        }

        /// <summary>
        /// 読込済みシーン1件を検査し、ビルドを停止する問題があれば例外を送出します。
        /// </summary>
        internal static void ValidateScene(Scene scene)
        {
            var inspection = BuildGuardSceneInspector.Inspect(scene);
            if (!inspection.HasFindings)
            {
                return;
            }

            throw new BuildFailedException(BuildGuardMessageFormatter.Format(
                scene,
                inspection.MissingScripts,
                inspection.MissingObjectReferences));
        }
    }
}
