using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneWorkspace.Editor
{
    /// <summary>利用者が明示的に設定した、エディター専用の順序付きシーン構成を保存します。</summary>
    [CreateAssetMenu(fileName = "SceneWorkspaceProfile", menuName = "シーン作業セット/設定")]
    public sealed class SceneWorkspaceProfile : ScriptableObject
    {
        /// <summary>切り替え後に復元するシーンを希望順で保持します。</summary>
        [SerializeField, InspectorName("順序付きシーン構成")] private SceneWorkspaceProfileEntry[] entries = Array.Empty<SceneWorkspaceProfileEntry>();

        /// <summary>直列化配列を呼び出し側から変更できないよう、複製したシーン設定を読み取り専用で返します。</summary>
        public IReadOnlyList<SceneWorkspaceProfileEntry> Entries
        {
            get
            {
                var copy = new SceneWorkspaceProfileEntry[entries?.Length ?? 0];
                for (var index = 0; index < copy.Length; index++)
                    copy[index] = entries[index]?.Clone();
                return Array.AsReadOnly(copy);
            }
        }

        /// <summary>すべての項目を複製して置き換えます。未指定の場合は空の構成にします。</summary>
        internal void ReplaceEntries(SceneWorkspaceProfileEntry[] value)
        {
            var source = value ?? Array.Empty<SceneWorkspaceProfileEntry>();
            entries = new SceneWorkspaceProfileEntry[source.Length];
            for (var index = 0; index < source.Length; index++)
                entries[index] = source[index]?.Clone();
        }
    }
}
