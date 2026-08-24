// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ModuleInstaller.Editor.Tests
{
    internal sealed class ModuleInstallCoordinatorTests
    {
        [Test]
        public void TryStart_BeginsOneAtomicPackageManagerRequest()
        {
            var client = new FakeClient();
            var environment = new FakeEnvironment();
            var store = new FakeStore();
            var coordinator = new ModuleInstallCoordinator(client, environment, store);
            var plan = ModuleInstallPlanner.Build(
                new[] { "com.studiogaku.scene-flow", "com.studiogaku.time-control" },
                environment.Installed,
                environment.AssetFolders);

            Assert.That(coordinator.TryStart(plan, out _), Is.True);
            Assert.That(client.CallCount, Is.EqualTo(1));
            Assert.That(client.LastUrls.Count, Is.EqualTo(2));
            Assert.That(coordinator.IsBusy, Is.True);
            coordinator.Tick();
            Assert.That(client.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Tick_SuccessClearsPersistentQueue()
        {
            var client = new FakeClient();
            var store = new FakeStore();
            var coordinator = CreateCoordinator(client, new FakeEnvironment(), store);

            Assert.That(coordinator.TryStart(CreateSinglePlan(), out _), Is.True);
            client.Request.IsCompletedValue = true;
            client.Request.SucceededValue = true;
            coordinator.Tick();

            Assert.That(coordinator.IsBusy, Is.False);
            Assert.That(store.QueueJson, Is.Empty);
            Assert.That(coordinator.LastMessage, Does.StartWith("Installed"));
        }

        [Test]
        public void Tick_FailureStopsWithoutRetryLoop()
        {
            var client = new FakeClient();
            var store = new FakeStore();
            var coordinator = CreateCoordinator(client, new FakeEnvironment(), store);

            Assert.That(coordinator.TryStart(CreateSinglePlan(), out _), Is.True);
            client.Request.IsCompletedValue = true;
            client.Request.SucceededValue = false;
            client.Request.ErrorMessageValue = "network unavailable";
            coordinator.Tick();
            coordinator.Tick();

            Assert.That(coordinator.IsBusy, Is.False);
            Assert.That(client.CallCount, Is.EqualTo(1));
            Assert.That(coordinator.LastMessage, Does.Contain("network unavailable"));
        }

        [Test]
        public void Tick_AfterReloadCompletesWhenTargetsAreAlreadyInstalled()
        {
            var firstClient = new FakeClient();
            var environment = new FakeEnvironment();
            var store = new FakeStore();
            var first = CreateCoordinator(firstClient, environment, store);
            Assert.That(first.TryStart(CreateSinglePlan(), out _), Is.True);

            environment.Installed.Add("com.studiogaku.scene-flow");
            var resumedClient = new FakeClient();
            var resumed = CreateCoordinator(resumedClient, environment, store);
            resumed.Tick();

            Assert.That(resumed.IsBusy, Is.False);
            Assert.That(resumedClient.CallCount, Is.Zero);
            Assert.That(resumed.LastMessage, Does.Contain("already installed").Or.Contain("Every selected"));
        }

        [Test]
        public void TryStartUpdates_ReplacesAnInstalledOlderPackage()
        {
            var client = new FakeClient();
            var environment = new FakeEnvironment();
            environment.Installed.Add("com.studiogaku.project-setup");
            environment.InstalledVersions["com.studiogaku.project-setup"] = "1.0.0";
            var coordinator = CreateCoordinator(client, environment, new FakeStore());
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.project-setup" },
                environment.InstalledVersions,
                environment.AssetFolders);

            Assert.That(coordinator.TryStartUpdates(plan, out _), Is.True);
            Assert.That(client.CallCount, Is.EqualTo(1));
            Assert.That(client.LastUrls[0], Does.EndWith("#project-setup-v1.15.0"));

            client.Request.IsCompletedValue = true;
            client.Request.SucceededValue = true;
            coordinator.Tick();
            Assert.That(coordinator.LastMessage, Does.StartWith("Updated 1"));
        }

        [Test]
        public void Tick_UpdateAfterReloadSkipsPackageThatReachedPinnedVersion()
        {
            var environment = new FakeEnvironment();
            environment.Installed.Add("com.studiogaku.project-setup");
            environment.InstalledVersions["com.studiogaku.project-setup"] = "1.0.0";
            var store = new FakeStore();
            var first = CreateCoordinator(new FakeClient(), environment, store);
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.project-setup" },
                environment.InstalledVersions,
                environment.AssetFolders);
            Assert.That(first.TryStartUpdates(plan, out _), Is.True);

            environment.InstalledVersions["com.studiogaku.project-setup"] = "1.15.0";
            var resumedClient = new FakeClient();
            var resumed = CreateCoordinator(resumedClient, environment, store);
            resumed.Tick();

            Assert.That(resumed.IsBusy, Is.False);
            Assert.That(resumedClient.CallCount, Is.Zero);
            Assert.That(resumed.LastMessage, Does.Contain("up to date"));
        }

        [Test]
        public void TryStart_RechecksLegacyConflictBeforePackageManagerMutation()
        {
            var client = new FakeClient();
            var environment = new FakeEnvironment();
            var coordinator = CreateCoordinator(client, environment, new FakeStore());
            var plan = ModuleInstallPlanner.Build(
                new[] { "com.studiogaku.input-command" },
                environment.Installed,
                environment.AssetFolders);
            environment.Installed.Add("com.studiogaku.input-command-buffer");

            Assert.That(coordinator.TryStart(plan, out var message), Is.False);
            Assert.That(client.CallCount, Is.Zero);
            Assert.That(coordinator.IsBusy, Is.False);
            Assert.That(message, Does.Contain("com.studiogaku.input-command-buffer"));
            Assert.That(coordinator.LastMessage, Does.Contain("com.studiogaku.input-command-buffer"));
        }

        [Test]
        public void TryStartUpdates_RechecksLegacyAssetConflictBeforePackageManagerMutation()
        {
            var client = new FakeClient();
            var environment = new FakeEnvironment();
            environment.Installed.Add("com.studiogaku.input-assist");
            environment.InstalledVersions["com.studiogaku.input-assist"] = "1.0.0";
            var coordinator = CreateCoordinator(client, environment, new FakeStore());
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.input-assist" },
                environment.InstalledVersions,
                environment.AssetFolders);
            environment.AssetFolders.Add("InputRepeat");

            Assert.That(coordinator.TryStartUpdates(plan, out var message), Is.False);
            Assert.That(client.CallCount, Is.Zero);
            Assert.That(coordinator.IsBusy, Is.False);
            Assert.That(message, Does.Contain("Assets/Modules/InputRepeat"));
            Assert.That(coordinator.LastMessage, Does.Contain("Assets/Modules/InputRepeat"));
        }

        [Test]
        public void TryStartUpdates_StopsWhenTargetWasRemovedAfterPlanning()
        {
            var client = new FakeClient();
            var environment = new FakeEnvironment();
            environment.InstalledVersions["com.studiogaku.project-setup"] = "1.0.0";
            var coordinator = CreateCoordinator(client, environment, new FakeStore());
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.project-setup" },
                environment.InstalledVersions,
                environment.AssetFolders);
            environment.InstalledVersions.Remove("com.studiogaku.project-setup");

            Assert.That(coordinator.TryStartUpdates(plan, out var message), Is.False);
            Assert.That(client.CallCount, Is.Zero);
            Assert.That(coordinator.IsBusy, Is.False);
            Assert.That(message, Does.Contain("no longer installed"));
            Assert.That(coordinator.LastMessage, Does.Contain("no longer installed"));
        }

        [Test]
        public void Tick_UpdateAfterReloadStopsWhenTargetWasRemoved()
        {
            var environment = new FakeEnvironment();
            environment.InstalledVersions["com.studiogaku.project-setup"] = "1.0.0";
            var store = new FakeStore();
            var first = CreateCoordinator(new FakeClient(), environment, store);
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.project-setup" },
                environment.InstalledVersions,
                environment.AssetFolders);
            Assert.That(first.TryStartUpdates(plan, out _), Is.True);
            environment.InstalledVersions.Remove("com.studiogaku.project-setup");

            var resumedClient = new FakeClient();
            var resumed = CreateCoordinator(resumedClient, environment, store);
            resumed.Tick();

            Assert.That(resumed.IsBusy, Is.False);
            Assert.That(resumedClient.CallCount, Is.Zero);
            Assert.That(resumed.LastMessage, Does.Contain("no longer installed"));
        }

        [Test]
        public void Tick_UpdateAfterReloadStopsWhenLegacyPackageAppears()
        {
            var environment = new FakeEnvironment();
            environment.InstalledVersions["com.studiogaku.input-assist"] = "1.0.0";
            var store = new FakeStore();
            var first = CreateCoordinator(new FakeClient(), environment, store);
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.input-assist" },
                environment.InstalledVersions,
                environment.AssetFolders);
            Assert.That(first.TryStartUpdates(plan, out _), Is.True);
            environment.InstalledVersions["com.studiogaku.input-repeat"] = "1.0.0";

            var resumedClient = new FakeClient();
            var resumed = CreateCoordinator(resumedClient, environment, store);
            resumed.Tick();

            Assert.That(resumed.IsBusy, Is.False);
            Assert.That(resumedClient.CallCount, Is.Zero);
            Assert.That(resumed.LastMessage, Does.Contain("com.studiogaku.input-repeat"));
        }

        [Test]
        public void Tick_UpdateAfterReloadStopsWhenLegacyAssetCopyAppears()
        {
            var environment = new FakeEnvironment();
            environment.InstalledVersions["com.studiogaku.input-assist"] = "1.0.0";
            var store = new FakeStore();
            var first = CreateCoordinator(new FakeClient(), environment, store);
            var plan = ModuleInstallPlanner.BuildUpdates(
                new[] { "com.studiogaku.input-assist" },
                environment.InstalledVersions,
                environment.AssetFolders);
            Assert.That(first.TryStartUpdates(plan, out _), Is.True);
            environment.AssetFolders.Add("InputRepeat");

            var resumedClient = new FakeClient();
            var resumed = CreateCoordinator(resumedClient, environment, store);
            resumed.Tick();

            Assert.That(resumed.IsBusy, Is.False);
            Assert.That(resumedClient.CallCount, Is.Zero);
            Assert.That(resumed.LastMessage, Does.Contain("Assets/Modules/InputRepeat"));
        }

        private static ModuleInstallCoordinator CreateCoordinator(
            FakeClient client,
            FakeEnvironment environment,
            FakeStore store)
        {
            return new ModuleInstallCoordinator(client, environment, store);
        }

        private static ModuleInstallPlan CreateSinglePlan()
        {
            return ModuleInstallPlanner.Build(
                new[] { "com.studiogaku.scene-flow" },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private sealed class FakeClient : IModulePackageClient
        {
            internal int CallCount { get; private set; }
            internal IReadOnlyList<string> LastUrls { get; private set; } = Array.Empty<string>();
            internal FakeRequest Request { get; } = new FakeRequest();

            public IModuleInstallRequest AddAndRemove(IReadOnlyList<string> packageUrls)
            {
                CallCount++;
                LastUrls = packageUrls;
                return Request;
            }
        }

        private sealed class FakeRequest : IModuleInstallRequest
        {
            internal bool IsCompletedValue { get; set; }
            internal bool SucceededValue { get; set; }
            internal string ErrorMessageValue { get; set; } = string.Empty;
            public bool IsCompleted => IsCompletedValue;
            public bool Succeeded => SucceededValue;
            public string ErrorMessage => ErrorMessageValue;
        }

        private sealed class FakeEnvironment : IModuleInstallEnvironment
        {
            internal ISet<string> Installed { get; } = new HashSet<string>(StringComparer.Ordinal);
            internal Dictionary<string, string> InstalledVersions { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
            internal ISet<string> AssetFolders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public ISet<string> GetInstalledPackageNames() => Installed;
            public IReadOnlyDictionary<string, string> GetInstalledPackageVersions() => InstalledVersions;
            public ISet<string> GetAssetModuleFolders() => AssetFolders;
        }

        private sealed class FakeStore : IModuleInstallStateStore
        {
            public string QueueJson { get; set; } = string.Empty;
            public string LastMessage { get; set; } = string.Empty;
        }
    }
}
