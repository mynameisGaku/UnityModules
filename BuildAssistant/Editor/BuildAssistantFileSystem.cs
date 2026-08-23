using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BuildAssistant.Editor
{
    internal sealed class CreateNewFileCollisionException : IOException
    {
        internal CreateNewFileCollisionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    internal sealed class CreateNewDirectoryCollisionException : IOException
    {
        internal CreateNewDirectoryCollisionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    internal class BuildAssistantFileSystem
    {
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint DeleteAccess = 0x00010000;
        private const uint FileReadAttributes = 0x00000080;
        private const uint VolumeNameGuid = 0x00000001;

        internal virtual bool FileExists(string path) => File.Exists(path);
        internal virtual bool DirectoryExists(string path) => Directory.Exists(path);
        internal virtual bool IsDirectoryEmpty(string path) => Directory.GetFileSystemEntries(path).Length == 0;
        internal virtual FileAttributes GetAttributes(string path) => File.GetAttributes(path);
        internal virtual string GetFullPath(string path) => Path.GetFullPath(path);
        internal virtual bool IsNetworkDrive(string path)
        {
            try
            {
                var drives = new List<KeyValuePair<string, DriveType>>();
                foreach (var drive in DriveInfo.GetDrives())
                    drives.Add(new KeyValuePair<string, DriveType>(drive.RootDirectory.FullName, drive.IsReady ? drive.DriveType : DriveType.Unknown));
                return !IsLocalFixedDrive(path, drives, Path.DirectorySeparatorChar);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException)
            {
                return true;
            }
        }

        internal static bool IsLocalFixedDrive(string path, IEnumerable<KeyValuePair<string, DriveType>> driveRoots, char directorySeparator)
        {
            if (string.IsNullOrWhiteSpace(path) || driveRoots == null)
                return false;
            var normalizedPath = NormalizeDrivePath(path, directorySeparator);
            var bestLength = -1;
            var bestType = DriveType.Unknown;
            foreach (var driveRoot in driveRoots)
            {
                var normalizedRoot = NormalizeDrivePath(driveRoot.Key, directorySeparator);
                if (normalizedRoot.Length == 0 || !IsPathOnDrive(normalizedPath, normalizedRoot, directorySeparator) || normalizedRoot.Length < bestLength)
                    continue;
                if (normalizedRoot.Length > bestLength)
                {
                    bestLength = normalizedRoot.Length;
                    bestType = driveRoot.Value;
                }
                else if (driveRoot.Value != DriveType.Fixed)
                {
                    bestType = DriveType.Unknown;
                }
            }
            return bestLength >= 0 && bestType == DriveType.Fixed;
        }

        internal virtual string GetCanonicalDirectoryPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException("The directory does not exist: " + fullPath);
            if (Path.DirectorySeparatorChar != '\\')
                return fullPath;

            using (var handle = CreateFileW(fullPath, 0, FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero, FileMode.Open, FileFlagBackupSemantics, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    throw CreateWindowsIOException("The directory identity could not be opened.");
                return ResolveCanonicalPath(handle);
            }
        }

        internal virtual void CreateDirectory(string path) => Directory.CreateDirectory(path);

        internal virtual void CreateDirectoryNew(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (Path.DirectorySeparatorChar == '\\')
            {
                if (CreateDirectoryW(fullPath, IntPtr.Zero))
                    return;
                var error = Marshal.GetLastWin32Error();
                var exception = new Win32Exception(error);
                if (error == 80 || error == 183)
                    throw new CreateNewDirectoryCollisionException("The create-new directory already exists.", exception);
                throw new IOException("The create-new directory could not be created. " + exception.Message, exception);
            }

            if (MakeDirectory(fullPath, 511) == 0)
                return;
            var unixError = Marshal.GetLastWin32Error();
            var unixException = new Win32Exception(unixError);
            if (unixError == 17)
                throw new CreateNewDirectoryCollisionException("The create-new directory already exists.", unixException);
            throw new IOException("The create-new directory could not be created. " + unixException.Message, unixException);
        }

        internal virtual DirectoryIdentityLease AcquireDirectoryIdentityLease(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (Path.DirectorySeparatorChar != '\\')
                return new DirectoryIdentityLease(GetCanonicalDirectoryPath(fullPath));
            var handle = CreateFileW(fullPath, DeleteAccess | FileReadAttributes, FileShare.Read | FileShare.Write, IntPtr.Zero, FileMode.Open, FileFlagBackupSemantics, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw CreateWindowsIOException("The directory identity lease could not be opened.");
            }

            try
            {
                return new DirectoryIdentityLease(ResolveCanonicalPath(handle), handle);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        internal virtual void DeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        internal virtual string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

        internal virtual void WriteAllTextFlushed(string path, string content, FileMode mode)
        {
            var created = false;
            try
            {
                using (var stream = new FileStream(path, mode, FileAccess.Write, FileShare.None))
                {
                    created = mode == FileMode.CreateNew;
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(content ?? string.Empty);
                        writer.Flush();
                        stream.Flush(true);
                    }
                }
            }
            catch (IOException exception) when (!created && mode == FileMode.CreateNew && File.Exists(path))
            {
                throw new CreateNewFileCollisionException("The create-new destination already exists.", exception);
            }
            catch
            {
                if (created && mode == FileMode.CreateNew)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        internal virtual void MoveFile(string source, string destination) => File.Move(source, destination);
        internal virtual void ReplaceFile(string source, string destination, string backup) => File.Replace(source, destination, backup, true);

        private static bool IsPathOnDrive(string path, string root, char directorySeparator)
        {
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
            if (string.Equals(path, root, comparison))
                return true;
            if (root.Length == 1 && root[0] == directorySeparator)
                return path.Length > 0 && path[0] == directorySeparator;
            return path.StartsWith(root + directorySeparator, comparison);
        }

        private static string NormalizeDrivePath(string path, char directorySeparator)
        {
            var alternateSeparator = directorySeparator == '/' ? '\\' : '/';
            var normalized = (path ?? string.Empty).Trim().Replace(alternateSeparator, directorySeparator);
            while (normalized.Length > 1 && normalized[normalized.Length - 1] == directorySeparator)
                normalized = normalized.Substring(0, normalized.Length - 1);
            return normalized;
        }

        private static IOException CreateWindowsIOException(string message)
        {
            var error = Marshal.GetLastWin32Error();
            return new IOException(message + " " + new Win32Exception(error).Message, new Win32Exception(error));
        }

        private static string ResolveCanonicalPath(SafeFileHandle handle)
        {
            var capacity = 512;
            while (true)
            {
                var buffer = new StringBuilder(capacity);
                var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, VolumeNameGuid);
                if (length == 0)
                    throw CreateWindowsIOException("The directory identity could not be resolved.");
                if (length < buffer.Capacity)
                    return buffer.ToString();
                capacity = checked((int)length + 1);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, FileShare shareMode, IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint pathLength, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDirectoryW(string path, IntPtr securityAttributes);

        [DllImport("libc", EntryPoint = "mkdir", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern int MakeDirectory(string path, uint mode);
    }
}
