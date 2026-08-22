// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Editor
{
    internal static class ProjectSetupFolderUtility
    {
        internal const int MaximumFolderCount = 64;
        internal const int MaximumPathLength = 200;
        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        internal static bool TryNormalize(string value, out string path, out string error)
        {
            path = (value ?? string.Empty).Replace('\\', '/').Trim();
            error = string.Empty;
            if (path.Length == 0 || path.Length > MaximumPathLength)
            {
                error = $"Folder paths must contain 1 to {MaximumPathLength} characters.";
                return false;
            }

            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
            {
                error = "Folder paths must start with 'Assets/' and name a child folder.";
                return false;
            }

            var segments = path.Split('/');
            for (var index = 1; index < segments.Length; index++)
            {
                var segment = segments[index];
                if (!IsValidSegment(segment))
                {
                    error = $"Folder path segment '{segment}' is not valid.";
                    return false;
                }
            }

            return true;
        }

        internal static string[] ExpandMissingFolders(IEnumerable<string> requested, IEnumerable<string> currentFolders)
        {
            var existing = new HashSet<string>(currentFolders ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var requestedPath in requested ?? Array.Empty<string>())
            {
                var segments = requestedPath.Split('/');
                var path = segments[0];
                for (var index = 1; index < segments.Length; index++)
                {
                    path += "/" + segments[index];
                    if (!existing.Contains(path))
                    {
                        missing.Add(path);
                    }
                }
            }

            return missing
                .OrderBy(GetDepth)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static string[] GetRestorableFolders(
            IEnumerable<string> createdFolders,
            IEnumerable<string> currentFolders,
            IEnumerable<string> currentAssetPaths)
        {
            var folders = new HashSet<string>(currentFolders ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var created = new HashSet<string>(createdFolders ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var assets = (currentAssetPaths ?? Array.Empty<string>()).ToArray();
            var removable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in created.Where(folders.Contains).OrderByDescending(GetDepth).ThenBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                var prefix = path + "/";
                var canRemove = true;
                foreach (var descendant in assets.Where(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!folders.Contains(descendant) || !removable.Contains(descendant))
                    {
                        canRemove = false;
                        break;
                    }
                }

                if (canRemove)
                {
                    removable.Add(path);
                }
            }

            return removable.OrderByDescending(GetDepth).ThenBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static int GetDepth(string path)
        {
            return string.IsNullOrEmpty(path) ? 0 : path.Count(character => character == '/');
        }

        private static bool IsValidSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment)
                || segment == "."
                || segment == ".."
                || segment.EndsWith(" ", StringComparison.Ordinal)
                || segment.EndsWith(".", StringComparison.Ordinal)
                || ReservedNames.Contains(segment.Split('.')[0]))
            {
                return false;
            }

            for (var index = 0; index < segment.Length; index++)
            {
                var character = segment[index];
                if (character < 32 || character == '<' || character == '>' || character == ':' || character == '"'
                    || character == '|' || character == '?' || character == '*')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
