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

        public ISet<string> GetAssetModuleFolders()
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < ModuleCatalog.Entries.Count; index++)
            {
                var folderName = ModuleCatalog.Entries[index].FolderName;
                if (AssetDatabase.IsValidFolder($"Assets/Modules/{folderName}"))
                {
                    folders.Add(folderName);
                }
            }

            return folders;
        }
    }
}
