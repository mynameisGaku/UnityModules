// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProjectSetup.Editor
{
    internal sealed class ProjectSetupVersionControlFileStore
    {
        private readonly string _projectRoot;

        internal ProjectSetupVersionControlFileStore(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            }

            _projectRoot = Path.GetFullPath(projectRoot);
        }

        internal string[] CapturePaths()
        {
            var paths = new List<string>(2);
            AddWhenOccupied(paths, ProjectSetupVersionControlFileUtility.GitIgnorePath);
            AddWhenOccupied(paths, ProjectSetupVersionControlFileUtility.GitAttributesPath);
            return paths.ToArray();
        }

        internal ProjectSetupCreatedRootFile[] Create(ProjectSetupVersionControlFilePlan[] plans)
        {
            var created = new List<ProjectSetupCreatedRootFile>();
            try
            {
                foreach (var plan in plans ?? Array.Empty<ProjectSetupVersionControlFilePlan>())
                {
                    var fullPath = GetSupportedFullPath(plan.Path);
                    if (File.Exists(fullPath) || Directory.Exists(fullPath))
                    {
                        throw new InvalidOperationException($"The version control target '{plan.Path}' already exists.");
                    }

                    using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(plan.Content);
                        writer.Flush();
                        stream.Flush(true);
                    }

                    created.Add(new ProjectSetupCreatedRootFile(
                        plan.Path,
                        ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(File.ReadAllBytes(fullPath))));
                }

                return created.ToArray();
            }
            catch
            {
                Restore(created.ToArray());
                throw;
            }
        }

        internal void Restore(ProjectSetupCreatedRootFile[] createdFiles)
        {
            foreach (var createdFile in createdFiles ?? Array.Empty<ProjectSetupCreatedRootFile>())
            {
                var fullPath = GetSupportedFullPath(createdFile.Path);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var currentHash = ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(File.ReadAllBytes(fullPath));
                if (string.Equals(currentHash, createdFile.ContentHash, StringComparison.Ordinal))
                {
                    File.Delete(fullPath);
                }
            }
        }

        private void AddWhenOccupied(ICollection<string> paths, string relativePath)
        {
            var fullPath = GetSupportedFullPath(relativePath);
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                paths.Add(relativePath);
            }
        }

        private string GetSupportedFullPath(string relativePath)
        {
            if (!ProjectSetupVersionControlFileUtility.IsSupportedPath(relativePath))
            {
                throw new InvalidOperationException($"Unsupported project root file '{relativePath}'.");
            }

            return Path.Combine(_projectRoot, relativePath);
        }
    }
}
