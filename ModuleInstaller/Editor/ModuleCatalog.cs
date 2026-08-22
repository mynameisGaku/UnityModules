// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal static class ModuleCatalog
    {
        private static readonly ModuleCatalogEntry[] CatalogEntries =
        {
            Entry("com.studiogaku.project-setup", "ProjectSetup", "project-setup-v1.13.0", "Project Setup", "Creates recommended project folders, Runtime and Editor assembly definitions, optional test assembly definitions, and Unity-ready version control files, then previews, backs up, applies, and restores Project Settings, build-target application identifiers, scripting backends, API compatibility levels, code generation defaults, duplicate naming defaults, scripting define symbols, Tags, Layers, Build Scenes, and the Play Mode Start Scene."),
            Entry("com.studiogaku.inspector", "Inspector", "inspector-v1.0.0", "Inspector Helpers", "Organizes and validates Inspector input."),
            Entry("com.studiogaku.drawing", "Drawing", "drawing-v1.0.0", "Debug Drawing", "Draws runtime lines, shapes, paths, and labels."),
            Entry("com.studiogaku.build-guard", "BuildGuard", "build-guard-v1.4.0", "Project Issue Scanner", "Finds and repairs missing references in Scenes and Prefabs."),
            Entry("com.studiogaku.reference-finder", "ReferenceFinder", "reference-finder-v1.3.0", "Asset Organizer", "Finds, replaces, and batch-renames asset references."),
            Entry("com.studiogaku.scene-flow", "SceneFlow", "scene-flow-v1.0.0", "Scene Switching", "Serializes Scene loading, activation, and unloading."),
            Entry("com.studiogaku.screen-transition", "ScreenTransition", "screen-transition-v1.0.1", "Screen Fade", "Covers and reveals the screen with a UI Toolkit overlay."),
            Entry("com.studiogaku.adaptive-layout", "AdaptiveLayout", "adaptive-layout-v1.0.0", "Safe Area Layout", "Keeps UI inside notches, cutouts, and changing safe areas."),
            Entry("com.studiogaku.time-control", "TimeControl", "time-control-v1.0.0", "Game Time Control", "Coordinates pause, slow motion, and fast forward."),
            Entry("com.studiogaku.startup-flow", "StartupFlow", "startup-flow-v1.0.0", "Startup Sequence", "Runs startup tasks in a deterministic order."),
            Entry("com.studiogaku.save-system", "SaveSystem", "save-system-v1.0.0", "Save Data", "Provides typed JSON slots, corruption checks, and backup recovery."),
            Entry("com.studiogaku.audio-control", "AudioControl", "audio-control-v1.0.0", "Audio Playback", "Controls pooled voices, limits, priority, handles, and fades."),
            Entry("com.studiogaku.diagnostics-context", "DiagnosticsContext", "diagnostics-context-v1.0.0", "Issue Report Writer", "Writes bounded context, breadcrumbs, and Unity logs to JSON."),
            Entry("com.studiogaku.input-assist", "InputAssist", "input-assist-v1.0.0", "Input Helpers", "Combines stick shaping, direction selection, and button gestures."),
            Entry("com.studiogaku.input-gate", "InputGate", "input-gate-v1.0.0", "Input Pause", "Temporarily disables configured Input System action maps."),
            Entry("com.studiogaku.simulation-clock", "SimulationClock", "simulation-clock-v1.0.0", "Simulation Clock", "Converts elapsed time into reproducible fixed steps."),
            Entry("com.studiogaku.deterministic-random", "DeterministicRandom", "deterministic-random-v1.0.0", "Deterministic Random", "Reproduces random sequences from versioned state."),
            Entry("com.studiogaku.state-fingerprint", "StateFingerprint", "state-fingerprint-v1.0.0", "State Fingerprint", "Checks deterministic state equality with canonical hashes."),
            Entry("com.studiogaku.replay-tape", "ReplayTape", "replay-tape-v1.0.0", "Input Recording", "Records and replays ordered tick commands."),
            Entry("com.studiogaku.canonical-payload", "CanonicalPayload", "canonical-payload-v1.0.0", "Canonical Data", "Encodes deterministic bounded primitive payloads."),
            Entry("com.studiogaku.fixed-point", "FixedPoint", "fixed-point-v1.0.0", "Fixed Point Math", "Provides deterministic signed Q16.16 arithmetic."),
            Entry("com.studiogaku.generational-handle", "GenerationalHandle", "generational-handle-v1.0.0", "Safe Handles", "Distinguishes released handles from reused slots."),
            Entry("com.studiogaku.resource-meter", "ResourceMeter", "resource-meter-v1.0.0", "Resource Meter", "Applies bounded recovery and spending."),
            Entry("com.studiogaku.stat-modifier-stack", "StatModifierStack", "stat-modifier-stack-v1.0.0", "Stat Modifiers", "Combines flat, additive, and multiplicative modifiers."),
            Entry("com.studiogaku.weighted-choice-table", "WeightedChoiceTable", "weighted-choice-table-v1.0.0", "Weighted Choice", "Maps an explicit sample to a weighted entry."),
            Entry("com.studiogaku.piecewise-linear-curve", "PiecewiseLinearCurve", "piecewise-linear-curve-v1.0.0", "Linear Curve", "Evaluates bounded piecewise linear points."),
            Entry("com.studiogaku.rolling-sample-window", "RollingSampleWindow", "rolling-sample-window-v1.0.0", "Rolling Samples", "Keeps bounded samples and summary values."),
            Entry("com.studiogaku.threshold-tier-table", "ThresholdTierTable", "threshold-tier-table-v1.0.0", "Threshold Tiers", "Maps a value to a tier and progress."),
            Entry("com.studiogaku.linear-trend-estimator", "LinearTrendEstimator", "linear-trend-estimator-v1.0.0", "Trend Estimate", "Fits a deterministic linear trend to bounded samples."),
            Entry("com.studiogaku.charge-cooldown", "ChargeCooldown", "charge-cooldown-v1.0.0", "Charge Cooldown", "Calculates charge use and sequential recovery."),
            Entry("com.studiogaku.sample-statistics", "SampleStatistics", "sample-statistics-v1.0.0", "Sample Statistics", "Calculates bounded descriptive statistics."),
            Entry("com.studiogaku.resource-cost-evaluator", "ResourceCostEvaluator", "resource-cost-evaluator-v1.0.0", "Resource Costs", "Checks multi-resource costs without mutating state."),
            Entry("com.studiogaku.numeric-requirement-evaluator", "NumericRequirementEvaluator", "numeric-requirement-evaluator-v1.0.0", "Numeric Requirements", "Evaluates bounded numeric conditions with details."),
            Entry("com.studiogaku.utility-score-evaluator", "UtilityScoreEvaluator", "utility-score-evaluator-v1.0.0", "Utility Scores", "Ranks candidates from weighted utility factors."),
            Entry("com.studiogaku.stable-score-selector", "StableScoreSelector", "stable-score-selector-v1.0.0", "Stable Selection", "Prevents score selection from switching on small differences."),
            Entry("com.studiogaku.weighted-integer-allocator", "WeightedIntegerAllocator", "weighted-integer-allocator-v1.0.0", "Integer Allocation", "Distributes an integer total without losing units."),
            Entry("com.studiogaku.stack-transfer-planner", "StackTransferPlanner", "stack-transfer-planner-v1.0.0", "Stack Transfer", "Plans bounded unit movement without mutating state."),
            Entry("com.studiogaku.timed-stack-resolver", "TimedStackResolver", "timed-stack-resolver-v1.0.0", "Timed Stacks", "Resolves timed effect reapplication policies."),
            Entry("com.studiogaku.periodic-tick-planner", "PeriodicTickPlanner", "periodic-tick-planner-v1.0.0", "Periodic Ticks", "Plans bounded periodic emissions by simulation tick."),
            Entry("com.studiogaku.damage-mitigation-evaluator", "DamageMitigationEvaluator", "damage-mitigation-evaluator-v1.0.0", "Damage Mitigation", "Applies ordered flat and percentage mitigation.")
        };

        private static readonly ModuleBundle[] CatalogBundles =
        {
            Bundle("project-maintenance", "Project Maintenance", "Set up a new project and keep project assets maintainable.", ModuleBundleTier.Recommended,
                "Use this when starting a project or cleaning up project-wide settings, missing references, and asset organization.",
                "Open Tools > Project Setup > Open after installation, preview the profile, and apply only the sections you need.",
                "Installation changes Packages only. The included Editor tools can change Project Settings and project assets after an explicit preview and apply action.",
                "com.studiogaku.project-setup", "com.studiogaku.inspector", "com.studiogaku.drawing", "com.studiogaku.build-guard", "com.studiogaku.reference-finder"),
            Bundle("scene-and-ui", "Scene and UI", "Build a predictable scene, screen, pause, and startup flow.", ModuleBundleTier.Recommended,
                "Use this when scene changes, fades, safe areas, pause behavior, or startup order are being implemented together.",
                "Import one Basics sample from Package Manager and copy only the controller pattern needed by your first scene.",
                "Installation changes Packages only. Runtime behavior starts only after components or services are added to scenes and configured.",
                "com.studiogaku.scene-flow", "com.studiogaku.screen-transition", "com.studiogaku.adaptive-layout", "com.studiogaku.time-control", "com.studiogaku.startup-flow"),
            Bundle("game-services", "Game Services", "Add save data, controlled audio playback, and manual issue reports.", ModuleBundleTier.Recommended,
                "Use this when the project needs reusable services for save slots, audio voices, or player-created diagnostic reports.",
                "Import the sample for the first service you need, then create one explicit owner in the application or bootstrap scene.",
                "Installation changes Packages only. Save and report files are written only when the corresponding service is created and called.",
                "com.studiogaku.save-system", "com.studiogaku.audio-control", "com.studiogaku.diagnostics-context"),
            Bundle("input-support", "Input Support", "Normalize stick and button input and temporarily block gameplay maps.", ModuleBundleTier.Recommended,
                "Use this when Input System callbacks need consistent stick shaping, button gestures, or owner-scoped gameplay blocking.",
                "Import Input Assist Basics first, verify the generated input values, then add Input Gate only where gameplay maps must pause.",
                "Installation adds the packages and their declared Input System dependency. Runtime input maps change only while configured owners are active.",
                "com.studiogaku.input-assist", "com.studiogaku.input-gate"),
            Bundle("deterministic-simulation", "Deterministic Simulation", "Compose fixed steps, reproducible random state, replay data, and stable identity.", ModuleBundleTier.Specialized,
                "Use this only when replay, lockstep, reproducible tests, or deterministic state comparison is a concrete requirement.",
                "Start with Simulation Clock and add one supporting module at a time after its state contract is covered by a test.",
                "Installation changes Packages only. These libraries do not create global runtime owners or modify Project Settings.",
                "com.studiogaku.simulation-clock", "com.studiogaku.deterministic-random", "com.studiogaku.state-fingerprint", "com.studiogaku.replay-tape", "com.studiogaku.canonical-payload", "com.studiogaku.fixed-point", "com.studiogaku.generational-handle"),
            Bundle("game-rules", "Game Rules and Math", "Choose focused calculation libraries for resources, stats, selection, timing, and damage.", ModuleBundleTier.Specialized,
                "Use this when a named game rule needs a small deterministic value type instead of a project-specific service.",
                "Open the individual module list, read the pinned README, and install only the calculation that matches the rule you are implementing.",
                "Installation changes Packages only. The libraries are explicit calculations and do not update scenes or global Unity state.",
                "com.studiogaku.resource-meter", "com.studiogaku.stat-modifier-stack", "com.studiogaku.weighted-choice-table", "com.studiogaku.piecewise-linear-curve", "com.studiogaku.rolling-sample-window", "com.studiogaku.threshold-tier-table", "com.studiogaku.linear-trend-estimator", "com.studiogaku.charge-cooldown", "com.studiogaku.sample-statistics", "com.studiogaku.resource-cost-evaluator", "com.studiogaku.numeric-requirement-evaluator", "com.studiogaku.utility-score-evaluator", "com.studiogaku.stable-score-selector", "com.studiogaku.weighted-integer-allocator", "com.studiogaku.stack-transfer-planner", "com.studiogaku.timed-stack-resolver", "com.studiogaku.periodic-tick-planner", "com.studiogaku.damage-mitigation-evaluator")
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
