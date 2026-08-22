// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModuleInstaller.Editor
{
    internal enum ModuleInstallOperation
    {
        Install = 0,
        Update = 1
    }

    internal sealed class ModuleInstallCoordinator
    {
        private readonly IModulePackageClient _client;
        private readonly IModuleInstallEnvironment _environment;
        private readonly IModuleInstallStateStore _store;
        private IModuleInstallRequest _request;

        internal ModuleInstallCoordinator(
            IModulePackageClient client,
            IModuleInstallEnvironment environment,
            IModuleInstallStateStore store)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        internal bool IsBusy => ReadQueue().Items.Count > 0;
        internal string LastMessage => _store.LastMessage ?? string.Empty;

        internal bool TryStart(ModuleInstallPlan plan, out string message)
        {
            return TryStart(plan, ModuleInstallOperation.Install, out message);
        }

        internal bool TryStartUpdates(ModuleInstallPlan plan, out string message)
        {
            return TryStart(plan, ModuleInstallOperation.Update, out message);
        }

        private bool TryStart(
            ModuleInstallPlan plan,
            ModuleInstallOperation operation,
            out string message)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (IsBusy)
            {
                message = "Another package operation is already in progress.";
                return false;
            }

            if (plan.Issues.Count > 0)
            {
                message = plan.Issues[0].Message;
                return false;
            }

            if (plan.Entries.Count == 0)
            {
                message = operation == ModuleInstallOperation.Update
                    ? "Every installed catalog module is up to date."
                    : plan.InstalledCount > 0
                        ? "Every selected module is already installed."
                        : "No module was selected.";
                return false;
            }

            var state = new ModuleInstallQueueState
            {
                Operation = operation
            };
            for (var index = 0; index < plan.Entries.Count; index++)
            {
                var entry = plan.Entries[index];
                state.Items.Add(new ModuleInstallQueueItem(entry.PackageName, entry.GitUrl, entry.Version));
            }

            WriteQueue(state);
            _store.LastMessage = operation == ModuleInstallOperation.Update
                ? $"Preparing {state.Items.Count} module update(s)."
                : $"Preparing {state.Items.Count} module installation(s).";
            message = _store.LastMessage;
            Tick();
            return true;
        }

        internal void Tick()
        {
            var state = ReadQueue();
            if (state.Items.Count == 0)
            {
                _request = null;
                return;
            }

            if (_request != null)
            {
                if (!_request.IsCompleted)
                {
                    return;
                }

                if (!_request.Succeeded)
                {
                    _store.LastMessage = state.Operation == ModuleInstallOperation.Update
                        ? $"Update failed: {_request.ErrorMessage}"
                        : $"Installation failed: {_request.ErrorMessage}";
                    ClearQueue();
                    _request = null;
                    return;
                }

                _store.LastMessage = state.Operation == ModuleInstallOperation.Update
                    ? $"Updated {state.Items.Count} module(s)."
                    : $"Installed {state.Items.Count} module(s).";
                ClearQueue();
                _request = null;
                return;
            }

            if (state.Operation == ModuleInstallOperation.Update)
            {
                var installedVersions = _environment.GetInstalledPackageVersions();
                state.Items.RemoveAll(item =>
                    installedVersions.TryGetValue(item.PackageName, out var installedVersion)
                    && !ModuleInstallPlanner.IsUpdateRequired(installedVersion, item.TargetVersion));
            }
            else
            {
                var installed = _environment.GetInstalledPackageNames();
                state.Items.RemoveAll(item => installed.Contains(item.PackageName));
            }

            if (state.Items.Count == 0)
            {
                _store.LastMessage = state.Operation == ModuleInstallOperation.Update
                    ? "Every selected module is up to date."
                    : "Every selected module is installed.";
                ClearQueue();
                return;
            }

            WriteQueue(state);
            var urls = new string[state.Items.Count];
            for (var index = 0; index < state.Items.Count; index++)
            {
                urls[index] = state.Items[index].Url;
            }

            _store.LastMessage = state.Operation == ModuleInstallOperation.Update
                ? $"Updating {state.Items.Count} module(s)..."
                : $"Installing {state.Items.Count} module(s)...";
            _request = _client.AddAndRemove(urls);
        }

        private ModuleInstallQueueState ReadQueue()
        {
            if (string.IsNullOrEmpty(_store.QueueJson))
            {
                return new ModuleInstallQueueState();
            }

            try
            {
                return JsonUtility.FromJson<ModuleInstallQueueState>(_store.QueueJson)
                    ?? new ModuleInstallQueueState();
            }
            catch (ArgumentException)
            {
                ClearQueue();
                return new ModuleInstallQueueState();
            }
        }

        private void WriteQueue(ModuleInstallQueueState state)
        {
            _store.QueueJson = JsonUtility.ToJson(state);
        }

        private void ClearQueue()
        {
            _store.QueueJson = string.Empty;
        }

        [Serializable]
        private sealed class ModuleInstallQueueState
        {
            [SerializeField]
            private ModuleInstallOperation operation;

            [SerializeField]
            private List<ModuleInstallQueueItem> items = new List<ModuleInstallQueueItem>();

            internal ModuleInstallOperation Operation
            {
                get => operation;
                set => operation = value;
            }

            internal List<ModuleInstallQueueItem> Items => items;
        }

        [Serializable]
        private sealed class ModuleInstallQueueItem
        {
            [SerializeField]
            private string packageName;

            [SerializeField]
            private string url;

            [SerializeField]
            private string targetVersion;

            internal ModuleInstallQueueItem(string packageName, string url, string targetVersion)
            {
                this.packageName = packageName;
                this.url = url;
                this.targetVersion = targetVersion;
            }

            internal string PackageName => packageName;
            internal string Url => url;
            internal string TargetVersion => targetVersion;
        }
    }
}
