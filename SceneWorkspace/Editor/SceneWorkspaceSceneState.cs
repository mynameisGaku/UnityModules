using System;

namespace SceneWorkspace.Editor
{
    /// <summary>エディターの現在構成または作業セット設定から取得した、一つの独立したシーン状態を保持します。</summary>
    public sealed class SceneWorkspaceSceneState
    {
        /// <summary>位置、識別情報、存在状態、読込状態、使用中状態、未保存状態から記録を作成します。</summary>
        internal SceneWorkspaceSceneState(int index, string guid, string path, bool exists, bool loaded, bool active, bool dirty)
        {
            Index = index;
            Guid = guid ?? string.Empty;
            Path = path ?? string.Empty;
            Exists = exists;
            Loaded = loaded;
            Active = active;
            Dirty = dirty;
        }

        /// <summary>取得元の構成における零始まり位置を返します。</summary>
        public int Index { get; }

        /// <summary>シーンアセットのGUIDを返します。取得できない場合は空です。</summary>
        public string Guid { get; }

        /// <summary>シーンのプロジェクト相対パスを返します。無題の場合は空です。</summary>
        public string Path { get; }

        /// <summary>シーンアセットを参照できるかを返します。</summary>
        public bool Exists { get; }

        /// <summary>シーンが読み込み済みか、または切り替え後に読み込む設定かを返します。</summary>
        public bool Loaded { get; }

        /// <summary>使用中のシーンか、または切り替え後に使用中とする設定かを返します。</summary>
        public bool Active { get; }

        /// <summary>現在のシーンに未保存の変更があるかを返します。作業セット設定から取得した場合は無効です。</summary>
        public bool Dirty { get; }

        /// <summary>指定した零始まり位置へ差し替えた独立した記録を返します。</summary>
        internal SceneWorkspaceSceneState WithIndex(int index)
        {
            return new SceneWorkspaceSceneState(index, Guid, Path, Exists, Loaded, Active, Dirty);
        }

        /// <summary>GUID、パス、読込状態、使用中状態が一致するかを返します。未指定の比較先とは一致しません。</summary>
        internal bool HasSameSetup(SceneWorkspaceSceneState other)
        {
            return other != null
                && StringComparer.Ordinal.Equals(Guid, other.Guid)
                && StringComparer.Ordinal.Equals(Path, other.Path)
                && Loaded == other.Loaded
                && Active == other.Active;
        }
    }
}
