using System;
using UnityEngine;

namespace DiagnosticsContext
{
    /// <summary>ordinal順へ確定したcontext項目。</summary>
    internal readonly struct DiagnosticsContextItem
    {
        /// <summary>keyとvalueを持つcontext項目を作る。</summary>
        internal DiagnosticsContextItem(string key, string value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>現在状態を識別するkey。</summary>
        internal string Key { get; }

        /// <summary>keyに対応する現在値。</summary>
        internal string Value { get; }
    }

    /// <summary>追加順を保持するbreadcrumb項目。</summary>
    internal readonly struct DiagnosticsBreadcrumbItem
    {
        /// <summary>sequenceと本文を持つbreadcrumbを作る。</summary>
        internal DiagnosticsBreadcrumbItem(long sequence, string message)
        {
            Sequence = sequence;
            Message = message;
        }

        /// <summary>Service内の追加順を示す単調増加値。</summary>
        internal long Sequence { get; }

        /// <summary>利用側が明示追加した短い出来事。</summary>
        internal string Message { get; }
    }

    /// <summary>Unity callbackからcopyした有界log項目。</summary>
    internal readonly struct DiagnosticsLogItem
    {
        /// <summary>sequence、種類、本文、stackを持つlog項目を作る。</summary>
        internal DiagnosticsLogItem(long sequence, LogType type, string message, string stackTrace)
        {
            Sequence = sequence;
            Type = type;
            Message = message;
            StackTrace = stackTrace;
        }

        /// <summary>Service内の取得順を示す単調増加値。</summary>
        internal long Sequence { get; }

        /// <summary>取得対象となったUnity log種別。</summary>
        internal LogType Type { get; }

        /// <summary>Unicode scalar境界で切り詰めたlog本文。</summary>
        internal string Message { get; }

        /// <summary>Unicode scalar境界で切り詰めたstack文字列。</summary>
        internal string StackTrace { get; }
    }

    /// <summary>lock内で確定し、lock外でJSON化できるreport snapshot。</summary>
    internal sealed class DiagnosticsReportSnapshot
    {
        /// <summary>reportの全fieldを確定したsnapshotを作る。</summary>
        internal DiagnosticsReportSnapshot(
            DateTime createdUtc,
            string reason,
            long droppedBreadcrumbCount,
            long droppedLogCount,
            DiagnosticsContextItem[] context,
            DiagnosticsBreadcrumbItem[] breadcrumbs,
            DiagnosticsLogItem[] logs)
        {
            CreatedUtc = createdUtc;
            Reason = reason;
            DroppedBreadcrumbCount = droppedBreadcrumbCount;
            DroppedLogCount = droppedLogCount;
            Context = context;
            Breadcrumbs = breadcrumbs;
            Logs = logs;
        }

        /// <summary>snapshotを作成したUTC時刻。</summary>
        internal DateTime CreatedUtc { get; }

        /// <summary>利用側が指定した書出し理由。</summary>
        internal string Reason { get; }

        /// <summary>容量上限により追い出したbreadcrumb総数。</summary>
        internal long DroppedBreadcrumbCount { get; }

        /// <summary>容量上限により追い出したlog総数。</summary>
        internal long DroppedLogCount { get; }

        /// <summary>keyのordinal順に並べたcontext。</summary>
        internal DiagnosticsContextItem[] Context { get; }

        /// <summary>sequence昇順のbreadcrumb。</summary>
        internal DiagnosticsBreadcrumbItem[] Breadcrumbs { get; }

        /// <summary>sequence昇順のcaptured log。</summary>
        internal DiagnosticsLogItem[] Logs { get; }
    }
}
