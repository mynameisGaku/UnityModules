// SPDX-License-Identifier: MIT

namespace ProjectSetup.Editor
{
    internal readonly struct ProjectSetupVersionControlFilePlan
    {
        internal ProjectSetupVersionControlFilePlan(string path, string content)
        {
            Path = path;
            Content = content;
            ContentHash = ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(content);
        }

        internal string Path { get; }

        internal string Content { get; }

        internal string ContentHash { get; }

        internal ProjectSetupCreatedRootFile ToCreatedRootFile()
        {
            return new ProjectSetupCreatedRootFile(Path, ContentHash);
        }
    }
}
