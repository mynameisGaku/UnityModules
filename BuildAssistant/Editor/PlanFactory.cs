using System;
using System.Globalization;
using System.IO;
using UnityEditor;

namespace BuildAssistant.Editor
{
    internal static class PlanFactory
    {
        internal static BuildAssistantPlan Create(PlanningContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var runId = CreateRunId(context.CreatedAtUtc, context.Entropy);
            var runDirectory = Path.Combine(context.OutputRoot, runId);
            var artifactPath = Path.Combine(runDirectory, GetArtifactName(context.Environment.Target));
            if (context.RunPathBusy)
                return CreateFailure(context.Environment, BuildAssistantError.OutputAlreadyExists, "今回の実行フォルダーまたは予約が既に存在します。", context.OutputRoot, context.OutputRootMode, runId, context.CreatedAtUtc, runDirectory, artifactPath);

            var environment = context.Environment;
            return new BuildAssistantPlan(BuildAssistantError.None, string.Empty, runId, context.CreatedAtUtc, context.OutputRoot, runDirectory, artifactPath, context.OutputRootMode, environment.Profile, environment.Target, environment.TargetGroup, environment.NamedBuildTarget, environment.Subtarget, environment.ScriptingBackend, environment.Options, environment.InvocationOptions, environment.AssetBundleManifestPath, environment.ExtraScriptingDefines, environment.EffectiveDefines, environment.Scenes, context.PreviousComparableSuccess);
        }

        internal static string CreateRunId(DateTime createdAtUtc, string entropy)
        {
            var utc = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : createdAtUtc.ToUniversalTime();
            return "BA-" + utc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + NormalizeEntropy(entropy);
        }

        internal static BuildAssistantPlan CreateFailure(EnvironmentSnapshot environment, BuildAssistantError error, string message, string outputRoot = "", OutputRootMode outputRootMode = OutputRootMode.ExistingDirectory, string runId = "", DateTime createdAtUtc = default, string runDirectory = "", string artifactPath = "")
        {
            if (environment == null)
            {
                var profile = new ProfileSnapshot(BuildAssistantProfileKind.Platform, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                environment = new EnvironmentSnapshot(profile, BuildTarget.NoTarget, BuildTargetGroup.Unknown, string.Empty, 0, ScriptingImplementation.Mono2x, BuildOptions.None, string.Empty, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<BuildAssistantScene>());
            }

            return new BuildAssistantPlan(error, message, runId, createdAtUtc, outputRoot, runDirectory, artifactPath, outputRootMode, environment.Profile, environment.Target, environment.TargetGroup, environment.NamedBuildTarget, environment.Subtarget, environment.ScriptingBackend, environment.Options, environment.InvocationOptions, environment.AssetBundleManifestPath, environment.ExtraScriptingDefines, environment.EffectiveDefines, environment.Scenes, null);
        }

        internal static string GetArtifactName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    return "Player.exe";
                case BuildTarget.StandaloneOSX:
                    return "Player.app";
                case BuildTarget.StandaloneLinux64:
                    return "Player.x86_64";
                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, "デスクトップ向けの通常プレイヤーだけに対応しています。");
            }
        }

        private static string NormalizeEntropy(string entropy)
        {
            if (entropy == null || entropy.Length != 8)
                throw new ArgumentException("実行識別子の乱数部には16進数8文字が必要です。", nameof(entropy));
            for (var index = 0; index < entropy.Length; index++)
            {
                var character = entropy[index];
                var hexadecimal = character >= '0' && character <= '9' || character >= 'a' && character <= 'f' || character >= 'A' && character <= 'F';
                if (!hexadecimal)
                    throw new ArgumentException("実行識別子の乱数部には16進数8文字が必要です。", nameof(entropy));
            }

            return entropy.ToLowerInvariant();
        }
    }
}
