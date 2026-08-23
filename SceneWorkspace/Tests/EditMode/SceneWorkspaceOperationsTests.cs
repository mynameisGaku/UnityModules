using System;
using System.Collections.Generic;
using NUnit.Framework;
using SceneWorkspace.Editor;
using UnityEngine;

namespace SceneWorkspace.Tests
{
    [TestFixture]
    internal sealed class SceneWorkspaceOperationsTests
    {
        private readonly List<SceneWorkspaceProfile> profiles = new List<SceneWorkspaceProfile>();

        [TearDown]
        public void TearDown()
        {
            foreach (var profile in profiles)
            {
                if (profile != null)
                    UnityEngine.Object.DestroyImmediate(profile);
            }
            profiles.Clear();
        }

        [Test]
        public void CaptureReturnsValidatedDetachedSetup()
        {
            var current = SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true));
            var gateway = new FakeSceneWorkspaceGateway { DefaultCurrent = current };

            var result = new SceneWorkspaceOperations(gateway).CaptureCurrentSetup();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Fingerprint, Has.Length.EqualTo(64));
            Assert.That(result.Scenes.Count, Is.EqualTo(1));
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [Test]
        public void ApplyRestoresAndPostVerifiesTarget()
        {
            var original = Original();
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, original, Snapshot(target));
            gateway.EnqueueProfile(Profile(target), Profile(target));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ApplyAttempted, Is.True);
            Assert.That(gateway.RestoreCalls.Count, Is.EqualTo(1));
            Assert.That(SceneWorkspaceSnapshotComparer.Matches(plan.TargetScenes, gateway.RestoreCalls[0], out var difference), Is.True, difference);
        }

        [Test]
        public void IdenticalSetupSucceedsWithoutRestore()
        {
            var original = Original();
            var gateway = ConfiguredGateway(original, original);
            gateway.EnqueueCurrent(original, original);
            gateway.EnqueueProfile(Profile(original), Profile(original));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ApplyAttempted, Is.False);
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [Test]
        public void ChangedCurrentSetupConsumesPlanWithoutRestore()
        {
            var original = Original();
            var changed = SceneWorkspaceTestData.Current(
                SceneWorkspaceTestData.Scene("Main", 0, true, true),
                SceneWorkspaceTestData.Scene("Extra", 1, false, false));
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, changed);
            gateway.EnqueueProfile(Profile(target), Profile(target));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var first = operations.Apply(plan);
            var second = operations.Apply(plan);

            Assert.That(first.ApplyError, Is.EqualTo(SceneWorkspaceError.StalePlan));
            Assert.That(second.ApplyError, Is.EqualTo(SceneWorkspaceError.PlanAlreadyConsumed));
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [Test]
        public void ChangedProfileRevisionIsStaleWithoutRestore()
        {
            var original = Original();
            var target = Target();
            var changedTarget = new[]
            {
                SceneWorkspaceTestData.Scene("Gameplay", 0, true, true),
                SceneWorkspaceTestData.Scene("Lighting", 1, false, false)
            };
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, original);
            gateway.EnqueueProfile(Profile(target), Profile(changedTarget));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.ApplyError, Is.EqualTo(SceneWorkspaceError.StalePlan));
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [Test]
        public void DifferentPlanObjectWithSameGenerationIsRejected()
        {
            var original = Original();
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original);
            gateway.EnqueueProfile(Profile(target));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(SceneWorkspaceTestData.ClonePlan(plan));

            Assert.That(result.ApplyError, Is.EqualTo(SceneWorkspaceError.StalePlan));
            Assert.That(gateway.CurrentCaptureCount, Is.EqualTo(1));
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [Test]
        public void ApplyExceptionRollsBackAndVerifiesOriginal()
        {
            var original = Original();
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, original, original);
            gateway.EnqueueProfile(Profile(target), Profile(target));
            gateway.RestoreHandler = (call, scenes) =>
            {
                if (call == 1)
                    throw new InvalidOperationException("partial apply");
            };
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.ApplyError, Is.EqualTo(SceneWorkspaceError.ApplyFailed));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(result.RollbackError, Is.EqualTo(SceneWorkspaceError.None));
            Assert.That(gateway.RestoreCalls.Count, Is.EqualTo(2));
        }

        [Test]
        public void VerificationMismatchRollsBackOriginal()
        {
            var original = Original();
            var target = Target();
            var mismatch = SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Wrong", 0, true, true));
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, original, mismatch, original);
            gateway.EnqueueProfile(Profile(target), Profile(target));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.ApplyError, Is.EqualTo(SceneWorkspaceError.VerificationFailed));
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(gateway.RestoreCalls.Count, Is.EqualTo(2));
        }

        [Test]
        public void RollbackFailureIsReportedSeparately()
        {
            var original = Original();
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, original);
            gateway.EnqueueProfile(Profile(target), Profile(target));
            gateway.RestoreHandler = (call, scenes) => throw new InvalidOperationException(call == 1 ? "apply failed" : "rollback failed");
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.ApplyError, Is.EqualTo(SceneWorkspaceError.ApplyFailed));
            Assert.That(result.RollbackError, Is.EqualTo(SceneWorkspaceError.RollbackFailed));
            Assert.That(result.RollbackAttempted, Is.True);
            Assert.That(result.RollbackSucceeded, Is.False);
        }

        [TestCase(SceneWorkspaceError.PlayModeActive)]
        [TestCase(SceneWorkspaceError.EditorBusy)]
        [TestCase(SceneWorkspaceError.PrefabStageOpen)]
        [TestCase(SceneWorkspaceError.DirtyScene)]
        [TestCase(SceneWorkspaceError.UntitledScene)]
        [TestCase(SceneWorkspaceError.MissingScene)]
        [TestCase(SceneWorkspaceError.DuplicateScene)]
        [TestCase(SceneWorkspaceError.InvalidActiveScene)]
        [TestCase(SceneWorkspaceError.NoLoadedScene)]
        public void ApplyGuardFailuresNeverCallRestore(SceneWorkspaceError expected)
        {
            var original = Original();
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, GuardSnapshot(expected));
            gateway.EnqueueProfile(Profile(target), Profile(target));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.ApplyError, Is.EqualTo(expected));
            Assert.That(result.ApplyAttempted, Is.False);
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [Test]
        public void CompilingApplyGuardNeverCallsRestore()
        {
            var original = Original();
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.EnqueueCurrent(original, SceneWorkspaceTestData.WithFlags(compiling: true));
            gateway.EnqueueProfile(Profile(target), Profile(target));
            var operations = new SceneWorkspaceOperations(gateway);
            var plan = operations.Preview(NewProfile());

            var result = operations.Apply(plan);

            Assert.That(result.ApplyError, Is.EqualTo(SceneWorkspaceError.EditorBusy));
            Assert.That(result.ApplyAttempted, Is.False);
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [TestCase(SceneWorkspaceError.MissingScene)]
        [TestCase(SceneWorkspaceError.DuplicateScene)]
        [TestCase(SceneWorkspaceError.InvalidActiveScene)]
        [TestCase(SceneWorkspaceError.NoLoadedScene)]
        public void InvalidProfilePreviewNeverCallsRestore(SceneWorkspaceError expected)
        {
            var original = Original();
            var gateway = new FakeSceneWorkspaceGateway();
            gateway.EnqueueCurrent(original);
            gateway.EnqueueProfile(InvalidProfile(expected));
            var operations = new SceneWorkspaceOperations(gateway);

            var plan = operations.Preview(NewProfile());

            Assert.That(plan.Error, Is.EqualTo(expected));
            Assert.That(plan.Generation, Is.Zero);
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        [Test]
        public void RegistryEvictsOldestGenerationFailClosed()
        {
            var original = Original();
            var target = Target();
            var gateway = ConfiguredGateway(original, target);
            gateway.DefaultCurrent = original;
            gateway.DefaultProfile = Profile(target);
            var operations = new SceneWorkspaceOperations(gateway);
            var plans = new List<SceneWorkspacePlan>();
            for (var index = 0; index < 65; index++)
                plans.Add(operations.Preview(NewProfile()));

            var result = operations.Apply(plans[0]);

            Assert.That(result.ApplyError, Is.EqualTo(SceneWorkspaceError.StalePlan));
            Assert.That(gateway.RestoreCalls, Is.Empty);
        }

        private SceneWorkspaceProfile NewProfile()
        {
            var profile = SceneWorkspaceTestData.CreateProfileAsset();
            profiles.Add(profile);
            return profile;
        }

        private static SceneWorkspaceSnapshot Original()
        {
            return SceneWorkspaceTestData.Current(
                SceneWorkspaceTestData.Scene("Main", 0, true, true),
                SceneWorkspaceTestData.Scene("Lighting", 1, false, false));
        }

        private static SceneWorkspaceSceneState[] Target()
        {
            return new[]
            {
                SceneWorkspaceTestData.Scene("Gameplay", 0, true, true),
                SceneWorkspaceTestData.Scene("Lighting", 1, true, false)
            };
        }

        private static SceneWorkspaceSnapshot Snapshot(SceneWorkspaceSceneState[] scenes)
        {
            return SceneWorkspaceTestData.Current(scenes);
        }

        private static SceneWorkspaceProfileSnapshot Profile(SceneWorkspaceSnapshot snapshot)
        {
            return SceneWorkspaceTestData.Profile(ToArray(snapshot));
        }

        private static SceneWorkspaceProfileSnapshot Profile(SceneWorkspaceSceneState[] scenes)
        {
            return SceneWorkspaceTestData.Profile(scenes);
        }

        private static SceneWorkspaceSceneState[] ToArray(SceneWorkspaceSnapshot snapshot)
        {
            var result = new SceneWorkspaceSceneState[snapshot.Scenes.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = snapshot.Scenes[index];
            return result;
        }

        private static FakeSceneWorkspaceGateway ConfiguredGateway(SceneWorkspaceSnapshot current, SceneWorkspaceSceneState[] target)
        {
            return new FakeSceneWorkspaceGateway { DefaultCurrent = current, DefaultProfile = Profile(target) };
        }

        private static FakeSceneWorkspaceGateway ConfiguredGateway(SceneWorkspaceSnapshot current, SceneWorkspaceSnapshot target)
        {
            return new FakeSceneWorkspaceGateway { DefaultCurrent = current, DefaultProfile = Profile(target) };
        }

        private static SceneWorkspaceSnapshot GuardSnapshot(SceneWorkspaceError error)
        {
            switch (error)
            {
                case SceneWorkspaceError.PlayModeActive:
                    return SceneWorkspaceTestData.WithFlags(play: true);
                case SceneWorkspaceError.EditorBusy:
                    return SceneWorkspaceTestData.WithFlags(updating: true);
                case SceneWorkspaceError.PrefabStageOpen:
                    return SceneWorkspaceTestData.WithFlags(prefab: true);
                case SceneWorkspaceError.DirtyScene:
                    return SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true, dirty: true));
                case SceneWorkspaceError.UntitledScene:
                    return SceneWorkspaceTestData.Current(new SceneWorkspaceSceneState(0, string.Empty, string.Empty, true, true, true, false));
                case SceneWorkspaceError.MissingScene:
                    return SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true, exists: false));
                case SceneWorkspaceError.DuplicateScene:
                    return SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, true, true), SceneWorkspaceTestData.Scene("Main", 1, false, false));
                case SceneWorkspaceError.InvalidActiveScene:
                    return SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, false, true));
                case SceneWorkspaceError.NoLoadedScene:
                    return SceneWorkspaceTestData.Current(SceneWorkspaceTestData.Scene("Main", 0, false, false));
                default:
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }

        private static SceneWorkspaceProfileSnapshot InvalidProfile(SceneWorkspaceError error)
        {
            switch (error)
            {
                case SceneWorkspaceError.MissingScene:
                    return SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Missing", 0, true, true, exists: false));
                case SceneWorkspaceError.DuplicateScene:
                    return SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Main", 0, true, true), SceneWorkspaceTestData.Scene("Main", 1, false, false));
                case SceneWorkspaceError.InvalidActiveScene:
                    return SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Main", 0, false, true));
                case SceneWorkspaceError.NoLoadedScene:
                    return SceneWorkspaceTestData.Profile(SceneWorkspaceTestData.Scene("Main", 0, false, false));
                default:
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }
    }
}
