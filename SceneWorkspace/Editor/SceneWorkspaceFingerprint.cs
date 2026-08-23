using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SceneWorkspace.Editor
{
    /// <summary>Creates stable length-prefixed hashes for current setups and mutable profile values.</summary>
    internal static class SceneWorkspaceFingerprint
    {
        internal static string ComputeCurrent(IReadOnlyList<SceneWorkspaceSceneState> scenes)
        {
            var builder = new StringBuilder();
            Append(builder, "current-v1");
            AppendScenes(builder, scenes, true);
            return Hash(builder.ToString());
        }

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

        private static void Append(StringBuilder builder, string value)
        {
            var safe = value ?? string.Empty;
            builder.Append(safe.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(safe);
            builder.Append('|');
        }

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
