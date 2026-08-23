using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BuildAssistant.Editor
{
    /// <summary>Provides the newest-first, bounded Build Assistant history detached from Unity objects.</summary>
    public sealed class BuildAssistantHistory
    {
        private readonly ReadOnlyCollection<BuildAssistantHistoryEntry> entries;

        internal BuildAssistantHistory(IEnumerable<BuildAssistantHistoryEntry> entries, bool recoveredFromBackup, string message)
        {
            this.entries = Array.AsReadOnly((entries ?? Enumerable.Empty<BuildAssistantHistoryEntry>()).Take(HistoryStore.MaximumEntryCount).ToArray());
            RecoveredFromBackup = recoveredFromBackup;
            Message = message ?? string.Empty;
        }

        /// <summary>Gets a defensive read-only copy of entries in newest-first order.</summary>
        public IReadOnlyList<BuildAssistantHistoryEntry> Entries => entries;

        /// <summary>Gets whether a valid backup was used because the primary history was missing or corrupt.</summary>
        public bool RecoveredFromBackup { get; }

        /// <summary>Gets a non-fatal history load or recovery diagnostic.</summary>
        public string Message { get; }
    }
}

