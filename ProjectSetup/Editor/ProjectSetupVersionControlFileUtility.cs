// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ProjectSetup.Editor
{
    internal static class ProjectSetupVersionControlFileUtility
    {
        internal const string GitIgnorePath = ".gitignore";
        internal const string GitAttributesPath = ".gitattributes";

        private static readonly string GitIgnoreContent = string.Join(
            "\n",
            new[]
            {
                "# Unity generated folders",
                "/[Ll]ibrary/",
                "/[Tt]emp/",
                "/[Oo]bj/",
                "/[Bb]uild/",
                "/[Bb]uilds/",
                "/[Ll]ogs/",
                "/[Uu]ser[Ss]ettings/",
                "/[Mm]emoryCaptures/",
                "/[Rr]ecordings/",
                string.Empty,
                "# Local Project Setup backup",
                "/ProjectSettings/ProjectSetupLastBackup.json",
                string.Empty,
                "# Generated IDE files",
                "/.vs/",
                "/.idea/",
                "*.csproj",
                "*.sln",
                "*.suo",
                "*.user",
                "*.userprefs",
                "*.pidb",
                "*.booproj",
                "*.svd",
                "*.sln.iml",
                string.Empty,
                "# Generated packages and diagnostics",
                "sysinfo.txt",
                "*.apk",
                "*.aab",
                "*.unitypackage"
            })
            + "\n";

        private static readonly string GitAttributesContent = string.Join(
            "\n",
            new[]
            {
                "* text=auto",
                "*.cs text eol=lf",
                "*.shader text eol=lf",
                "*.compute text eol=lf",
                "*.hlsl text eol=lf",
                "*.cginc text eol=lf",
                "*.asmdef text eol=lf",
                "*.asmref text eol=lf",
                "*.json text eol=lf",
                "*.meta text eol=lf",
                "*.unity text eol=lf",
                "*.prefab text eol=lf",
                "*.asset text eol=lf",
                "*.mat text eol=lf",
                "*.anim text eol=lf",
                "*.controller text eol=lf",
                "*.overrideController text eol=lf",
                "*.playable text eol=lf",
                "*.inputactions text eol=lf"
            })
            + "\n";

        internal static ProjectSetupVersionControlFilePlan[] BuildMissingFiles(IEnumerable<string> existingPaths)
        {
            var existing = new HashSet<string>(existingPaths ?? Array.Empty<string>(), StringComparer.Ordinal);
            var plans = new List<ProjectSetupVersionControlFilePlan>(2);
            if (!existing.Contains(GitIgnorePath))
            {
                plans.Add(new ProjectSetupVersionControlFilePlan(GitIgnorePath, GitIgnoreContent));
            }

            if (!existing.Contains(GitAttributesPath))
            {
                plans.Add(new ProjectSetupVersionControlFilePlan(GitAttributesPath, GitAttributesContent));
            }

            return plans.ToArray();
        }

        internal static bool IsSupportedPath(string path)
        {
            return string.Equals(path, GitIgnorePath, StringComparison.Ordinal)
                || string.Equals(path, GitAttributesPath, StringComparison.Ordinal);
        }
    }
}
