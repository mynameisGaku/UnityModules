// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModuleInstaller.Editor
{
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
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (IsBusy)
            {
                message = "Another installation is already in progress.";
                return false;
            }

            if (plan.Issues.Count > 0)
            {
                message = plan.Issues[0].Message;
                return false;
            }

            if (plan.Entries.Count == 0)
            {
                message = plan.InstalledCount > 0
                    ? "Every selected module is already installed."
                    : "No module was selected.";
                return false;
            }

            var state = new ModuleInstallQueueState();
            for (var index = 0; index < plan.Entries.Count; index++)
            {
                var entry = plan.Entries[index];
                state.Items.Add(new ModuleInstallQueueItem(entry.PackageName, entry.GitUrl));
            }

            WriteQueue(state);
            _store.LastMessage = $"Preparing {state.Items.Count} module(s).";
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
                    _store.LastMessage = $"Installation failed: {_request.ErrorMessage}";
                    ClearQueue();
                    _request = null;
                    return;
                }

                _store.LastMessage = $"Installed {state.Items.Count} module(s).";
                ClearQueue();
                _request = null;
                return;
            }

            var installed = _environment.GetInstalledPackageNames();
            state.Items.RemoveAll(item => installed.Contains(item.PackageName));
            if (state.Items.Count == 0)
            {
                _store.LastMessage = "Every selected module is installed.";
                ClearQueue();
                return;
            }

            WriteQueue(state);
            var urls = new string[state.Items.Count];
            for (var index = 0; index < state.Items.Count; index++)
            {
                urls[index] = state.Items[index].Url;
            }

            _store.LastMessage = $"Installing {state.Items.Count} module(s)...";
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
            private List<ModuleInstallQueueItem> items = new List<ModuleInstallQueueItem>();

            internal List<ModuleInstallQueueItem> Items => items;
        }

        [Serializable]
        private sealed class ModuleInstallQueueItem
        {
            [SerializeField]
            private string packageName;

            [SerializeField]
            private string url;

            internal ModuleInstallQueueItem(string packageName, string url)
            {
                this.packageName = packageName;
                this.url = url;
            }

            internal string PackageName => packageName;
            internal string Url => url;
        }
    }
}
