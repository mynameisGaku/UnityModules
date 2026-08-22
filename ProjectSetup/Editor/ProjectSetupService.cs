// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectSetup.Editor
{
    internal sealed class ProjectSetupService
    {
        private readonly IProjectSetupEnvironment _environment;
        private readonly IProjectSetupBackupStore _backupStore;

        internal ProjectSetupService(IProjectSetupEnvironment environment, IProjectSetupBackupStore backupStore)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _backupStore = backupStore ?? throw new ArgumentNullException(nameof(backupStore));
        }

        internal bool HasBackup => _backupStore.Exists;

        internal ProjectSetupPlan Preview(ProjectSetupProfile profile)
        {
            if (!_environment.IsAvailable)
            {
                return new ProjectSetupPlan(Array.Empty<ProjectSetupChange>(), new[] { "Project Settings are unavailable while Unity is busy or entering Play Mode." });
            }

            return ProjectSetupPlanner.Build(profile, _environment.Capture());
        }

        internal ProjectSetupApplyResult Apply(ProjectSetupProfile profile)
        {
            var plan = Preview(profile);
            if (!plan.IsValid)
            {
                return new ProjectSetupApplyResult(false, plan.Errors[0], plan);
            }

            if (!plan.HasChanges)
            {
                return new ProjectSetupApplyResult(true, "The project already matches the profile.", plan);
            }

            var before = _environment.Capture();
            try
            {
                _backupStore.Save(before);
            }
            catch (Exception exception)
            {
                return new ProjectSetupApplyResult(false, $"Backup could not be saved, so no Project Settings were changed: {exception.Message}", plan);
            }

            try
            {
                _environment.Apply(profile);
                var verification = ProjectSetupPlanner.Build(profile, _environment.Capture());
                if (!verification.IsValid || verification.HasChanges)
                {
                    throw new InvalidOperationException("Project Settings did not match the profile after applying it.");
                }

                return new ProjectSetupApplyResult(true, $"Applied {plan.Changes.Count} setting change(s).", plan);
            }
            catch (Exception exception)
            {
                TryRestore(before);
                return new ProjectSetupApplyResult(false, $"Apply failed and the previous values were restored where possible: {exception.Message}", plan);
            }
        }

        internal ProjectSetupPlan PreviewRestore(out ProjectSetupSnapshot backup, out string error)
        {
            backup = default;
            error = string.Empty;
            if (!_environment.IsAvailable)
            {
                error = "Project Settings are unavailable while Unity is busy or entering Play Mode.";
                return new ProjectSetupPlan(Array.Empty<ProjectSetupChange>(), new[] { error });
            }

            if (!_backupStore.TryLoad(out backup, out error))
            {
                return new ProjectSetupPlan(Array.Empty<ProjectSetupChange>(), new[] { error });
            }

            return BuildSnapshotPlan(_environment.Capture(), backup);
        }

        internal ProjectSetupApplyResult RestoreLast()
        {
            var plan = PreviewRestore(out var backup, out var error);
            if (!plan.IsValid)
            {
                return new ProjectSetupApplyResult(false, error, plan);
            }

            if (!plan.HasChanges)
            {
                return new ProjectSetupApplyResult(true, "The project already matches the last backup.", plan);
            }

            var beforeRestore = _environment.Capture();
            try
            {
                _environment.Apply(backup);
                if (!backup.Matches(_environment.Capture()))
                {
                    throw new InvalidOperationException("Project Settings did not match the backup after restoring it.");
                }

                return new ProjectSetupApplyResult(true, $"Restored {plan.Changes.Count} setting change(s).", plan);
            }
            catch (Exception exception)
            {
                TryRestore(beforeRestore);
                return new ProjectSetupApplyResult(false, $"Restore failed and the pre-restore values were restored where possible: {exception.Message}", plan);
            }
        }

        private void TryRestore(ProjectSetupSnapshot snapshot)
        {
            try
            {
                _environment.Apply(snapshot);
            }
            catch
            {
            }
        }

        private static ProjectSetupPlan BuildSnapshotPlan(ProjectSetupSnapshot current, ProjectSetupSnapshot desired)
        {
            var profile = UnityEngine.ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                profile.Capture(desired);
                profile.ConfigureTags = false;
                profile.ConfigureLayers = false;
                profile.ConfigureSortingLayers = false;
                profile.ConfigureBuildScenes = false;
                var scalarPlan = ProjectSetupPlanner.Build(profile, current);
                var changes = new List<ProjectSetupChange>(scalarPlan.Changes);
                var errors = new List<string>(scalarPlan.Errors);
                if (desired.HasTagManagerData)
                {
                    if (desired.Layers.Length != current.Layers.Length)
                    {
                        errors.Add("The backup Layer slot count does not match this Unity project.");
                    }

                    if (!desired.CustomTags.SequenceEqual(current.CustomTags, StringComparer.Ordinal))
                    {
                        changes.Add(new ProjectSetupChange(
                            ProjectSetupSettingKey.Tags,
                            "Tags",
                            $"{current.CustomTags.Length} custom tag(s)",
                            $"Restore {desired.CustomTags.Length} custom tag(s) exactly"));
                    }

                    if (!desired.Layers.SequenceEqual(current.Layers, StringComparer.Ordinal))
                    {
                        changes.Add(new ProjectSetupChange(
                            ProjectSetupSettingKey.Layers,
                            "Layers",
                            $"{CountNamedUserLayers(current.Layers)} named user layer(s)",
                            $"Restore {CountNamedUserLayers(desired.Layers)} named user layer(s) exactly"));
                    }

                    if (!desired.SortingLayers.SequenceEqual(current.SortingLayers))
                    {
                        changes.Add(new ProjectSetupChange(
                            ProjectSetupSettingKey.SortingLayers,
                            "Sorting Layers",
                            $"{current.SortingLayers.Length} layer(s)",
                            $"Restore {desired.SortingLayers.Length} layer(s) exactly"));
                    }
                }

                if (desired.HasBuildSceneData)
                {
                    if (!current.HasBuildSceneData
                        || !string.Equals(desired.BuildSceneTargetId, current.BuildSceneTargetId, StringComparison.Ordinal))
                    {
                        errors.Add($"The active Build Scene target must remain '{desired.BuildSceneTargetLabel}' before restoring this backup.");
                    }
                    else if (!desired.BuildScenes.SequenceEqual(current.BuildScenes))
                    {
                        changes.Add(new ProjectSetupChange(
                            ProjectSetupSettingKey.BuildScenes,
                            "Build Scenes",
                            ProjectSetupPlanner.FormatBuildScenes(current.BuildScenes),
                            ProjectSetupPlanner.FormatBuildScenes(desired.BuildScenes)));
                    }
                }

                return new ProjectSetupPlan(changes, errors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        private static int CountNamedUserLayers(IReadOnlyList<string> layers)
        {
            var count = 0;
            for (var index = 8; index < layers.Count; index++)
            {
                if (!string.IsNullOrEmpty(layers[index]))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
