// SPDX-License-Identifier: MIT

using System;

namespace ProjectSetup.Editor
{
    internal readonly struct ProjectSetupEnvironmentApplyResult
    {
        internal ProjectSetupEnvironmentApplyResult(
            string[] createdFolders,
            ProjectSetupCreatedAsset[] createdAssets,
            ProjectSetupCreatedRootFile[] createdRootFiles = null)
        {
            CreatedFolders = createdFolders ?? Array.Empty<string>();
            CreatedAssets = createdAssets ?? Array.Empty<ProjectSetupCreatedAsset>();
            CreatedRootFiles = createdRootFiles ?? Array.Empty<ProjectSetupCreatedRootFile>();
        }

        internal string[] CreatedFolders { get; }

        internal ProjectSetupCreatedAsset[] CreatedAssets { get; }

        internal ProjectSetupCreatedRootFile[] CreatedRootFiles { get; }

        internal static ProjectSetupEnvironmentApplyResult Empty { get; }
            = new ProjectSetupEnvironmentApplyResult(
                Array.Empty<string>(),
                Array.Empty<ProjectSetupCreatedAsset>(),
                Array.Empty<ProjectSetupCreatedRootFile>());
    }
}
