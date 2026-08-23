using System;
using System.IO;
using System.Security;

namespace BuildAssistant.Editor
{
    internal sealed class LocationInspection
    {
        internal LocationInspection(BuildAssistantError error, string message, string normalizedPath, string canonicalPath, OutputRootMode mode)
        {
            Error = error;
            Message = message ?? string.Empty;
            NormalizedPath = normalizedPath ?? string.Empty;
            CanonicalPath = canonicalPath ?? string.Empty;
            Mode = mode;
        }

        internal BuildAssistantError Error { get; }
        internal string Message { get; }
        internal string NormalizedPath { get; }
        internal string CanonicalPath { get; }
        internal OutputRootMode Mode { get; }
        internal bool IsValid => Error == BuildAssistantError.None;
    }

    internal sealed class LocationPolicy
    {
        private static readonly string[] ManagedDirectoryNames = { "Assets", "Packages", "ProjectSettings", "Library", "Temp", "Logs", "obj" };
        private readonly BuildAssistantFileSystem fileSystem;
        private readonly string projectRoot;
        private readonly string canonicalProjectRoot;

        internal LocationPolicy(string projectRoot, BuildAssistantFileSystem fileSystem = null)
        {
            this.fileSystem = fileSystem ?? new BuildAssistantFileSystem();
            this.projectRoot = NormalizeExistingProjectRoot(projectRoot);
            if (this.fileSystem.IsNetworkDrive(this.projectRoot))
                throw new ArgumentException("A local project root is required.", nameof(projectRoot));
            canonicalProjectRoot = TrimEndingSeparators(this.fileSystem.GetCanonicalDirectoryPath(this.projectRoot));
        }

