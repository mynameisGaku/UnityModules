using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

namespace BuildAssistant.Editor
{
    internal sealed class UnityBuildExecutor
    {
        private readonly Func<BuildAssistantPlan, BuildReport> executeGuarded;

        /// <summary>通常はUnityのビルド処理を使い、試験時だけ同じ境界を通る呼び出しへ差し替えます。</summary>
        internal UnityBuildExecutor(Func<BuildAssistantPlan, BuildReport> executeGuarded = null)
        {
            this.executeGuarded = executeGuarded ?? ExecuteGuarded;
        }

        /// <summary>予約を保持したまま最終前処理を有効化し、Unityのプレイヤービルドを呼び出します。</summary>
        internal BuildReport Execute(BuildAssistantPlan plan, OutputReservation reservation)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (reservation == null || !reservation.IsReserved)
                throw new ArgumentException("有効な出力予約が必要です。", nameof(reservation));

            using (BuildInputGuard.Begin(plan, reservation))
            {
                try
                {
                    var report = executeGuarded(plan);
                    BuildInputGuard.ThrowIfRejected();
                    return report;
                }
                catch
                {
                    BuildInputGuard.ThrowIfRejected();
                    throw;
                }
            }
        }

        private static BuildReport ExecuteGuarded(BuildAssistantPlan plan)
        {
            if (plan.ProfileKind == BuildAssistantProfileKind.Custom)
            {
                var activeProfile = BuildProfile.GetActiveBuildProfile();
                if (activeProfile == null)
                    throw new InvalidOperationException("計画した独自のビルドプロファイルが有効ではありません。");
                var activeProfilePath = AssetDatabase.GetAssetPath(activeProfile) ?? string.Empty;
                var activeProfileGuid = activeProfilePath.Length == 0 ? string.Empty : AssetDatabase.AssetPathToGUID(activeProfilePath);
                if (!StringComparer.Ordinal.Equals(activeProfileGuid, plan.ProfileGuid) || !StringComparer.Ordinal.Equals(activeProfilePath, plan.ProfilePath))
                    throw new InvalidOperationException("計画作成後に別の独自ビルドプロファイルが有効になりました。");
                return BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
                {
                    buildProfile = activeProfile,
                    locationPathName = plan.ArtifactPath,
                    assetBundleManifestPath = plan.AssetBundleManifestPath,
                    options = plan.InvocationOptions
                });
            }

            if (BuildProfile.GetActiveBuildProfile() != null)
                throw new InvalidOperationException("計画作成後に独自のビルドプロファイルが有効になりました。");
            return BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = plan.Scenes.Where(scene => scene.Enabled).Select(scene => scene.AssetPath).ToArray(),
                locationPathName = plan.ArtifactPath,
                assetBundleManifestPath = plan.AssetBundleManifestPath,
                target = plan.Target,
                targetGroup = plan.TargetGroup,
                subtarget = plan.Subtarget,
                extraScriptingDefines = plan.ExtraScriptingDefines.ToArray(),
                options = plan.InvocationOptions
            });
        }
    }
}
