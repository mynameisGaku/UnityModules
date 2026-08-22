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
            return BuildMissingDefinitions(
                assemblyName,
                runtimeFolder,
                editorFolder,
                false,
                string.Empty,
                currentFolders,
                currentAssetPaths,
                errors);
        }

        internal static ProjectSetupAssemblyDefinitionPlan[] BuildMissingDefinitions(
            string assemblyName,
            string runtimeFolder,
            string editorFolder,
            bool includeTestAssemblies,
            string testRootFolder,
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

            var normalizedTestRootFolder = string.Empty;
            if (includeTestAssemblies
                && !ProjectSetupFolderUtility.TryNormalize(testRootFolder, out normalizedTestRootFolder, out var testRootError))
            {
                errors?.Add($"Test Root Folder: {testRootError}");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            var existingFolders = new HashSet<string>(currentFolders ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var existingAssets = new HashSet<string>(currentAssetPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var runtimePath = $"{normalizedRuntimeFolder}/{assemblyName}.asmdef";
            var editorPath = $"{normalizedEditorFolder}/{assemblyName}.Editor.asmdef";
            var editModeFolder = includeTestAssemblies ? $"{normalizedTestRootFolder}/EditMode" : string.Empty;
            var playModeFolder = includeTestAssemblies ? $"{normalizedTestRootFolder}/PlayMode" : string.Empty;
            var editModePath = includeTestAssemblies ? $"{editModeFolder}/{assemblyName}.Tests.asmdef" : string.Empty;
            var playModePath = includeTestAssemblies ? $"{playModeFolder}/{assemblyName}.PlayMode.Tests.asmdef" : string.Empty;
            var targetPaths = includeTestAssemblies
                ? new[] { runtimePath, editorPath, editModePath, playModePath }
                : new[] { runtimePath, editorPath };
            if (targetPaths.Any(existingFolders.Contains))
            {
                errors?.Add("An Assembly Definition target path is already used by a folder.");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            if (ContainsDifferentAssemblyDefinition(normalizedRuntimeFolder, runtimePath, existingAssets)
                || ContainsDifferentAssemblyDefinition(normalizedEditorFolder, editorPath, existingAssets)
                || (includeTestAssemblies && ContainsDifferentAssemblyDefinition(editModeFolder, editModePath, existingAssets))
                || (includeTestAssemblies && ContainsDifferentAssemblyDefinition(playModeFolder, playModePath, existingAssets)))
            {
                errors?.Add("A target folder already contains a different Assembly Definition. Existing Assembly Definitions are never overwritten.");
                return Array.Empty<ProjectSetupAssemblyDefinitionPlan>();
            }

            var plans = new List<ProjectSetupAssemblyDefinitionPlan>(includeTestAssemblies ? 4 : 2);
            if (!existingAssets.Contains(runtimePath))
            {
                plans.Add(new ProjectSetupAssemblyDefinitionPlan(runtimePath, CreateRuntimeContent(assemblyName)));
            }

            if (!existingAssets.Contains(editorPath))
            {
                plans.Add(new ProjectSetupAssemblyDefinitionPlan(editorPath, CreateEditorContent(assemblyName)));
            }

            if (includeTestAssemblies && !existingAssets.Contains(editModePath))
            {
                plans.Add(new ProjectSetupAssemblyDefinitionPlan(editModePath, CreateEditModeTestContent(assemblyName)));
            }

            if (includeTestAssemblies && !existingAssets.Contains(playModePath))
            {
                plans.Add(new ProjectSetupAssemblyDefinitionPlan(playModePath, CreatePlayModeTestContent(assemblyName)));
            }

            return plans.ToArray();
        }

        internal static string[] GetRequiredFolders(string runtimeFolder, string editorFolder)
        {
            return GetRequiredFolders(runtimeFolder, editorFolder, false, string.Empty);
        }

        internal static string[] GetRequiredFolders(
            string runtimeFolder,
            string editorFolder,
            bool includeTestAssemblies,
            string testRootFolder)
        {
            var result = new List<string>(includeTestAssemblies ? 5 : 2);
            if (ProjectSetupFolderUtility.TryNormalize(runtimeFolder, out var normalizedRuntimeFolder, out _))
            {
                result.Add(normalizedRuntimeFolder);
            }

            if (ProjectSetupFolderUtility.TryNormalize(editorFolder, out var normalizedEditorFolder, out _))
            {
                result.Add(normalizedEditorFolder);
            }

            if (includeTestAssemblies
                && ProjectSetupFolderUtility.TryNormalize(testRootFolder, out var normalizedTestRootFolder, out _))
            {
                result.Add(normalizedTestRootFolder);
                result.Add($"{normalizedTestRootFolder}/EditMode");
                result.Add($"{normalizedTestRootFolder}/PlayMode");
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

        private static string CreateEditModeTestContent(string assemblyName)
        {
            return "{\n"
                + $"  \"name\": \"{assemblyName}.Tests\",\n"
                + $"  \"rootNamespace\": \"{assemblyName}.Tests\",\n"
                + "  \"references\": [\n"
                + $"    \"{assemblyName}\",\n"
                + $"    \"{assemblyName}.Editor\"\n"
                + "  ],\n"
                + "  \"includePlatforms\": [\n"
                + "    \"Editor\"\n"
                + "  ],\n"
                + "  \"optionalUnityReferences\": [\n"
                + "    \"TestAssemblies\"\n"
                + "  ]\n"
                + "}\n";
        }

        private static string CreatePlayModeTestContent(string assemblyName)
        {
            return "{\n"
                + $"  \"name\": \"{assemblyName}.PlayMode.Tests\",\n"
                + $"  \"rootNamespace\": \"{assemblyName}.PlayMode.Tests\",\n"
                + "  \"references\": [\n"
                + $"    \"{assemblyName}\"\n"
                + "  ],\n"
                + "  \"optionalUnityReferences\": [\n"
                + "    \"TestAssemblies\"\n"
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
