// SPDX-License-Identifier: MIT

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Player buildで処理される各Sceneを検査し、Missing Scriptがあればbuildを中止します。
    /// </summary>
    [BuildCallbackVersion(1)]
    internal sealed class BuildGuardSceneProcessor : IProcessSceneWithReport
    {
        /// <summary>
        /// 他のScene変換より先に検査するためのcallback順序です。
        /// </summary>
        internal const int CallbackOrder = -10000;

        /// <summary>
        /// Unity build callback間の実行順序を取得します。
        /// </summary>
        public int callbackOrder => CallbackOrder;

        /// <summary>
        /// Player build中だけSceneを検査し、Missing Script検出時はbuildを失敗させます。
        /// </summary>
        /// <param name="scene">Unityがbuild用に処理しているScene。</param>
        /// <param name="report">Player build report。PlayMode読込時はnull。</param>
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            ValidateScene(scene);
        }

        /// <summary>
        /// Sceneを検査し、Missing Scriptが存在する場合はbuild失敗例外を送出します。
        /// </summary>
        internal static void ValidateScene(Scene scene)
        {
            var findings = MissingScriptSceneScanner.Scan(scene);
            if (findings.Count == 0)
            {
                return;
            }

            throw new BuildFailedException(MissingScriptMessageFormatter.Format(scene, findings));
        }
    }
}
