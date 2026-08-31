using System;
using System.Collections.Generic;
using System.Linq;

namespace SceneWorkspace.Editor
{
    /// <summary>直列化された設定項目を保持せず、設定の識別情報と順序付き目標構成を記録します。</summary>
    internal sealed class SceneWorkspaceProfileSnapshot
    {
        /// <summary>設定の存在状態、識別情報、名前、シーン構成から記録を作成します。</summary>
        internal SceneWorkspaceProfileSnapshot(bool exists, string guid, string path, string name, IEnumerable<SceneWorkspaceSceneState> scenes)
        {
            Exists = exists;
            Guid = guid ?? string.Empty;
            Path = path ?? string.Empty;
            Name = name ?? string.Empty;
            Scenes = Array.AsReadOnly((scenes ?? Enumerable.Empty<SceneWorkspaceSceneState>()).ToArray());
        }

        /// <summary>設定オブジェクトが存在するかを返します。</summary>
        internal bool Exists { get; }

        /// <summary>設定アセットのGUIDを返します。</summary>
        internal string Guid { get; }

        /// <summary>設定アセットのプロジェクト相対パスを返します。</summary>
        internal string Path { get; }

        /// <summary>設定アセットの名前を返します。</summary>
        internal string Name { get; }

        /// <summary>切り替え後のシーン構成を順序付きの読み取り専用一覧で返します。</summary>
        internal IReadOnlyList<SceneWorkspaceSceneState> Scenes { get; }
    }
}
