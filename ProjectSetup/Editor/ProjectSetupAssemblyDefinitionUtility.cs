// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ProjectSetup.Editor
{
    internal static class ProjectSetupAssemblyDefinitionUtility
    {
        internal const int MaximumAssemblyNameLength = 128;
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly",
            "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
            "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        internal static ProjectSetupAssemblyDefinitionPlan[] BuildMissingDefinitions(
            string assemblyName,
            string runtimeFolder,
            string editorFolder,
            IEnumerable<string> currentFolders,
            IEnumerable<string> currentAssetPaths,
            ICollection<string> errors)
        {
            if (!IsValidDottedIdentifier(assemblyName))
            {
                errors?.Add("Assembly Name must be a dotted ASCII C# identifier with 1 to 128 characters.");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            if (!ProjectSetupFolderUtility.TryNormalize(runtimeFolder, out var normalizedRuntimeFolder, out var runtimeError))
            {
                errors?.Add($"Runtime Assembly Folder: {runtimeError}");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            if (!ProjectSetupFolderUtility.TryNormalize(editorFolder, out var normalizedEditorFolder, out var editorError))
            {
                errors?.Add($"Editor Assembly Folder: {editorError}");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            if (string.Equals(normalizedRuntimeFolder, normalizedEditorFolder, StringComparison.OrdinalIgnoreCase)
                || !normalizedEditorFolder.StartsWith(normalizedRuntimeFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                errors?.Add("Editor Assembly Folder must be a child of Runtime Assembly Folder.");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            var existingFolders = new HashSet<string>(currentFolders ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var existingAssets = new HashSet<string>(currentAssetPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var runtimePath = $"{normalizedRuntimeFolder}/{assemblyName}.asmdef";
            var editorPath = $"{normalizedEditorFolder}/{assemblyName}.Editor.asmdef";
            if (existingFolders.Contains(runtimePath) || existingFolders.Contains(editorPath))
            {
                errors?.Add("An Assembly Definition target path is already used by a folder.");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            if (ContainsDifferentAssemblyDefinition(normalizedRuntimeFolder, runtimePath, existingAssets)
                || ContainsDifferentAssemblyDefinition(normalizedEditorFolder, editorPath, existingAssets))
            {
                errors?.Add("A target folder already contains a different Assembly Definition. Existing Assembly Definitions are never overwritten.");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            var plans = new List<ProjectSetupAssemblyDefinitionPlan>(2);
            if (!existingAssets.Contains(runtimePath))
            {
                plans.Add(new ProjectSetupAssemblyDefinitionPlan(runtimePath, CreateRuntimeContent(assemblyName)));
            }

            if (!existingAssets.Contains(editorPath))
            {
                plans.Add(new ProjectSetupAssemblyDefinitionPlan(editorPath, CreateEditorContent(assemblyName)));
            }

            return plans.ToArray();
        }

        internal static string[] GetRequiredFolders(string runtimeFolder, string editorFolder)
        {
            var result = new List<string>(2);
            if (ProjectSetupFolderUtility.TryNormalize(runtimeFolder, out var normalizedRuntimeFolder, out _))
            {
                result.Add(normalizedRuntimeFolder);
            }

            if (ProjectSetupFolderUtility.TryNormalize(editorFolder, out var normalizedEditorFolder, out _))
            {
                result.Add(normalizedEditorFolder);
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static string ComputeContentHash(string content)
        {
            var bytes = new UTF8Encoding(false).GetBytes(content ?? string.Empty);
            return ComputeContentHash(bytes);
        }

        internal static string ComputeContentHash(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes ?? Array.Empty<byte>())).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string CreateRuntimeContent(string assemblyName)
        {
            return "{\n"
                + $"  \"name\": \"{assemblyName}\",\n"
                + $"  \"rootNamespace\": \"{assemblyName}\"\n"
                + "}\n";
        }

        private static string CreateEditorContent(string assemblyName)
        {
            return "{\n"
                + $"  \"name\": \"{assemblyName}.Editor\",\n"
                + $"  \"rootNamespace\": \"{assemblyName}.Editor\",\n"
                + "  \"references\": [\n"
                + $"    \"{assemblyName}\"\n"
                + "  ],\n"
                + "  \"includePlatforms\": [\n"
                + "    \"Editor\"\n"
                + "  ]\n"
                + "}\n";
        }

        private static bool ContainsDifferentAssemblyDefinition(string folder, string targetPath, IEnumerable<string> currentAssetPaths)
        {
            return (currentAssetPaths ?? Array.Empty<string>())
                .Where(path => path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                .Any(path => string.Equals(GetParent(path), folder, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetParent(string path)
        {
            var separator = path?.LastIndexOf('/') ?? -1;
            return separator <= 0 ? string.Empty : path.Substring(0, separator);
        }

        private static bool IsValidDottedIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumAssemblyNameLength)
            {
                return false;
            }

            var segments = value.Split('.');
            foreach (var segment in segments)
            {
                if (segment.Length == 0 || CSharpKeywords.Contains(segment) || !IsIdentifierStart(segment[0]))
                {
                    return false;
                }

                for (var index = 1; index < segment.Length; index++)
                {
                    if (!IsIdentifierPart(segment[index]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsIdentifierStart(char value)
        {
            return value == '_' || IsAsciiLetter(value);
        }

        private static bool IsIdentifierPart(char value)
        {
            return value == '_' || IsAsciiLetter(value) || (value >= '0' && value <= '9');
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }
    }
}
