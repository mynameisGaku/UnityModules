using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SceneWorkspace.Editor
{
    /// <summary>現在構成と変更可能な設定値から、長さ付きで安定した指紋値を作成します。</summary>
    internal static class SceneWorkspaceFingerprint
    {
        /// <summary>現在構成の順番、識別情報、状態をSHA-256指紋値へ変換します。</summary>
        internal static string ComputeCurrent(IReadOnlyList<SceneWorkspaceSceneState> scenes)
        {
            var builder = new StringBuilder();
            Append(builder, "current-v1");
            AppendScenes(builder, scenes, true);
            return Hash(builder.ToString());
        }

        /// <summary>設定の識別情報、名前、目標構成をSHA-256指紋値へ変換します。</summary>
        internal static string ComputeProfile(SceneWorkspaceProfileSnapshot profile)
        {
            var builder = new StringBuilder();
            Append(builder, "profile-v1");
            Append(builder, profile?.Guid);
            Append(builder, profile?.Path);
            Append(builder, profile?.Name);
            AppendScenes(builder, profile?.Scenes, false);
            return Hash(builder.ToString());
        }

        /// <summary>シーン数と各シーンの順序付き値を長さ付きで追記します。</summary>
        private static void AppendScenes(StringBuilder builder, IReadOnlyList<SceneWorkspaceSceneState> scenes, bool includeDirty)
        {
            Append(builder, (scenes?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
            if (scenes == null)
                return;
            for (var index = 0; index < scenes.Count; index++)
            {
                var scene = scenes[index];
                Append(builder, index.ToString(CultureInfo.InvariantCulture));
                Append(builder, scene?.Guid);
                Append(builder, scene?.Path);
                Append(builder, scene != null && scene.Exists ? "1" : "0");
                Append(builder, scene != null && scene.Loaded ? "1" : "0");
                Append(builder, scene != null && scene.Active ? "1" : "0");
                if (includeDirty)
                    Append(builder, scene != null && scene.Dirty ? "1" : "0");
            }
        }

        /// <summary>文字数と値を区切り付きで追記し、値内の区切り文字との衝突を防ぎます。</summary>
        private static void Append(StringBuilder builder, string value)
        {
            var safe = value ?? string.Empty;
            builder.Append(safe.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(safe);
            builder.Append('|');
        }

        /// <summary>文字列を小文字の十六進SHA-256指紋値へ変換します。</summary>
        private static string Hash(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var item in hash)
                    builder.Append(item.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
