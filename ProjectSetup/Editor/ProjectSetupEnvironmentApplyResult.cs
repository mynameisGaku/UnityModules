// SPDX-License-Identifier: MIT

using System;

namespace ProjectSetup.Editor
{
    internal readonly struct ProjectSetupEnvironmentApplyResult
    {
        internal ProjectSetupEnvironmentApplyResult(string[] createdFolders, ProjectSetupCreatedAsset[] createdAssets)
        {
            CreatedFolders = createdFolders ?? Array.Empty<string>();
            CreatedAssets = createdAssets ?? Array.Empty<ProjectSetupCreatedAsset>();
        }

        internal string[] CreatedFolders { get; }

        internal ProjectSetupCreatedAsset[] CreatedAssets { get; }

        internal static ProjectSetupEnvironmentApplyResult Empty { get; }
            = new ProjectSetupEnvironmentApplyResult(Array.Empty<string>(), Array.Empty<ProjectSetupCreatedAsset>());
    }
}
