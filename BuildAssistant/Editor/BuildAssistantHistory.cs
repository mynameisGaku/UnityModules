using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BuildAssistant.Editor
{
    /// <summary>Unityオブジェクトに依存しない、件数制限付きのビルド履歴を新しい順で提供します。</summary>
    public sealed class BuildAssistantHistory
    {
        private readonly ReadOnlyCollection<BuildAssistantHistoryEntry> entries;

        internal BuildAssistantHistory(IEnumerable<BuildAssistantHistoryEntry> entries, bool recoveredFromBackup, string message)
        {
            this.entries = Array.AsReadOnly((entries ?? Enumerable.Empty<BuildAssistantHistoryEntry>()).Take(HistoryStore.MaximumEntryCount).ToArray());
            RecoveredFromBackup = recoveredFromBackup;
            Message = message ?? string.Empty;
        }

        /// <summary>履歴項目を新しい順に並べた、保護された読み取り専用一覧を取得します。</summary>
        public IReadOnlyList<BuildAssistantHistoryEntry> Entries => entries;

        /// <summary>主履歴が存在しないか壊れていたため、有効な予備履歴を使ったかどうかを取得します。</summary>
        public bool RecoveredFromBackup { get; }

        /// <summary>処理を中断しない履歴読込または復旧の診断文を取得します。</summary>
        public string Message { get; }
    }
}
