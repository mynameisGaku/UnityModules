// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal interface IModuleInstallRequest
    {
        bool IsCompleted { get; }
        bool Succeeded { get; }
        string ErrorMessage { get; }
    }

    internal interface IModulePackageClient
    {
        IModuleInstallRequest AddAndRemove(IReadOnlyList<string> packageUrls);
    }

    internal interface IModuleInstallEnvironment
    {
        ISet<string> GetInstalledPackageNames();
        ISet<string> GetAssetModuleFolders();
    }

    internal interface IModuleInstallStateStore
    {
        string QueueJson { get; set; }
        string LastMessage { get; set; }
    }
}
