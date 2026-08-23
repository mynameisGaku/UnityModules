using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

namespace BuildAssistant.Editor
{
    internal sealed class UnityBuildExecutor
    {
        internal BuildReport Execute(BuildAssistantPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            if (plan.ProfileKind == BuildAssistantProfileKind.Custom)
            {
                var activeProfile = BuildProfile.GetActiveBuildProfile();
                if (activeProfile == null)
                    throw new InvalidOperationException("The planned custom BuildProfile is no longer active.");
                var activeProfilePath = AssetDatabase.GetAssetPath(activeProfile) ?? string.Empty;
                var activeProfileGuid = activeProfilePath.Length == 0 ? string.Empty : AssetDatabase.AssetPathToGUID(activeProfilePath);
                if (!StringComparer.Ordinal.Equals(activeProfileGuid, plan.ProfileGuid) || !StringComparer.Ordinal.Equals(activeProfilePath, plan.ProfilePath))
                    throw new InvalidOperationException("A different custom BuildProfile became active after preview.");
                return BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
                {
                    buildProfile = activeProfile,
                    locationPathName = plan.ArtifactPath,
                    assetBundleManifestPath = plan.AssetBundleManifestPath,
                    options = plan.InvocationOptions
                });
            }

            if (BuildProfile.GetActiveBuildProfile() != null)
                throw new InvalidOperationException("A custom BuildProfile became active after preview.");
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
