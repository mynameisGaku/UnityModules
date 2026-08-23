using System;
using System.IO;
using System.Security;

namespace BuildAssistant.Editor
{
    internal sealed class JsonExporter
    {
        private readonly BuildAssistantFileSystem fileSystem;
        private readonly string projectRoot;

        internal JsonExporter(string projectRoot, BuildAssistantFileSystem fileSystem = null)
        {
            this.projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            this.fileSystem = fileSystem ?? new BuildAssistantFileSystem();
        }

        internal BuildAssistantError Export(BuildAssistantHistoryEntry entry, string absolutePath)
        {
            if (entry == null || !LocationPolicy.IsFullyQualifiedPath(absolutePath))
                return BuildAssistantError.InvalidOutputRoot;
            string normalized;
            try
            {
                normalized = fileSystem.GetFullPath(absolutePath.Trim());
                if (!string.Equals(Path.GetExtension(normalized), ".json", StringComparison.OrdinalIgnoreCase))
                    return BuildAssistantError.InvalidOutputRoot;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return BuildAssistantError.InvalidOutputRoot;
            }

            try
            {
                if (fileSystem.FileExists(normalized) || fileSystem.DirectoryExists(normalized))
                    return BuildAssistantError.OutputAlreadyExists;
                var parent = Path.GetDirectoryName(normalized);
                if (string.IsNullOrEmpty(parent) || !fileSystem.DirectoryExists(parent))
                    return BuildAssistantError.InvalidOutputRoot;
                var parentInspection = new LocationPolicy(projectRoot, fileSystem).Inspect(parent);
                if (!parentInspection.IsValid)
                    return parentInspection.Error;
                using (var parentLease = fileSystem.AcquireDirectoryIdentityLease(parent))
                {
                    if (!LocationPolicy.CanonicalEquals(parentInspection.CanonicalPath, parentLease.CanonicalPath) || fileSystem.IsNetworkDrive(parent) || ContainsReparsePoint(parent))
                        return BuildAssistantError.UnsafeOutputPath;
                    if (fileSystem.FileExists(normalized) || fileSystem.DirectoryExists(normalized))
                        return BuildAssistantError.OutputAlreadyExists;
                    fileSystem.WriteAllTextFlushed(normalized, HistoryStore.SerializeExport(entry), FileMode.CreateNew);
                    return BuildAssistantError.None;
                }
            }
            catch (CreateNewFileCollisionException)
            {
                return BuildAssistantError.OutputAlreadyExists;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException)
            {
                return BuildAssistantError.HistoryWriteFailed;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException)
            {
                return BuildAssistantError.InvalidOutputRoot;
            }
        }

        private bool ContainsReparsePoint(string existingPath)
        {
            var current = existingPath;
            while (!string.IsNullOrEmpty(current))
            {
                if ((fileSystem.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || StringComparer.OrdinalIgnoreCase.Equals(parent, current))
                    break;
                current = parent;
            }
            return false;
        }
    }
}
