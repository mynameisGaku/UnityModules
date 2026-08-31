using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;

namespace BuildAssistant.Editor
{
    internal sealed class EnvironmentCaptureException : Exception
    {
        internal EnvironmentCaptureException(BuildAssistantError error, string message, Exception innerException = null) : base(message, innerException)
        {
            Error = error;
        }

        internal BuildAssistantError Error { get; }
    }

    internal sealed class UnityBuildEnvironment
    {
        internal EnvironmentSnapshot Capture()
        {
            try
            {
                var activeProfile = BuildProfile.GetActiveBuildProfile();
                ValidateProfilerConnectionId(CaptureProfilerConnectionId());
                var globalTarget = EditorUserBuildSettings.activeBuildTarget;
                var globalSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
                var profileTarget = activeProfile == null ? globalTarget : (BuildTarget)GetRequiredProperty(activeProfile, "buildTarget");
                var profileSubtarget = activeProfile == null ? globalSubtarget : (StandaloneBuildSubtarget)GetRequiredProperty(activeProfile, "subtarget");
                ResolveBuildAuthority(activeProfile != null, globalTarget, globalSubtarget, profileTarget, profileSubtarget, out var target, out var standaloneSubtarget);
                var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
                ValidateTarget(target, targetGroup, standaloneSubtarget);
                ValidateBuildTargetSupport(targetGroup, target, BuildPipeline.IsBuildTargetSupported(targetGroup, target));
                var namedBuildTarget = NamedBuildTarget.Standalone;
                var projectRoot = CaptureProjectRoot();
                var projectSettingsFingerprint = CaptureProjectSettingsFingerprint(projectRoot);
                var platformProfilesFingerprint = CapturePlatformProfilesFingerprint(projectRoot);
                var projectContentFingerprint = CaptureProjectContentFingerprint(projectRoot);
                var effectiveSettingsFingerprint = HashStrings(projectSettingsFingerprint, platformProfilesFingerprint, projectContentFingerprint);
                var profile = CaptureProfile(activeProfile, namedBuildTarget.TargetName, effectiveSettingsFingerprint);
                var scenes = CaptureScenes(activeProfile);
                if (!scenes.Any(scene => scene.Enabled))
                    throw new EnvironmentCaptureException(BuildAssistantError.NoEnabledScenes, "有効なビルドプロファイルに、ビルド対象のシーンがありません。");

                var extraDefines = Array.Empty<string>();
                var effectivePlayerSettings = CaptureEffectivePlayerSettings(activeProfile);
                var playerDefines = CaptureScriptingDefines(effectivePlayerSettings, namedBuildTarget);
                var scriptingBackend = CaptureScriptingBackend(effectivePlayerSettings, namedBuildTarget);
                var effectiveDefines = NormalizeDefines(playerDefines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Concat(activeProfile?.scriptingDefines ?? Array.Empty<string>()).Concat(extraDefines));
                var invocationOptions = CaptureReadOnlyInvocationOptions(target, targetGroup, activeProfile == null, scriptingBackend);
                var effectiveOptions = activeProfile == null ? invocationOptions : invocationOptions | CaptureCustomProfileOptions(activeProfile, target, scriptingBackend);
                return new EnvironmentSnapshot(profile, target, targetGroup, namedBuildTarget.TargetName, (int)standaloneSubtarget, scriptingBackend, effectiveOptions, CaptureAssetBundleManifestPath(), extraDefines, effectiveDefines, scenes, invocationOptions);
            }
            catch (EnvironmentCaptureException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "プロジェクト設定を変更せずに、現在のビルド環境を取得できませんでした。", exception);
            }
        }

