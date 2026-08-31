using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BuildAssistant.Editor;

namespace BuildAssistant.Tests
{
    internal sealed class FakeBuildAssistantFileSystem : BuildAssistantFileSystem
    {
        private readonly HashSet<string> directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FileAttributes> attributes = new Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> canonicalPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> directoryIdentities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> physicalLocations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> networkRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> getAttributesCallCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal bool ThrowOnReplace { get; set; }
        internal string ThrowOnReplacePath { get; set; }
        internal string ThrowOnWritePathPrefix { get; set; }
        internal string ThrowOnDeletePath { get; set; }
        internal Exception FileExistsException { get; set; }
        internal string FileExistsExceptionPath { get; set; }
        internal Exception ReadAllTextBoundedException { get; set; }
        internal string ReadAllTextBoundedExceptionPath { get; set; }
        internal Exception DirectoryExistsException { get; set; }
        internal Exception GetAttributesException { get; set; }
        internal string UnexpectedGetAttributesPath { get; set; }
        internal int UnexpectedGetAttributesCall { get; set; }
        internal string InjectCreateNewDirectoryCollisionPath { get; set; }

        internal FakeBuildAssistantFileSystem(params string[] existingDirectories)
        {
            foreach (var directory in existingDirectories)
                AddDirectory(directory);
        }

        internal void AddDirectory(string path)
        {
            var fullPath = GetFullPath(path);
            directories.Add(fullPath);
            attributes[fullPath] = FileAttributes.Directory;
        }

        internal void SetFile(string path, string content)
        {
            files[GetFullPath(path)] = content;
        }

        internal string GetFile(string path) => files[GetFullPath(path)];

        internal void MarkReparse(string path)
        {
            var fullPath = GetFullPath(path);
            AddDirectory(fullPath);
            attributes[fullPath] = FileAttributes.Directory | FileAttributes.ReparsePoint;
        }

        internal void SetCanonicalPath(string path, string canonicalPath)
        {
            canonicalPaths[GetFullPath(path)] = GetFullPath(canonicalPath);
        }

        internal void SetDirectoryIdentity(string path, string identity)
        {
            directoryIdentities[GetFullPath(path)] = identity ?? string.Empty;
        }

        internal void SetPhysicalLocation(string path, string fileSystemId, string internalPath)
        {
            physicalLocations[GetFullPath(path)] = (fileSystemId ?? string.Empty) + "\n" + (internalPath ?? string.Empty);
        }

        internal void MarkNetworkDrive(string path)
        {
            networkRoots.Add(GetFullPath(path));
        }

