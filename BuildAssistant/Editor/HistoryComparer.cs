using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildAssistant.Editor
{
    internal static class HistoryComparer
    {
        internal static BuildAssistantHistoryEntry FindLatestComparable(IEnumerable<BuildAssistantHistoryEntry> entries, EnvironmentSnapshot snapshot)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            return entries.Where(entry => entry != null && entry.Status == BuildAssistantHistoryStatus.Succeeded && entry.Error == BuildAssistantError.None && StringComparer.Ordinal.Equals(entry.ProfileStableId, snapshot.Profile.StableId) && entry.Target == snapshot.Target && entry.Subtarget == snapshot.Subtarget && entry.ScriptingBackend == snapshot.ScriptingBackend && entry.Options == snapshot.Options).OrderByDescending(entry => entry.CompletedAtUtc).ThenByDescending(entry => entry.RunId, StringComparer.Ordinal).FirstOrDefault();
        }

        internal static long Difference(ulong current, ulong previous)
        {
            checked
            {
                return (long)current - (long)previous;
            }
        }
    }
}