        private static ProfileSnapshot CaptureProfile(BuildProfile activeProfile, string namedBuildTarget, string projectSettingsFingerprint)
        {
            if (activeProfile == null)
                return new ProfileSnapshot(BuildAssistantProfileKind.Platform, string.Empty, "デスクトップ向けプラットフォーム設定", string.Empty, projectSettingsFingerprint, "platform:" + namedBuildTarget);

            var assetPath = AssetDatabase.GetAssetPath(activeProfile) ?? string.Empty;
            var guid = assetPath.Length == 0 ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            var profileDependencyHash = assetPath.Length == 0 ? string.Empty : AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
            ValidateCustomProfileIdentity(assetPath, guid, profileDependencyHash);
            var dependencyHash = HashStrings(projectSettingsFingerprint, profileDependencyHash);
            var stableId = "custom:" + guid;
            return new ProfileSnapshot(BuildAssistantProfileKind.Custom, guid, activeProfile.name, assetPath, dependencyHash, stableId);
        }

        internal static void ValidateCustomProfileIdentity(string assetPath, string guid, string dependencyHash)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !IsUsableHexIdentity(guid) || !IsUsableHexIdentity(dependencyHash))
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "有効な独自ビルドプロファイルは、安定したGUIDと依存関係照合値を持つ保存済みプロジェクトアセットである必要があります。");
        }

        internal static void ValidateProfilerConnectionId(string customConnectionId)
        {
            if (!string.IsNullOrEmpty(customConnectionId))
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "独自の性能測定接続先には対応していません。接続先を標準設定へ戻してください。");
        }

        private static string CaptureProfilerConnectionId()
        {
            var type = typeof(BuildProfile).Assembly.GetType("UnityEditor.Profiling.ProfilerUserSettings");
            var property = type?.GetProperty("customConnectionID", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || property.GetIndexParameters().Length != 0)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "独自の性能測定接続先設定を確認できませんでした。");
            return property.GetValue(null, null) as string ?? string.Empty;
        }

        private static bool IsUsableHexIdentity(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
                return false;
            var nonzero = false;
            foreach (var character in value)
            {
                if (!Uri.IsHexDigit(character))
                    return false;
                if (character != '0')
                    nonzero = true;
            }
            return nonzero;
        }

        private static string CaptureProjectRoot()
        {
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "プロジェクトの基準フォルダーを解決できませんでした。");
            return projectRoot;
        }

        private static string CaptureProjectSettingsFingerprint(string projectRoot)
        {
            var settingsRoot = Path.Combine(projectRoot, "ProjectSettings");
            if (!Directory.Exists(settingsRoot))
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "ProjectSettingsフォルダーを読み取れませんでした。");

            var files = Directory.GetFiles(settingsRoot, "*", SearchOption.AllDirectories).Select(path => new KeyValuePair<string, byte[]>(path.Substring(settingsRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/'), File.ReadAllBytes(path)));
            return ComputeProjectSettingsFingerprint(files);
        }

        internal static string ComputeProjectSettingsFingerprint(IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            return ComputeFileSetFingerprint("BuildAssistant.ProjectSettings.v1", files);
        }

        private static string CapturePlatformProfilesFingerprint(string projectRoot)
        {
            var profilesRoot = Path.Combine(projectRoot, "Library", "BuildProfiles");
            var files = new List<KeyValuePair<string, byte[]>>();
            if (Directory.Exists(profilesRoot))
                files.AddRange(Directory.GetFiles(profilesRoot, "*.asset", SearchOption.AllDirectories).Select(path => new KeyValuePair<string, byte[]>("BuildProfiles/" + path.Substring(profilesRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/'), File.ReadAllBytes(path))));
            var editorUserSettingsPath = Path.Combine(projectRoot, "Library", "EditorUserBuildSettings.asset");
            if (File.Exists(editorUserSettingsPath))
                files.Add(new KeyValuePair<string, byte[]>("EditorUserBuildSettings.asset", File.ReadAllBytes(editorUserSettingsPath)));
            return ComputePlatformProfilesFingerprint(files);
        }

        internal static string ComputePlatformProfilesFingerprint(IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            return ComputeFileSetFingerprint("BuildAssistant.EffectiveBuildSettings.v1", files);
        }

        private static string CaptureProjectContentFingerprint(string projectRoot)
        {
            var files = new List<KeyValuePair<string, byte[]>>();
            foreach (var relativePath in new[] { "Packages/manifest.json", "Packages/packages-lock.json" })
            {
                var path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path))
                    files.Add(new KeyValuePair<string, byte[]>(relativePath, File.ReadAllBytes(path)));
            }
            var streamingAssetsRoot = Path.Combine(projectRoot, "Assets", "StreamingAssets");
            if (Directory.Exists(streamingAssetsRoot))
                files.AddRange(Directory.GetFiles(streamingAssetsRoot, "*", SearchOption.AllDirectories).Select(path => new KeyValuePair<string, byte[]>("Assets/StreamingAssets/" + path.Substring(streamingAssetsRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/'), File.ReadAllBytes(path))));
            return ComputeProjectContentFingerprint(AssetDatabase.GlobalArtifactDependencyVersion, AssetDatabase.GlobalArtifactProcessedVersion, files);
        }

        internal static string ComputeProjectContentFingerprint(uint dependencyVersion, uint processedVersion, IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            var rawFiles = ComputeFileSetFingerprint("BuildAssistant.RawProjectContent.v1", files);
            return HashStrings(dependencyVersion.ToString(CultureInfo.InvariantCulture), processedVersion.ToString(CultureInfo.InvariantCulture), rawFiles);
        }

        private static string ComputeFileSetFingerprint(string version, IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            var ordered = (files ?? Enumerable.Empty<KeyValuePair<string, byte[]>>()).OrderBy(pair => pair.Key ?? string.Empty, StringComparer.Ordinal).ToArray();
            using (var hash = SHA256.Create())
            {
                AppendFramedBytes(hash, Encoding.UTF8.GetBytes(version ?? string.Empty));
                AppendUnsigned(hash, (ulong)ordered.Length);
                foreach (var pair in ordered)
                {
                    AppendFramedBytes(hash, Encoding.UTF8.GetBytes(pair.Key ?? string.Empty));
                    AppendFramedBytes(hash, pair.Value ?? Array.Empty<byte>());
                }

                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(hash.Hash);
            }
        }

        private static void AppendFramedBytes(HashAlgorithm hash, byte[] bytes)
        {
            var value = bytes ?? Array.Empty<byte>();
            AppendUnsigned(hash, (ulong)value.Length);
            if (value.Length > 0)
                hash.TransformBlock(value, 0, value.Length, value, 0);
        }

        private static void AppendUnsigned(HashAlgorithm hash, ulong value)
        {
            var bytes = new byte[8];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = (byte)(value >> ((bytes.Length - index - 1) * 8));
            hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
        }

        private static string HashStrings(params string[] values)
        {
            using (var hash = SHA256.Create())
            {
                var source = values ?? Array.Empty<string>();
                AppendFramedBytes(hash, Encoding.UTF8.GetBytes("BuildAssistant.StringSet.v1"));
                AppendUnsigned(hash, (ulong)source.Length);
                foreach (var value in source)
                    AppendFramedBytes(hash, Encoding.UTF8.GetBytes(value ?? string.Empty));
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(hash.Hash);
            }
        }

        private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes ?? Array.Empty<byte>()).Replace("-", string.Empty).ToLowerInvariant();

        private static BuildAssistantScene[] CaptureScenes(BuildProfile activeProfile)
        {
            var source = activeProfile != null && activeProfile.overrideGlobalScenes ? activeProfile.scenes : EditorBuildSettings.scenes;
            source = source ?? Array.Empty<EditorBuildSettingsScene>();
            var result = new BuildAssistantScene[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var scene = source[index];
                var path = (scene?.path ?? string.Empty).Replace('\\', '/');
                var guid = scene == null ? string.Empty : scene.guid.ToString();
                if (string.IsNullOrEmpty(guid) && path.Length > 0)
                    guid = AssetDatabase.AssetPathToGUID(path);
                var dependencyHash = path.Length > 0 ? AssetDatabase.GetAssetDependencyHash(path).ToString() : string.Empty;
                var enabled = scene != null && scene.enabled;
                if (enabled)
                    ValidateEnabledScene(path, guid, dependencyHash, AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null);
                result[index] = new BuildAssistantScene(index, guid, path, enabled, dependencyHash);
            }

            return result;
        }

        internal static void ValidateEnabledScene(string assetPath, string guid, string dependencyHash, bool loadableSceneAsset)
        {
            var normalizedPath = (assetPath ?? string.Empty).Replace('\\', '/');
            var supportedRoot = normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) || normalizedPath.StartsWith("Packages/", StringComparison.Ordinal);
            var validPath = supportedRoot && normalizedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !normalizedPath.Split('/').Any(segment => segment == "." || segment == ".." || segment.Length == 0);
            if (!validPath || !loadableSceneAsset || !IsUsableHexIdentity(guid) || !IsUsableHexIdentity(dependencyHash))
                throw new EnvironmentCaptureException(BuildAssistantError.NoEnabledScenes, "有効なシーンが存在しない、シーンアセットとして読み込めない、または安定したGUIDと依存関係照合値を持ちません: " + normalizedPath);
        }

        private static string[] NormalizeDefines(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static BuildOptions CaptureReadOnlyInvocationOptions(BuildTarget target, BuildTargetGroup targetGroup, bool platformProfile, ScriptingImplementation scriptingBackend)
        {
            if (EditorUserBuildSettings.buildScriptsOnly)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "スクリプトだけを更新するビルドには対応していません。");
            ValidateGlobalInstallInBuildFolder(platformProfile, EditorUserBuildSettings.installInBuildFolder);

            var options = BuildOptions.DetailedBuildReport;
            if (platformProfile)
            {
                options |= CaptureCompressionOption(target, targetGroup);
                options |= ComposeDevelopmentOptions(EditorUserBuildSettings.development, EditorUserBuildSettings.connectProfiler, EditorUserBuildSettings.allowDebugging, EditorUserBuildSettings.waitForPlayerConnection, EditorUserBuildSettings.buildWithCodeCoverage, EditorUserBuildSettings.buildWithDeepProfilingSupport, scriptingBackend);
            }
            if (EditorUserBuildSettings.symlinkSources)
                options |= BuildOptions.SymlinkSources;
            return options;
        }

        internal static void ValidateGlobalInstallInBuildFolder(bool platformProfile, bool globalInstallInBuildFolder)
        {
            if (platformProfile && globalInstallInBuildFolder)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "ビルドフォルダー内へ直接配置する設定には対応していません。");
        }

        private static BuildOptions CaptureCustomProfileOptions(BuildProfile activeProfile, BuildTarget target, ScriptingImplementation scriptingBackend)
        {
            var settings = GetRequiredProperty(activeProfile, "platformBuildProfile");
            var configuredCompression = GetRequiredProperty(settings, "compressionType")?.ToString() ?? string.Empty;
            var defaultCompression = int.TryParse(configuredCompression, out var numericValue) && numericValue < 0 ? CaptureDefaultCompressionName(target) : string.Empty;
            return ComposeProfileOptions(configuredCompression, defaultCompression, ReadRequiredBoolean(settings, "development"), ReadRequiredBoolean(settings, "connectProfiler"), ReadRequiredBoolean(settings, "allowDebugging"), ReadRequiredBoolean(settings, "waitForManagedDebugger"), ReadRequiredBoolean(settings, "buildWithCodeCoverage"), ReadRequiredBoolean(settings, "buildWithDeepProfilingSupport"), ReadRequiredBoolean(settings, "installInBuildFolder"), scriptingBackend);
        }

        private static PlayerSettings CaptureEffectivePlayerSettings(BuildProfile activeProfile)
        {
            if (activeProfile == null)
                return null;
            var utility = typeof(BuildProfile).Assembly.GetType("UnityEditor.Build.Profile.BuildProfileModuleUtil");
            var method = utility?.GetMethod("GetBuildProfileOrGlobalPlayerSettings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(BuildProfile) }, null);
            var playerSettings = method?.Invoke(null, new object[] { activeProfile }) as PlayerSettings;
            if (playerSettings == null)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "独自ビルドプロファイルで有効なプレイヤー設定を取得できませんでした。");
            return playerSettings;
        }

        private static string CaptureScriptingDefines(PlayerSettings effectivePlayerSettings, NamedBuildTarget namedBuildTarget)
        {
            if (effectivePlayerSettings == null)
                return PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            var method = typeof(PlayerSettings).GetMethod("GetScriptingDefineSymbols_Internal", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(PlayerSettings), typeof(string) }, null);
            if (method == null)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "独自ビルドプロファイルで有効な条件付きコンパイル定義を取得できませんでした。");
            return method.Invoke(null, new object[] { effectivePlayerSettings, namedBuildTarget.TargetName }) as string ?? string.Empty;
        }

        private static ScriptingImplementation CaptureScriptingBackend(PlayerSettings effectivePlayerSettings, NamedBuildTarget namedBuildTarget)
        {
            if (effectivePlayerSettings == null)
                return PlayerSettings.GetScriptingBackend(namedBuildTarget);
            var method = typeof(PlayerSettings).GetMethod("GetScriptingBackend_Internal", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(PlayerSettings), typeof(string) }, null);
            if (method == null)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "独自ビルドプロファイルで有効なコード生成方式を取得できませんでした。");
            return (ScriptingImplementation)method.Invoke(null, new object[] { effectivePlayerSettings, namedBuildTarget.TargetName });
        }

        internal static BuildOptions ComposeProfileOptions(string configuredCompression, string defaultCompression, bool development, bool connectProfiler, bool allowDebugging, bool waitForPlayerConnection, bool codeCoverage, bool deepProfiling, bool installInBuildFolder, ScriptingImplementation scriptingBackend)
        {
            if (installInBuildFolder)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "ビルドフォルダー内へ直接配置する設定には対応していません。");
            return ResolveCompressionOption(configuredCompression, defaultCompression) | ComposeDevelopmentOptions(development, connectProfiler, allowDebugging, waitForPlayerConnection, codeCoverage, deepProfiling, scriptingBackend);
        }

        internal static BuildOptions ComposeDevelopmentOptions(bool development, bool connectProfiler, bool allowDebugging, bool waitForPlayerConnection, bool codeCoverage, bool deepProfiling, ScriptingImplementation scriptingBackend)
        {
            if (!development)
                return BuildOptions.None;
            var options = BuildOptions.Development;
            if (connectProfiler)
                options |= BuildOptions.ConnectWithProfiler;
            if (allowDebugging)
                options |= BuildOptions.AllowDebugging;
            if (waitForPlayerConnection)
                options |= BuildOptions.WaitForPlayerConnection;
            if (codeCoverage && scriptingBackend == ScriptingImplementation.Mono2x)
                options |= BuildOptions.EnableCodeCoverage;
            if (deepProfiling)
                options |= BuildOptions.EnableDeepProfilingSupport;
            return options;
        }

        private static object GetRequiredProperty(object instance, string propertyName)
        {
            if (instance == null)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "有効な独自ビルドプロファイルの設定を取得できませんでした。");
            for (var type = instance.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(instance, null);
            }

            throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "有効な独自ビルドプロファイルから必要な設定項目を取得できませんでした。");
        }

        private static bool ReadRequiredBoolean(object instance, string propertyName)
        {
            var value = GetRequiredProperty(instance, propertyName);
            if (value is bool result)
                return result;
            throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "有効な独自ビルドプロファイルの設定値に対応していません。");
        }

        private static BuildOptions CaptureCompressionOption(BuildTarget target, BuildTargetGroup targetGroup)
        {
            var method = typeof(EditorUserBuildSettings).GetMethod("GetCompressionType", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(BuildTargetGroup) }, null);
            if (method == null)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "現在の通常プレイヤー圧縮設定を取得できませんでした。");
            var value = method.Invoke(null, new object[] { targetGroup });
            var configuredName = value?.ToString() ?? string.Empty;
            var defaultName = string.Empty;
            if (value != null && Convert.ToInt32(value) < 0)
                defaultName = CaptureDefaultCompressionName(target);

            return ResolveCompressionOption(configuredName, defaultName);
        }

        private static string CaptureDefaultCompressionName(BuildTarget target)
        {
            var defaultType = typeof(EditorUserBuildSettings).Assembly.GetType("UnityEditor.PostprocessBuildPlayer");
            var defaultMethod = defaultType?.GetMethod("GetDefaultCompression", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(BuildTarget) }, null);
            if (defaultMethod == null)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "通常プレイヤーの標準圧縮設定を取得できませんでした。");
            return defaultMethod.Invoke(null, new object[] { target })?.ToString() ?? string.Empty;
        }

        private static string CaptureAssetBundleManifestPath()
        {
            var type = typeof(EditorUserBuildSettings).Assembly.GetType("UnityEditor.PostprocessBuildPlayer");
            var method = type?.GetMethod("GetStreamingAssetsBundleManifestPath", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (method == null)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "現在のアセットバンドル目録設定を取得できませんでした。");
            return method.Invoke(null, null) as string ?? string.Empty;
        }

        internal static BuildOptions ResolveCompressionOption(string configuredName, string defaultName = "")
        {
            var name = configuredName;
            if (int.TryParse(configuredName, out var numericValue) && numericValue < 0)
                name = defaultName;
            switch (name)
            {
                case "None":
                    return BuildOptions.None;
                case "Lz4":
                    return BuildOptions.CompressWithLz4;
                case "Lz4HC":
                    return BuildOptions.CompressWithLz4HC;
                default:
                    throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "現在の通常プレイヤー圧縮設定には対応していません。");
            }
        }

        internal static void ResolveBuildAuthority(bool customProfileActive, BuildTarget globalTarget, StandaloneBuildSubtarget globalSubtarget, BuildTarget profileTarget, StandaloneBuildSubtarget profileSubtarget, out BuildTarget target, out StandaloneBuildSubtarget subtarget)
        {
            if (customProfileActive && (globalTarget != profileTarget || !AreEquivalentPlayerSubtargets(globalSubtarget, profileSubtarget)))
            {
                throw new EnvironmentCaptureException(
                    BuildAssistantError.BuildTargetMismatch,
                    "独自のビルドプロファイルが指定する対象機種または種別と、エディターで選択中の内容が一致しません。Unityのビルドプロファイル画面で対象を切り替え、コンパイル完了後に計画を作り直してください。"
                );
            }

            target = customProfileActive ? profileTarget : globalTarget;
            subtarget = customProfileActive ? profileSubtarget : globalSubtarget;
        }

        private static bool AreEquivalentPlayerSubtargets(StandaloneBuildSubtarget left, StandaloneBuildSubtarget right)
        {
            if (left == right)
                return true;
            var leftIsPlayer = left == StandaloneBuildSubtarget.Default || left == StandaloneBuildSubtarget.Player;
            var rightIsPlayer = right == StandaloneBuildSubtarget.Default || right == StandaloneBuildSubtarget.Player;
            return leftIsPlayer && rightIsPlayer;
        }

        internal static void ValidateTarget(BuildTarget target, BuildTargetGroup targetGroup, StandaloneBuildSubtarget subtarget)
        {
            var desktop = target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneOSX || target == BuildTarget.StandaloneLinux64;
            if (!desktop || targetGroup != BuildTargetGroup.Standalone)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "デスクトップ向けの通常プレイヤーだけに対応しています。");
            if (subtarget == StandaloneBuildSubtarget.Server)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "専用サーバーのビルドには対応していません。");
            if (subtarget != StandaloneBuildSubtarget.Default && subtarget != StandaloneBuildSubtarget.Player)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "現在の通常プレイヤー種別には対応していません。");
        }

        internal static void ValidateBuildTargetSupport(BuildTargetGroup targetGroup, BuildTarget target, bool isSupported)
        {
            if (!isSupported)
                throw new EnvironmentCaptureException(BuildAssistantError.UnsupportedBuildTarget, "現在のデスクトップ対象に必要なプラットフォームモジュールがありません: " + targetGroup + "/" + target + "。");
        }
    }
}
