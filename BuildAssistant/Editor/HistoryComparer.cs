using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildAssistant.Editor
{
    /// <summary>保存順が新しい順の履歴から、現在入力と比較できる直近成功結果を探します。</summary>
    internal static class HistoryComparer
    {
        /// <summary>保存順を時刻で並べ直さず、対象設定が一致する最初の成功結果を返します。</summary>
        internal static BuildAssistantHistoryEntry FindLatestComparable(IEnumerable<BuildAssistantHistoryEntry> entries, EnvironmentSnapshot snapshot)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            return entries.FirstOrDefault(entry => entry != null && entry.Status == BuildAssistantHistoryStatus.Succeeded && entry.Error == BuildAssistantError.None && StringComparer.Ordinal.Equals(entry.ProfileStableId, snapshot.Profile.StableId) && entry.Target == snapshot.Target && entry.TargetGroup == snapshot.TargetGroup && StringComparer.Ordinal.Equals(entry.NamedBuildTarget, snapshot.NamedBuildTarget) && entry.Subtarget == snapshot.Subtarget && entry.ScriptingBackend == snapshot.ScriptingBackend && entry.Options == snapshot.Options);
        }

        /// <summary>現在値と以前値の差を、範囲外なら失敗する符号付き整数として返します。</summary>
        internal static long Difference(ulong current, ulong previous)
        {
            checked
            {
                return (long)current - (long)previous;
            }
        }
    }
}
