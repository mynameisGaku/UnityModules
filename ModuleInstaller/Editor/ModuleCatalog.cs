// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal static class ModuleCatalog
    {
        private static readonly ModuleCatalogEntry[] CatalogEntries =
        {
            Entry("com.studiogaku.project-setup", "ProjectSetup", "project-setup-v1.15.0", "Project Setup", "Creates recommended project folders, Runtime and Editor assembly definitions, optional test assembly definitions, and Unity-ready version control files, then previews, backs up, applies, and restores Project Settings, build-target application identifiers, scripting backends, API compatibility levels, managed stripping levels, IL2CPP code generation, code generation defaults, duplicate naming defaults, scripting define symbols, Tags, Layers, Build Scenes, and the Play Mode Start Scene."),
            Entry("com.studiogaku.inspector", "Inspector", "inspector-v1.0.0", "Inspector Helpers", "Organizes and validates Inspector input."),
            Entry("com.studiogaku.drawing", "Drawing", "drawing-v1.0.0", "Debug Drawing", "Draws runtime lines, shapes, paths, and labels."),
            Entry("com.studiogaku.build-guard", "BuildGuard", "build-guard-v1.4.0", "Project Issue Scanner", "Finds and repairs missing references in Scenes and Prefabs."),
            Entry("com.studiogaku.reference-finder", "ReferenceFinder", "reference-finder-v1.3.0", "Asset Organizer", "Finds, replaces, and batch-renames asset references."),
            Entry("com.studiogaku.asset-import-audit", "AssetImportAudit", "asset-import-audit-v1.1.0", "Texture Import Settings", "Audits and batch-applies shared, Standalone, Android, and iOS texture import settings after a reviewed preview."),
            Entry("com.studiogaku.build-assistant", "BuildAssistant", "build-assistant-v1.0.0", "Build Assistant", "Previews and executes reviewed desktop standalone builds in new output folders, then records bounded history, size changes, and JSON reports."),
            Entry("com.studiogaku.scene-workspace", "SceneWorkspace", "scene-workspace-v1.0.0", "Scene Workspace", "Captures, previews, and safely switches ordered multi-scene editor workspaces with stale-plan checks, post-verification, and rollback reporting."),
            Entry("com.studiogaku.play-mode-tuning", "PlayModeTuning", "play-mode-tuning-v1.0.0", "Play Mode Tuning", "Carries selected Play Mode property edits back into saved Scenes after a reviewed preview, stale-plan check and rollback report."),
            Entry("com.studiogaku.scene-flow", "SceneFlow", "scene-flow-v1.0.0", "Scene Switching", "Serializes Scene loading, activation, and unloading."),
            Entry("com.studiogaku.screen-transition", "ScreenTransition", "screen-transition-v1.0.1", "Screen Fade", "Covers and reveals the screen with a UI Toolkit overlay."),
            Entry("com.studiogaku.adaptive-layout", "AdaptiveLayout", "adaptive-layout-v1.0.0", "Safe Area Layout", "Keeps UI inside notches, cutouts, and changing safe areas."),
            Entry("com.studiogaku.time-control", "TimeControl", "time-control-v1.0.0", "Game Time Control", "Coordinates pause, slow motion, and fast forward."),
            Entry("com.studiogaku.startup-flow", "StartupFlow", "startup-flow-v1.0.0", "Startup Sequence", "Runs startup tasks in a deterministic order."),
            Entry("com.studiogaku.save-system", "SaveSystem", "save-system-v1.0.0", "Save Data", "Provides typed JSON slots, corruption checks, and backup recovery."),
            Entry("com.studiogaku.audio-control", "AudioControl", "audio-control-v1.0.0", "Audio Playback", "Controls pooled voices, limits, priority, handles, and fades."),
            Entry("com.studiogaku.diagnostics-context", "DiagnosticsContext", "diagnostics-context-v1.0.0", "Issue Report Writer", "Writes bounded context, breadcrumbs, and Unity logs to JSON."),
            Entry("com.studiogaku.input-assist", "InputAssist", "input-assist-v2.0.0", "Input Helpers", "Shapes stick and trigger values with radial dead zones, response curves, rate limits, direction quantization and weighted mixing, and classifies button taps, holds, repeats and multi-taps."),
            Entry("com.studiogaku.input-command", "InputCommand", "input-command-v1.0.0", "Input Commands", "Buffers, debounces and recognizes tick-based command input as sequences, chords, priority arbitration and opposing-axis resolution."),
            Entry("com.studiogaku.input-gate", "InputGate", "input-gate-v1.0.0", "Input Pause", "Temporarily disables configured Input System action maps."),
            Entry("com.studiogaku.gameplay-rules", "GameplayRules", "gameplay-rules-v1.0.0", "Gameplay Rules", "Evaluates resources and costs, stat modifiers, weighted selection and allocation, curves and tiers, sample statistics and trends, timed stacks and periodic ticks, requirements, utility and threat scores, and damage mitigation."),
            Entry("com.studiogaku.deterministic-simulation", "DeterministicSimulation", "deterministic-simulation-v1.0.0", "Deterministic Simulation", "Combines fixed-step clocks, reproducible random state, canonical payload encoding, fixed-point arithmetic, replay tapes, state fingerprints and generational handles into one reproducibility base.")
        };

        private static readonly ModuleBundle[] CatalogBundles =
        {
            Bundle("project-maintenance", "Project Maintenance", "Set up a new project, keep project assets and texture import settings maintainable, and run reviewed desktop builds.", ModuleBundleTier.Recommended,
                "Use this when starting a project, cleaning up project-wide settings and assets, or preparing a desktop standalone build.",
                "Start with Tools > Project Setup > Open, audit textures from Tools > Asset Import Audit > Open, then preview a release plan from Tools > Build Assistant > Open.",
                "Installation changes Packages only. Project Setup and Asset Import Audit change selected settings only after explicit preview and apply actions. Build Assistant writes new build output and bounded Library history only after review and confirmation.",
                "com.studiogaku.project-setup", "com.studiogaku.asset-import-audit", "com.studiogaku.inspector", "com.studiogaku.drawing", "com.studiogaku.build-guard", "com.studiogaku.reference-finder", "com.studiogaku.build-assistant"),
            Bundle("scene-and-ui", "Scene and UI", "Prepare reusable Editor scene workspaces, then build a predictable runtime scene, screen, pause, and startup flow.", ModuleBundleTier.Recommended,
                "Use this when switching between multi-scene editing setups, carrying Play Mode edits back into saved Scenes, or implementing scene changes, fades, safe areas, pause behavior, or startup order together.",
                "Start with Tools > Scene Workspace > Open, select or create a profile, configure its ordered scenes, then Preview Changes before switching.",
                "Installation changes Packages only. Create New Profile creates an asset under Assets. Editing or capturing a setup changes only the selected profile and does not save it automatically. After Preview and confirmation, Switch Workspace changes open Editor Scene order, Loaded state, and Active Scene; it never saves or discards Scene changes. Runtime behavior starts only after components or services are added and configured.",
                "com.studiogaku.scene-workspace", "com.studiogaku.play-mode-tuning", "com.studiogaku.scene-flow", "com.studiogaku.screen-transition", "com.studiogaku.adaptive-layout", "com.studiogaku.time-control", "com.studiogaku.startup-flow"),
            Bundle("game-services", "Game Services", "Add save data, controlled audio playback, and manual issue reports.", ModuleBundleTier.Recommended,
                "Use this when the project needs reusable services for save slots, audio voices, or player-created diagnostic reports.",
                "Import the sample for the first service you need, then create one explicit owner in the application or bootstrap scene.",
                "Installation changes Packages only. Save and report files are written only when the corresponding service is created and called.",
                "com.studiogaku.save-system", "com.studiogaku.audio-control", "com.studiogaku.diagnostics-context"),
            Bundle("input-support", "Input Support", "Normalize stick and button input, recognize buffered command gestures, and temporarily block gameplay maps.", ModuleBundleTier.Recommended,
                "Use this when Input System callbacks need consistent stick shaping, button gestures, or owner-scoped gameplay blocking.",
                "Import an Input Assist sample first, verify the generated input values, add Input Command where buffered or multi-button gestures are needed, then add Input Gate only where gameplay maps must pause.",
                "Installation adds the packages and their declared Input System dependency. Runtime input maps change only while configured owners are active.",
                "com.studiogaku.input-assist", "com.studiogaku.input-command", "com.studiogaku.input-gate"),
            Bundle("deterministic-simulation", "Deterministic Simulation", "Compose fixed steps, reproducible random state, replay data, and stable identity.", ModuleBundleTier.Specialized,
                "Use this only when replay, lockstep, reproducible tests, or deterministic state comparison is a concrete requirement.",
                "Install the package, import the Simulation Clock sample first, then adopt one namespace at a time after its state contract is covered by a test.",
                "Installation changes Packages only. These libraries do not create global runtime owners or modify Project Settings.",
                "com.studiogaku.deterministic-simulation"),
            Bundle("game-rules", "Game Rules and Math", "Add one library of deterministic calculations for resources, stats, selection, timing, and damage.", ModuleBundleTier.Specialized,
                "Use this when a named game rule needs a small deterministic value type instead of a project-specific service.",
                "Install the package, then open the README namespace table and use only the namespace that matches the rule you are implementing.",
                "Installation changes Packages only. The libraries are explicit calculations and do not update scenes or global Unity state.",
                "com.studiogaku.gameplay-rules")
        };

        internal static IReadOnlyList<ModuleCatalogEntry> Entries => CatalogEntries;
        internal static IReadOnlyList<ModuleBundle> Bundles => CatalogBundles;

        internal static bool TryFindEntry(string packageName, out ModuleCatalogEntry entry)
        {
            for (var index = 0; index < CatalogEntries.Length; index++)
            {
                if (string.Equals(CatalogEntries[index].PackageName, packageName, StringComparison.Ordinal))
                {
                    entry = CatalogEntries[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        internal static bool TryFindBundle(string id, out ModuleBundle bundle)
        {
            for (var index = 0; index < CatalogBundles.Length; index++)
            {
                if (string.Equals(CatalogBundles[index].Id, id, StringComparison.Ordinal))
                {
                    bundle = CatalogBundles[index];
                    return true;
                }
            }

            bundle = null;
            return false;
        }

        private static ModuleCatalogEntry Entry(string packageName, string folderName, string tag, string displayName, string summary)
        {
            return new ModuleCatalogEntry(packageName, folderName, tag, displayName, summary);
        }

        private static ModuleBundle Bundle(
            string id,
            string displayName,
            string summary,
            ModuleBundleTier tier,
            string useWhen,
            string firstStep,
            string changeScope,
            params string[] packageNames)
        {
            return new ModuleBundle(id, displayName, summary, tier, useWhen, firstStep, changeScope, packageNames);
        }
    }
}
