using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>変更を伴わない検証に必要なエディター状態と順序付きシーン構成を保持します。</summary>
    internal sealed class SceneWorkspaceSnapshot
    {
        /// <summary>エディター状態とシーン構成から一つの現在状態記録を作成します。</summary>
        internal SceneWorkspaceSnapshot(bool playModeActive, bool compiling, bool updating, bool prefabStageOpen, IEnumerable<SceneWorkspaceSceneState> scenes)
        {
            PlayModeActive = playModeActive;
            Compiling = compiling;
            Updating = updating;
            PrefabStageOpen = prefabStageOpen;
            Scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
        }

        /// <summary>再生モード中または切り替え中かを返します。</summary>
        internal bool PlayModeActive { get; }

        /// <summary>スクリプトをコンパイル中かを返します。</summary>
        internal bool Compiling { get; }

        /// <summary>アセットを更新中かを返します。</summary>
        internal bool Updating { get; }

        /// <summary>プレハブ編集画面が開いているかを返します。</summary>
        internal bool PrefabStageOpen { get; }

        /// <summary>現在のシーン構成を順序付きの読み取り専用一覧で返します。</summary>
        internal IReadOnlyList<SceneWorkspaceSceneState> Scenes { get; }
    }
}
