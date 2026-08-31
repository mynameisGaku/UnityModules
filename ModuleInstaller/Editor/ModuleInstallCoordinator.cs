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
        private const string InvalidStoredQueueMessage =
            "保存されていた導入処理を確認できないため中止しました。対象を選び直してください。";

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

        internal bool IsBusy => !string.IsNullOrEmpty(_store.QueueJson);
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
                message = "別のパッケージ操作を処理しています。完了後にもう一度実行してください。";
                _store.LastMessage = message;
                return false;
            }

            if (plan.Issues.Count > 0)
            {
                message = plan.Issues[0].Message;
                _store.LastMessage = message;
                return false;
            }

            if (plan.Entries.Count == 0)
            {
                message = operation == ModuleInstallOperation.Update
                    ? "導入済みの一覧掲載モジュールはすべて固定版と一致しています。"
                    : plan.InstalledCount > 0
                        ? "選択したモジュールはすべて導入済みです。"
                        : "モジュールが選択されていません。";
                _store.LastMessage = message;
                return false;
            }

            if (!ValidateEntries(plan.Entries, operation, out message))
            {
                _store.LastMessage = message;
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
                ? $"{state.Items.Count}件のモジュール更新を準備しています。"
                : $"{state.Items.Count}件のモジュール導入を準備しています。";
            Tick();
            message = _store.LastMessage;
            return IsBusy;
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
                    _store.LastMessage = BuildPackageFailureMessage(state.Operation, _request.ErrorMessage, false);
                    ClearQueue();
                    _request = null;
                    return;
                }

                _store.LastMessage = state.Operation == ModuleInstallOperation.Update
                    ? $"{state.Items.Count}件のモジュールを更新しました。"
                    : $"{state.Items.Count}件のモジュールを導入しました。";
                ClearQueue();
                _request = null;
                return;
            }

            if (!TryCanonicalizePendingState(state, out var queueIssueMessage))
            {
                _store.LastMessage = queueIssueMessage;
                ClearQueue();
                return;
            }

            if (state.Operation == ModuleInstallOperation.Update)
            {
                var installedVersions = _environment.GetInstalledPackageVersions();
                if (!ValidatePendingState(state, installedVersions, out var issueMessage))
                {
                    _store.LastMessage = issueMessage;
                    ClearQueue();
                    return;
                }

                state.Items.RemoveAll(item =>
                    installedVersions.TryGetValue(item.PackageName, out var installedVersion)
                    && !ModuleInstallPlanner.IsUpdateRequired(installedVersion, item.TargetVersion));
            }
            else
            {
                var installed = _environment.GetInstalledPackageNames();
                if (!ValidatePendingState(state, installed, out var issueMessage))
                {
                    _store.LastMessage = issueMessage;
                    ClearQueue();
                    return;
                }

                state.Items.RemoveAll(item => installed.Contains(item.PackageName));
            }

            if (state.Items.Count == 0)
            {
                _store.LastMessage = state.Operation == ModuleInstallOperation.Update
                    ? "選択したモジュールはすべて固定版と一致しています。"
                    : "選択したモジュールはすべて導入済みです。";
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
                ? $"{state.Items.Count}件のモジュールを更新しています…"
                : $"{state.Items.Count}件のモジュールを導入しています…";
            try
            {
                _request = _client.AddAndRemove(urls);
            }
            catch (Exception exception)
            {
                _store.LastMessage = BuildPackageFailureMessage(state.Operation, exception.Message, true);
                ClearQueue();
                _request = null;
            }
        }

        private static bool TryCanonicalizePendingState(
            ModuleInstallQueueState state,
            out string issueMessage)
        {
            if (state.Operation != ModuleInstallOperation.Install
                && state.Operation != ModuleInstallOperation.Update)
            {
                issueMessage = "保存されていたパッケージ操作の種類を確認できないため、処理を中止しました。対象を選び直してください。";
                return false;
            }

            var canonicalItems = new List<ModuleInstallQueueItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < state.Items.Count; index++)
            {
                var item = state.Items[index];
                if (item == null
                    || string.IsNullOrEmpty(item.PackageName)
                    || !ModuleCatalog.TryFindEntry(item.PackageName, out var entry))
                {
                    issueMessage = "保存されていた導入対象を現在の固定一覧で確認できないため、処理を中止しました。対象を選び直してください。";
                    return false;
                }

                if (!seen.Add(entry.PackageName))
                {
                    continue;
                }

                canonicalItems.Add(new ModuleInstallQueueItem(entry.PackageName, entry.GitUrl, entry.Version));
            }

            state.Items.Clear();
            state.Items.AddRange(canonicalItems);
            issueMessage = string.Empty;
            return true;
        }

        private static string BuildPackageFailureMessage(
            ModuleInstallOperation operation,
            string technicalDetails,
            bool failedToStart)
        {
            var operationName = operation == ModuleInstallOperation.Update ? "更新" : "導入";
            var failure = failedToStart ? "を開始できませんでした。" : "を完了できませんでした。";
            var details = string.IsNullOrWhiteSpace(technicalDetails) ? "詳細なし" : technicalDetails;
            return $"パッケージの{operationName}{failure}通信、権限、Git、競合状態を確認してください。技術詳細（Unity原文）：{details}";
        }

        private bool ValidateEntries(
            IReadOnlyList<ModuleCatalogEntry> entries,
            ModuleInstallOperation operation,
            out string issueMessage)
        {
            var packageNames = new string[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                packageNames[index] = entries[index].PackageName;
            }

            ModuleInstallPlan plan;
            if (operation == ModuleInstallOperation.Update)
            {
                var installedVersions = _environment.GetInstalledPackageVersions();
                if (!TryValidateUpdateTargetsPresent(packageNames, installedVersions, out issueMessage))
                {
                    return false;
                }

                plan = ModuleInstallPlanner.BuildUpdates(
                    packageNames,
                    installedVersions,
                    _environment.GetAssetModuleFolders());
            }
            else
            {
                plan = ModuleInstallPlanner.Build(
                    packageNames,
                    _environment.GetInstalledPackageNames(),
                    _environment.GetAssetModuleFolders());
            }

            return TryAcceptValidationPlan(plan, out issueMessage);
        }

        private bool ValidatePendingState(
            ModuleInstallQueueState state,
            IReadOnlyDictionary<string, string> installedVersions,
            out string issueMessage)
        {
            var packageNames = GetPendingPackageNames(state);
            if (!TryValidateUpdateTargetsPresent(packageNames, installedVersions, out issueMessage))
            {
                return false;
            }

            var plan = ModuleInstallPlanner.BuildUpdates(
                packageNames,
                installedVersions,
                _environment.GetAssetModuleFolders());
            return TryAcceptValidationPlan(plan, out issueMessage);
        }

        private static bool TryValidateUpdateTargetsPresent(
            IReadOnlyList<string> packageNames,
            IReadOnlyDictionary<string, string> installedVersions,
            out string issueMessage)
        {
            for (var index = 0; index < packageNames.Count; index++)
            {
                var packageName = packageNames[index];
                if (!installedVersions.ContainsKey(packageName))
                {
                    issueMessage = $"{packageName} が導入済みではなくなったため、更新を中止しました。更新対象を再確認してから、もう一度実行してください。";
                    return false;
                }
            }

            issueMessage = string.Empty;
            return true;
        }

        private bool ValidatePendingState(
            ModuleInstallQueueState state,
            ISet<string> installedPackageNames,
            out string issueMessage)
        {
            var packageNames = GetPendingPackageNames(state);
            var plan = ModuleInstallPlanner.Build(
                packageNames,
                installedPackageNames,
                _environment.GetAssetModuleFolders());
            return TryAcceptValidationPlan(plan, out issueMessage);
        }

        private static string[] GetPendingPackageNames(ModuleInstallQueueState state)
        {
            var packageNames = new string[state.Items.Count];
            for (var index = 0; index < state.Items.Count; index++)
            {
                packageNames[index] = state.Items[index].PackageName;
            }

            return packageNames;
        }

        private static bool TryAcceptValidationPlan(ModuleInstallPlan plan, out string issueMessage)
        {
            if (plan.Issues.Count == 0)
            {
                issueMessage = string.Empty;
                return true;
            }

            issueMessage = plan.Issues[0].Message;
            return false;
        }

        private ModuleInstallQueueState ReadQueue()
        {
            var queueJson = _store.QueueJson;
            if (string.IsNullOrEmpty(queueJson))
            {
                return new ModuleInstallQueueState();
            }

            try
            {
                var state = JsonUtility.FromJson<ModuleInstallQueueState>(queueJson);
                if (state == null || !state.HasItemList)
                {
                    _store.LastMessage = InvalidStoredQueueMessage;
                    ClearQueue();
                    return new ModuleInstallQueueState();
                }

                if (state.Items.Count == 0)
                {
                    _store.LastMessage = InvalidStoredQueueMessage;
                    ClearQueue();
                    return new ModuleInstallQueueState();
                }

                return state;
            }
            catch (ArgumentException)
            {
                _store.LastMessage = InvalidStoredQueueMessage;
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

            internal bool HasItemList => items != null;
            internal List<ModuleInstallQueueItem> Items => items ?? (items = new List<ModuleInstallQueueItem>());
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
