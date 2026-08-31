// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;

namespace ModuleInstaller.Editor
{
    [InitializeOnLoad]
    internal static class ModuleInstallDriver
    {
        private static readonly UnityModuleInstallEnvironment Environment = new UnityModuleInstallEnvironment();
        private static readonly ModuleInstallCoordinator Coordinator = new ModuleInstallCoordinator(
            new UnityModulePackageClient(),
            Environment,
            new ModuleInstallSessionStore());

        static ModuleInstallDriver()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        internal static event Action Changed;

        internal static bool IsBusy => Coordinator.IsBusy;
        internal static string LastMessage => Coordinator.LastMessage;

        internal static ModuleInstallPlan BuildPlan(IEnumerable<string> packageNames)
        {
            return ModuleInstallPlanner.Build(
                packageNames,
                Environment.GetInstalledPackageNames(),
                Environment.GetAssetModuleFolders());
        }

        internal static ModuleInstallPlan BuildUpdatePlan()
        {
            var packageNames = new string[ModuleCatalog.Entries.Count];
            for (var index = 0; index < ModuleCatalog.Entries.Count; index++)
            {
                packageNames[index] = ModuleCatalog.Entries[index].PackageName;
            }

            return ModuleInstallPlanner.BuildUpdates(
                packageNames,
                Environment.GetInstalledPackageVersions(),
                Environment.GetAssetModuleFolders());
        }

        internal static bool TryInstallBundle(string bundleId, out string message)
        {
            if (!ModuleCatalog.TryFindBundle(bundleId, out var bundle))
            {
                message = $"一覧にない機能セットです：{bundleId}";
                return false;
            }

            return TryInstall(bundle.PackageNames, out message);
        }

        internal static bool TryInstallPackage(string packageName, out string message)
        {
            return TryInstall(new[] { packageName }, out message);
        }

        internal static bool TryUpdateInstalled(out string message)
        {
            var result = Coordinator.TryStartUpdates(BuildUpdatePlan(), out message);
            Changed?.Invoke();
            return result;
        }

        private static bool TryInstall(IEnumerable<string> packageNames, out string message)
        {
            var result = Coordinator.TryStart(BuildPlan(packageNames), out message);
            Changed?.Invoke();
            return result;
        }

        private static void Update()
        {
            if (!Coordinator.IsBusy)
            {
                return;
            }

            var previousMessage = Coordinator.LastMessage;
            Coordinator.Tick();
            if (!Coordinator.IsBusy
                || !string.Equals(previousMessage, Coordinator.LastMessage, StringComparison.Ordinal))
            {
                Changed?.Invoke();
            }
        }
    }
}
