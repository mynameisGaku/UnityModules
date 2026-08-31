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
                if (normalizedRoot.Length == 0)
                    continue;
                var exactMatch = IsPathOnDrive(normalizedPath, normalizedRoot, directorySeparator, GetPathComparison(directorySeparator));
                var ambiguousUnixMatch = directorySeparator != '\\' && !exactMatch && IsPathOnDrive(normalizedPath, normalizedRoot, directorySeparator, StringComparison.OrdinalIgnoreCase);
                if (ambiguousUnixMatch && driveRoot.Value != DriveType.Fixed)
                    return false;
                if (!exactMatch || normalizedRoot.Length < bestLength)
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
                throw new DirectoryNotFoundException("フォルダーが存在しません: " + fullPath);
            if (Path.DirectorySeparatorChar != '\\')
                return fullPath;

            using (var handle = CreateFileW(fullPath, 0, FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero, FileMode.Open, FileFlagBackupSemantics, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    throw CreateWindowsIOException("フォルダーの物理識別子を開けませんでした。");
                return ResolveCanonicalPath(handle);
            }
        }

        /// <summary>同じ実在フォルダーを別経路から参照しても一致する識別子を返します。</summary>
        internal virtual string GetDirectoryIdentity(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException("フォルダーが存在しません: " + fullPath);
            if (Path.DirectorySeparatorChar == '\\')
                return "windows:" + GetCanonicalDirectoryPath(fullPath).ToUpperInvariant();
            if (IntPtr.Size != 8 || (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)))
                throw new PlatformNotSupportedException("この環境ではフォルダーの物理識別子を安全に取得できません。");

            var buffer = Marshal.AllocHGlobal(512);
            try
            {
                if (GetUnixFileStatus(fullPath, buffer) != 0)
                    throw CreateUnixIOException("フォルダーの物理識別子を取得できませんでした。");
                var deviceLength = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 4 : 8;
                var device = new byte[deviceLength];
                var inode = new byte[8];
                Marshal.Copy(buffer, device, 0, device.Length);
                Marshal.Copy(IntPtr.Add(buffer, 8), inode, 0, inode.Length);
                return "unix:" + BitConverter.ToString(device) + ":" + BitConverter.ToString(inode);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>Linuxのマウント別名を、同じファイルシステム内の元の位置へ対応付けます。</summary>
        internal virtual bool TryGetPhysicalDirectoryLocation(string path, out string fileSystemId, out string internalPath)
        {
            fileSystemId = string.Empty;
            internalPath = string.Empty;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return false;
            var mountInformation = File.ReadAllText("/proc/self/mountinfo", Encoding.UTF8);
            return TryResolveLinuxMountLocation(Path.GetFullPath(path), mountInformation, out fileSystemId, out internalPath);
        }

        /// <summary>試験可能なLinuxマウント情報から、ファイルシステム内の位置を解決します。</summary>
        internal static bool TryResolveLinuxMountLocation(string path, string mountInformation, out string fileSystemId, out string internalPath)
        {
            fileSystemId = string.Empty;
            internalPath = string.Empty;
            if (string.IsNullOrEmpty(path) || path[0] != '/' || string.IsNullOrEmpty(mountInformation))
                return false;

            var bestMountPoint = string.Empty;
            var bestRoot = string.Empty;
            var bestFileSystem = string.Empty;
            foreach (var rawLine in mountInformation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = rawLine.Split(' ');
                if (fields.Length < 6)
                    continue;
                var root = DecodeLinuxMountField(fields[3]);
                var mountPoint = DecodeLinuxMountField(fields[4]);
                if (!IsUnixPathContained(mountPoint, path) || mountPoint.Length < bestMountPoint.Length)
                    continue;
                bestMountPoint = mountPoint;
                bestRoot = root;
                bestFileSystem = fields[2];
            }
            if (bestMountPoint.Length == 0 || bestFileSystem.Length == 0)
                return false;

            var relative = path.Length == bestMountPoint.Length ? string.Empty : path.Substring(bestMountPoint.Length).TrimStart('/');
            fileSystemId = bestFileSystem;
            internalPath = NormalizeUnixPhysicalPath(bestRoot + "/" + relative);
            return true;
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
                    throw new CreateNewDirectoryCollisionException("新規作成するフォルダーが既に存在します。", exception);
                throw new IOException("新しいフォルダーを作成できませんでした。", exception);
            }

            if (MakeDirectory(fullPath, 511) == 0)
                return;
            var unixError = Marshal.GetLastWin32Error();
            var unixException = new Win32Exception(unixError);
            if (unixError == 17)
                throw new CreateNewDirectoryCollisionException("新規作成するフォルダーが既に存在します。", unixException);
            throw new IOException("新しいフォルダーを作成できませんでした。", unixException);
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
                throw CreateWindowsIOException("フォルダーの物理識別子を保持できませんでした。");
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

        /// <summary>同時に内容が増えた場合も、指定容量を超えて読み込まずUTF-8文字列へ変換します。</summary>
        internal virtual string ReadAllTextBounded(string path, int maximumBytes)
        {
            if (maximumBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                return ReadStreamTextBounded(stream, maximumBytes);
        }

        /// <summary>開始時の長さに依存せず、入力保持領域を指定容量以下に固定して文字列へ変換します。</summary>
        internal static string ReadStreamTextBounded(Stream stream, int maximumBytes)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (maximumBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            if (stream.CanSeek && stream.Length > maximumBytes)
                throw new InvalidDataException("読み込み対象が安全に扱える最大容量を超えています。");

            var content = maximumBytes == 0 ? Array.Empty<byte>() : new byte[maximumBytes];
            var length = 0;
            while (length < maximumBytes)
            {
                var read = stream.Read(content, length, maximumBytes - length);
                if (read == 0)
                    break;
                length += read;
            }
            if (length == maximumBytes && stream.ReadByte() != -1)
                throw new InvalidDataException("読み込み中に対象が安全に扱える最大容量を超えました。");
            var text = DecodeBoundedText(content, length);
            if (Encoding.UTF8.GetByteCount(text) > maximumBytes)
                throw new InvalidDataException("UTF-8へ復元した内容が安全に扱える最大容量を超えています。");
            return text;
        }

        /// <summary>従来の全読み込みと同じ文字順印を認識し、不正な文字列は履歴として受理しません。</summary>
        private static string DecodeBoundedText(byte[] content, int length)
        {
            Encoding encoding = new UTF8Encoding(false, true);
            var offset = 0;
            if (length >= 4 && content[0] == 0xff && content[1] == 0xfe && content[2] == 0x00 && content[3] == 0x00)
            {
                encoding = new UTF32Encoding(false, false, true);
                offset = 4;
            }
            else if (length >= 4 && content[0] == 0x00 && content[1] == 0x00 && content[2] == 0xfe && content[3] == 0xff)
            {
                encoding = new UTF32Encoding(true, false, true);
                offset = 4;
            }
            else if (length >= 3 && content[0] == 0xef && content[1] == 0xbb && content[2] == 0xbf)
            {
                offset = 3;
            }
            else if (length >= 2 && content[0] == 0xff && content[1] == 0xfe)
            {
                encoding = new UnicodeEncoding(false, false, true);
                offset = 2;
            }
            else if (length >= 2 && content[0] == 0xfe && content[1] == 0xff)
            {
                encoding = new UnicodeEncoding(true, false, true);
                offset = 2;
            }

            try
            {
                return encoding.GetString(content, offset, length - offset);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("読み込み対象の文字コードが壊れています。", exception);
            }
        }

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
                throw new CreateNewFileCollisionException("新規作成するファイルが既に存在します。", exception);
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

        private static bool IsPathOnDrive(string path, string root, char directorySeparator, StringComparison comparison)
        {
            if (string.Equals(path, root, comparison))
                return true;
            if (root.Length == 1 && root[0] == directorySeparator)
                return path.Length > 0 && path[0] == directorySeparator;
            return path.StartsWith(root + directorySeparator, comparison);
        }

        /// <summary>経路区切り文字に対応する大文字と小文字の比較規則を返します。</summary>
        internal static StringComparison GetPathComparison(char directorySeparator) => directorySeparator == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static string NormalizeDrivePath(string path, char directorySeparator)
        {
            var alternateSeparator = directorySeparator == '/' ? '\\' : '/';
            var normalized = (path ?? string.Empty).Trim().Replace(alternateSeparator, directorySeparator);
            while (normalized.Length > 1 && normalized[normalized.Length - 1] == directorySeparator)
                normalized = normalized.Substring(0, normalized.Length - 1);
            return normalized;
        }

        private static string DecodeLinuxMountField(string value)
        {
            return (value ?? string.Empty).Replace("\\040", " ").Replace("\\011", "\t").Replace("\\012", "\n").Replace("\\134", "\\");
        }

        private static bool IsUnixPathContained(string boundary, string candidate)
        {
            var normalizedBoundary = NormalizeUnixPhysicalPath(boundary);
            var normalizedCandidate = NormalizeUnixPhysicalPath(candidate);
            if (StringComparer.Ordinal.Equals(normalizedBoundary, normalizedCandidate))
                return true;
            return normalizedBoundary == "/" ? normalizedCandidate.StartsWith("/", StringComparison.Ordinal) : normalizedCandidate.StartsWith(normalizedBoundary + "/", StringComparison.Ordinal);
        }

        private static string NormalizeUnixPhysicalPath(string value)
        {
            var segments = new List<string>();
            foreach (var segment in (value ?? string.Empty).Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                    continue;
                if (segment == "..")
                {
                    if (segments.Count > 0)
                        segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(segment);
            }
            return segments.Count == 0 ? "/" : "/" + string.Join("/", segments);
        }

        private static IOException CreateWindowsIOException(string message)
        {
            var error = Marshal.GetLastWin32Error();
            return new IOException(message, new Win32Exception(error));
        }

        private static IOException CreateUnixIOException(string message)
        {
            var error = Marshal.GetLastWin32Error();
            return new IOException(message, new Win32Exception(error));
        }

        private static string ResolveCanonicalPath(SafeFileHandle handle)
        {
            var capacity = 512;
            while (true)
            {
                var buffer = new StringBuilder(capacity);
                var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, VolumeNameGuid);
                if (length == 0)
                    throw CreateWindowsIOException("フォルダーの物理識別子を解決できませんでした。");
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

        [DllImport("libc", EntryPoint = "stat", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern int GetUnixFileStatus(string path, IntPtr buffer);
    }
}