        internal LocationInspection Inspect(string outputRoot)
        {
            if (!IsFullyQualifiedPath(outputRoot))
                return Invalid(BuildAssistantError.InvalidOutputRoot, "The output root must be an absolute path.");

            try
            {
                var normalized = TrimEndingSeparators(fileSystem.GetFullPath(outputRoot.Trim()));
                if (fileSystem.FileExists(normalized))
                    return Invalid(BuildAssistantError.InvalidOutputRoot, "The output root points to a file.", normalized);

                var exists = fileSystem.DirectoryExists(normalized);
                var existingPath = normalized;
                if (!exists)
                {
                    existingPath = Path.GetDirectoryName(normalized);
                    if (string.IsNullOrEmpty(existingPath) || !fileSystem.DirectoryExists(existingPath))
                        return Invalid(BuildAssistantError.InvalidOutputRoot, "Only one missing child directory is allowed.", normalized);
                }

                if (fileSystem.IsNetworkDrive(existingPath))
                    return Invalid(BuildAssistantError.UnsafeOutputPath, "Network and mapped-drive output roots are not supported.", normalized);
                if (ContainsReparsePoint(existingPath))
                    return Invalid(BuildAssistantError.UnsafeOutputPath, "The output root or an existing ancestor is a reparse point.", normalized);

                var canonical = ResolveCanonicalCandidate(normalized, existingPath, exists);
                if (OverlapsManagedDirectory(normalized, canonical))
                    return Invalid(BuildAssistantError.UnsafeOutputPath, "The output root overlaps a Unity-managed project directory.", normalized);

                return new LocationInspection(BuildAssistantError.None, string.Empty, normalized, canonical, exists ? OutputRootMode.ExistingDirectory : OutputRootMode.MissingChild);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return Invalid(BuildAssistantError.InvalidOutputRoot, "The output root is not a valid absolute path.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException)
            {
                return Invalid(BuildAssistantError.UnsafeOutputPath, "The output root could not be verified safely: " + exception.Message);
            }
        }

        private bool OverlapsManagedDirectory(string candidate, string canonicalCandidate)
        {
            foreach (var name in ManagedDirectoryNames)
            {
                var managed = TrimEndingSeparators(Path.Combine(projectRoot, name));
                if (SafeBuildOutput.IsContained(managed, candidate) || SafeBuildOutput.IsContained(candidate, managed))
                    return true;
                var canonicalManaged = fileSystem.DirectoryExists(managed) ? TrimEndingSeparators(fileSystem.GetCanonicalDirectoryPath(managed)) : TrimEndingSeparators(Path.Combine(canonicalProjectRoot, name));
                if (CanonicalContains(canonicalManaged, canonicalCandidate) || CanonicalContains(canonicalCandidate, canonicalManaged))
                    return true;
            }

            return false;
        }

        private string ResolveCanonicalCandidate(string normalized, string existingPath, bool exists)
        {
            var canonicalExisting = TrimEndingSeparators(fileSystem.GetCanonicalDirectoryPath(existingPath));
            if (exists)
                return canonicalExisting;
            var childName = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(childName))
                throw new IOException("The missing output child name could not be resolved.");
            return TrimEndingSeparators(Path.Combine(canonicalExisting, childName));
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

        private static string NormalizeExistingProjectRoot(string value)
        {
            if (!IsFullyQualifiedPath(value))
                throw new ArgumentException("An absolute project root is required.", nameof(value));
            return TrimEndingSeparators(Path.GetFullPath(value));
        }

        internal static bool IsFullyQualifiedPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (Path.DirectorySeparatorChar == '\\')
            {
                var windowsPath = value.Replace('/', '\\');
                if (windowsPath.StartsWith("\\\\?\\", StringComparison.OrdinalIgnoreCase) || windowsPath.StartsWith("\\\\.\\", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'))
                    return HasSafeWindowsPathComponents(windowsPath);
                return false;
            }

            return value[0] == '/';
        }

        internal static bool HasSafeWindowsPathComponents(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            var path = value.Trim().Replace('/', '\\');
            if (path.Length < 3 || !char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path.IndexOf(':', 2) >= 0)
                return false;
            var tail = path.Substring(3).TrimEnd('\\');
            if (tail.Length == 0)
                return true;
            foreach (var component in tail.Split('\\'))
            {
                if (component.Length == 0 || component == "." || component == ".." || component != component.TrimEnd(' ', '.'))
                    return false;
                foreach (var character in component)
                {
                    if (character < 32 || character == '<' || character == '>' || character == '"' || character == '|' || character == '?' || character == '*')
                        return false;
                }
                var separator = component.IndexOf('.');
                var deviceName = (separator < 0 ? component : component.Substring(0, separator)).TrimEnd(' ', '.');
                if (IsReservedWindowsDeviceName(deviceName))
                    return false;
            }
            return true;
        }

        private static bool IsReservedWindowsDeviceName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            if (value.Equals("CON", StringComparison.OrdinalIgnoreCase) || value.Equals("PRN", StringComparison.OrdinalIgnoreCase) || value.Equals("AUX", StringComparison.OrdinalIgnoreCase) || value.Equals("NUL", StringComparison.OrdinalIgnoreCase) || value.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase) || value.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || value.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value.Length == 4 && (value.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || value.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && ((value[3] >= '1' && value[3] <= '9') || value[3] == '\u00B9' || value[3] == '\u00B2' || value[3] == '\u00B3'))
                return true;
            return false;
        }

        private static string TrimEndingSeparators(string value)
        {
            var root = Path.GetPathRoot(value);
            while (value.Length > root.Length && (value[value.Length - 1] == Path.DirectorySeparatorChar || value[value.Length - 1] == Path.AltDirectorySeparatorChar))
                value = value.Substring(0, value.Length - 1);
            return value;
        }

        internal static bool CanonicalContains(string boundary, string candidate)
        {
            if (string.IsNullOrEmpty(boundary) || string.IsNullOrEmpty(candidate))
                return false;
            var normalizedBoundary = TrimEndingSeparators(boundary);
            var normalizedCandidate = TrimEndingSeparators(candidate);
            if (string.Equals(normalizedBoundary, normalizedCandidate, PathComparison))
                return true;
            return normalizedCandidate.StartsWith(normalizedBoundary + Path.DirectorySeparatorChar, PathComparison);
        }

        internal static bool CanonicalEquals(string left, string right) => string.Equals(TrimEndingSeparators(left ?? string.Empty), TrimEndingSeparators(right ?? string.Empty), PathComparison);

        private static StringComparison PathComparison => StringComparison.OrdinalIgnoreCase;

        private static LocationInspection Invalid(BuildAssistantError error, string message, string path = "") => new LocationInspection(error, message, path, string.Empty, OutputRootMode.ExistingDirectory);
    }
}