        internal int TemporaryFileCount
        {
            get
            {
                var count = 0;
                foreach (var path in files.Keys)
                {
                    if (path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                        count++;
                }

                return count;
            }
        }

        internal override bool FileExists(string path)
        {
            if (FileExistsException != null && (string.IsNullOrEmpty(FileExistsExceptionPath) || StringComparer.OrdinalIgnoreCase.Equals(GetFullPath(path), GetFullPath(FileExistsExceptionPath))))
                throw FileExistsException;
            return files.ContainsKey(GetFullPath(path));
        }

        internal override bool DirectoryExists(string path)
        {
            if (DirectoryExistsException != null)
                throw DirectoryExistsException;
            return directories.Contains(GetFullPath(path));
        }

        internal override bool IsDirectoryEmpty(string path)
        {
            var fullPath = GetFullPath(path);
            foreach (var directory in directories)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(directory, fullPath) && StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(directory), fullPath))
                    return false;
            }
            foreach (var file in files.Keys)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(file), fullPath))
                    return false;
            }
            return true;
        }

        internal override FileAttributes GetAttributes(string path)
        {
            if (GetAttributesException != null)
                throw GetAttributesException;
            var fullPath = GetFullPath(path);
            getAttributesCallCounts.TryGetValue(fullPath, out var previousCallCount);
            var currentCallCount = previousCallCount + 1;
            getAttributesCallCounts[fullPath] = currentCallCount;
            if (!string.IsNullOrEmpty(UnexpectedGetAttributesPath) && StringComparer.OrdinalIgnoreCase.Equals(fullPath, GetFullPath(UnexpectedGetAttributesPath)) && currentCallCount == UnexpectedGetAttributesCall)
                throw new InvalidOperationException("Injected unexpected attribute failure.");
            return attributes.TryGetValue(fullPath, out var value) ? value : FileAttributes.Directory;
        }

        internal override string GetFullPath(string path) => Path.GetFullPath(path);

        internal override bool IsNetworkDrive(string path)
        {
            var fullPath = GetFullPath(path);
            foreach (var networkRoot in networkRoots)
            {
                if (SafeBuildOutput.IsContained(networkRoot, fullPath))
                    return true;
            }
            return false;
        }

        internal override string GetCanonicalDirectoryPath(string path)
        {
            var fullPath = GetFullPath(path);
            if (!directories.Contains(fullPath))
                throw new DirectoryNotFoundException(fullPath);
            return canonicalPaths.TryGetValue(fullPath, out var canonicalPath) ? canonicalPath : fullPath;
        }

        internal override string GetDirectoryIdentity(string path)
        {
            var fullPath = GetFullPath(path);
            if (!directories.Contains(fullPath))
                throw new DirectoryNotFoundException(fullPath);
            if (directoryIdentities.TryGetValue(fullPath, out var identity))
                return identity;
            return "fake:" + GetCanonicalDirectoryPath(fullPath).ToUpperInvariant();
        }

        internal override bool TryGetPhysicalDirectoryLocation(string path, out string fileSystemId, out string internalPath)
        {
            if (!physicalLocations.TryGetValue(GetFullPath(path), out var value))
            {
                fileSystemId = string.Empty;
                internalPath = string.Empty;
                return false;
            }
            var separator = value.IndexOf('\n');
            fileSystemId = separator < 0 ? value : value.Substring(0, separator);
            internalPath = separator < 0 ? string.Empty : value.Substring(separator + 1);
            return true;
        }

        internal override void CreateDirectory(string path) => AddDirectory(path);

        internal override void CreateDirectoryNew(string path)
        {
            var fullPath = GetFullPath(path);
            if (!string.IsNullOrEmpty(InjectCreateNewDirectoryCollisionPath) && StringComparer.OrdinalIgnoreCase.Equals(fullPath, GetFullPath(InjectCreateNewDirectoryCollisionPath)))
            {
                AddDirectory(fullPath);
                throw new CreateNewDirectoryCollisionException("Injected create-new directory collision.", new IOException("Injected collision."));
            }
            if (directories.Contains(fullPath) || files.ContainsKey(fullPath))
                throw new CreateNewDirectoryCollisionException("Directory exists.", new IOException("Injected collision."));
            AddDirectory(fullPath);
        }

        internal override DirectoryIdentityLease AcquireDirectoryIdentityLease(string path) => new DirectoryIdentityLease(GetCanonicalDirectoryPath(path));

        internal override void DeleteFile(string path)
        {
            var fullPath = GetFullPath(path);
            if (!string.IsNullOrEmpty(ThrowOnDeletePath) && StringComparer.OrdinalIgnoreCase.Equals(fullPath, GetFullPath(ThrowOnDeletePath)))
                throw new IOException("Injected delete failure.");
            files.Remove(fullPath);
        }

        internal override string ReadAllText(string path)
        {
            if (!files.TryGetValue(GetFullPath(path), out var content))
                throw new FileNotFoundException();
            return content;
        }

        internal override string ReadAllTextBounded(string path, int maximumBytes)
        {
            if (ReadAllTextBoundedException != null && (string.IsNullOrEmpty(ReadAllTextBoundedExceptionPath) || StringComparer.OrdinalIgnoreCase.Equals(GetFullPath(path), GetFullPath(ReadAllTextBoundedExceptionPath))))
                throw ReadAllTextBoundedException;
            var content = ReadAllText(path);
            if (Encoding.UTF8.GetByteCount(content) > maximumBytes)
                throw new InvalidDataException("Injected bounded read limit.");
            return content;
        }

        internal override void WriteAllTextFlushed(string path, string content, FileMode mode)
        {
            var fullPath = GetFullPath(path);
            if (!string.IsNullOrEmpty(ThrowOnWritePathPrefix) && fullPath.StartsWith(GetFullPath(ThrowOnWritePathPrefix), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Injected write failure.");
            if (mode == FileMode.CreateNew && files.ContainsKey(fullPath))
                throw new CreateNewFileCollisionException("File exists.", new IOException("Injected collision."));
            files[fullPath] = content ?? string.Empty;
        }

        internal override void MoveFile(string source, string destination)
        {
            var sourcePath = GetFullPath(source);
            var destinationPath = GetFullPath(destination);
            if (!files.TryGetValue(sourcePath, out var content) || files.ContainsKey(destinationPath))
                throw new IOException("Move failed.");
            files[destinationPath] = content;
            files.Remove(sourcePath);
        }

        internal override void ReplaceFile(string source, string destination, string backup)
        {
            if (ThrowOnReplace || (!string.IsNullOrEmpty(ThrowOnReplacePath) && StringComparer.OrdinalIgnoreCase.Equals(GetFullPath(destination), GetFullPath(ThrowOnReplacePath))))
                throw new IOException("Injected replace failure.");
            var sourcePath = GetFullPath(source);
            var destinationPath = GetFullPath(destination);
            var backupPath = GetFullPath(backup);
            if (!files.TryGetValue(sourcePath, out var replacement) || !files.TryGetValue(destinationPath, out var original))
                throw new IOException("Replace failed.");
            files[backupPath] = original;
            files[destinationPath] = replacement;
            files.Remove(sourcePath);
        }
    }
}
