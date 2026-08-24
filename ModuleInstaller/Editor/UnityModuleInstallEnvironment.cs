// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;

namespace ModuleInstaller.Editor
{
    internal sealed class UnityModuleInstallEnvironment : IModuleInstallEnvironment
    {
        public ISet<string> GetInstalledPackageNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            for (var index = 0; index < packages.Length; index++)
            {
                names.Add(packages[index].name);
            }

            return names;
        }

        public IReadOnlyDictionary<string, string> GetInstalledPackageVersions()
        {
            var versions = new Dictionary<string, string>(StringComparer.Ordinal);
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            for (var index = 0; index < packages.Length; index++)
            {
                versions[packages[index].name] = packages[index].version ?? string.Empty;
            }

            return versions;
        }

        public ISet<string> GetAssetModuleFolders()
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < ModuleCatalog.Entries.Count; index++)
            {
                var entry = ModuleCatalog.Entries[index];
                AddIfPresent(folders, entry.FolderName);
                for (var legacyIndex = 0; legacyIndex < entry.LegacyFolderNames.Count; legacyIndex++)
                {
                    AddIfPresent(folders, entry.LegacyFolderNames[legacyIndex]);
                }
            }

            return folders;
        }

        private static void AddIfPresent(ISet<string> folders, string folderName)
        {
            if (AssetDatabase.IsValidFolder($"Assets/Modules/{folderName}"))
            {
                folders.Add(folderName);
            }
        }
    }
}
