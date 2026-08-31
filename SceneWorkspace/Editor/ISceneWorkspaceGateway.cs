using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>Unityのシーン変更を、順序が一定の計画処理と復元試験から分離します。</summary>
    internal interface ISceneWorkspaceGateway
    {
        /// <summary>現在のエディター状態と順序付きシーン構成を取得します。</summary>
        SceneWorkspaceSnapshot CaptureCurrentSetup();

        /// <summary>指定設定の識別情報と順序付き目標構成を取得します。</summary>
        SceneWorkspaceProfileSnapshot CaptureProfile(SceneWorkspaceProfile profile);

        /// <summary>指定した順番、読込状態、使用中状態へシーン構成を復元します。</summary>
        void RestoreSetup(IReadOnlyList<SceneWorkspaceSceneState> scenes);
    }
}
