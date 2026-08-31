using System;
using UnityEditor;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>一つのシーン参照と、切り替え後の読込状態・使用中状態を保存します。</summary>
    [Serializable]
    public sealed class SceneWorkspaceProfileEntry
    {
        /// <summary>切り替え対象となる保存済みシーンです。</summary>
        [SerializeField, InspectorName("シーン")] private SceneAsset scene;

        /// <summary>切り替え後にシーンを読み込む場合は有効です。</summary>
        [SerializeField, InspectorName("読み込む")] private bool loaded = true;

        /// <summary>切り替え後に使用中のシーンへ設定する場合は有効です。</summary>
        [SerializeField, InspectorName("使用中にする")] private bool active;

        /// <summary>参照先の保存済みシーンを返します。欠損している場合は未指定です。</summary>
        public SceneAsset Scene => scene;

        /// <summary>切り替え後に読み込む設定かを返します。</summary>
        public bool Loaded => loaded;

        /// <summary>切り替え後に使用中のシーンへ設定するかを返します。</summary>
        public bool Active => active;

        /// <summary>シーン参照と切り替え後の状態から一つの設定項目を作成します。</summary>
        internal SceneWorkspaceProfileEntry(SceneAsset scene, bool loaded, bool active)
        {
            this.scene = scene;
            this.loaded = loaded;
            this.active = active;
        }

        /// <summary>同じ値を持つ独立した設定項目を返します。</summary>
        internal SceneWorkspaceProfileEntry Clone()
        {
            return new SceneWorkspaceProfileEntry(scene, loaded, active);
        }
    }
}
