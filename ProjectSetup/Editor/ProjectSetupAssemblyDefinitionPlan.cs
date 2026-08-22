// SPDX-License-Identifier: MIT

namespace ProjectSetup.Editor
{
    internal readonly struct ProjectSetupAssemblyDefinitionPlan
    {
        internal ProjectSetupAssemblyDefinitionPlan(string path, string content)
        {
            Path = path;
            Content = content;
            ContentHash = ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(content);
        }

        internal string Path { get; }

        internal string Content { get; }

        internal string ContentHash { get; }

        internal ProjectSetupCreatedAsset ToCreatedAsset()
        {
            return new ProjectSetupCreatedAsset(Path, ContentHash);
        }
    }
}
