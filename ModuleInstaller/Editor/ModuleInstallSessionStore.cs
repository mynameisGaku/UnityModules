// SPDX-License-Identifier: MIT

using UnityEditor;

namespace ModuleInstaller.Editor
{
    internal sealed class ModuleInstallSessionStore : IModuleInstallStateStore
    {
        private const string QueueKey = "com.studiogaku.module-installer.queue";
        private const string MessageKey = "com.studiogaku.module-installer.message";

        public string QueueJson
        {
            get => SessionState.GetString(QueueKey, string.Empty);
            set => SessionState.SetString(QueueKey, value ?? string.Empty);
        }

        public string LastMessage
        {
            get => SessionState.GetString(MessageKey, string.Empty);
            set => SessionState.SetString(MessageKey, value ?? string.Empty);
        }
    }
}
