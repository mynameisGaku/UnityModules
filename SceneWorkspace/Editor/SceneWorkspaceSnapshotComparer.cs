using System;
using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>復元後の順番、同一性、読込状態、使用中状態、未変更状態が完全一致するかを確認します。</summary>
    internal static class SceneWorkspaceSnapshotComparer
    {
        internal static bool Matches(IReadOnlyList<SceneWorkspaceSceneState> expected, IReadOnlyList<SceneWorkspaceSceneState> actual, out string difference)
        {
            if (expected == null || actual == null)
            {
                difference = "比較するシーン構成を取得できません。";
                return false;
            }
            if (expected.Count != actual.Count)
            {
                difference = "シーン数が確認済みの構成と一致しません。";
                return false;
            }

            for (var index = 0; index < expected.Count; index++)
            {
                var wanted = expected[index];
                var found = actual[index];
                if (wanted == null || found == null || !wanted.HasSameSetup(found))
                {
                    difference = (index + 1) + "番目のシーンが確認済みの構成と一致しません。";
                    return false;
                }
                if (found.Dirty)
                {
                    difference = "シーン構成の復元中に、未保存の変更が発生したシーンがあります。";
                    return false;
                }
            }

            difference = string.Empty;
            return true;
        }
    }
}
