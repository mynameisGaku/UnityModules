using System.Globalization;
using System.Text;
using UnityEngine;

namespace DiagnosticsContext
{
    /// <summary>固定field順とJSON文字列規則でreport snapshotをUTF-16 JSONへ変換する。</summary>
    internal static class DiagnosticsJsonSerializer
    {
        /// <summary>schema version 1の固定順JSONを生成する。</summary>
        /// <param name="snapshot">lock内で確定済みのreport情報。</param>
        /// <returns>BOMを含まないUTF-8へ変換可能なJSON文字列。</returns>
        internal static string Serialize(DiagnosticsReportSnapshot snapshot)
        {
            var builder = new StringBuilder(1024);
            builder.Append('{');
            AppendName(builder, "schemaVersion");
            builder.Append('1');
            builder.Append(',');
            AppendName(builder, "createdUtc");
            AppendString(builder, snapshot.CreatedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendName(builder, "reason");
            AppendString(builder, snapshot.Reason);
            builder.Append(',');
            AppendName(builder, "droppedBreadcrumbCount");
            builder.Append(snapshot.DroppedBreadcrumbCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendName(builder, "droppedLogCount");
            builder.Append(snapshot.DroppedLogCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            AppendName(builder, "context");
            AppendContext(builder, snapshot.Context);
            builder.Append(',');
            AppendName(builder, "breadcrumbs");
            AppendBreadcrumbs(builder, snapshot.Breadcrumbs);
            builder.Append(',');
            AppendName(builder, "logs");
            AppendLogs(builder, snapshot.Logs);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>context arrayをkey、valueの固定順で追加する。</summary>
        private static void AppendContext(StringBuilder builder, DiagnosticsContextItem[] items)
        {
            builder.Append('[');
            for (var index = 0; index < items.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append('{');
                AppendName(builder, "key");
                AppendString(builder, items[index].Key);
                builder.Append(',');
                AppendName(builder, "value");
                AppendString(builder, items[index].Value);
                builder.Append('}');
            }

            builder.Append(']');
        }

        /// <summary>breadcrumb arrayをsequence、messageの固定順で追加する。</summary>
        private static void AppendBreadcrumbs(StringBuilder builder, DiagnosticsBreadcrumbItem[] items)
        {
            builder.Append('[');
            for (var index = 0; index < items.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append('{');
                AppendName(builder, "sequence");
                builder.Append(items[index].Sequence.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                AppendName(builder, "message");
                AppendString(builder, items[index].Message);
                builder.Append('}');
            }

            builder.Append(']');
        }

        /// <summary>log arrayをsequence、type、message、stackTraceの固定順で追加する。</summary>
        private static void AppendLogs(StringBuilder builder, DiagnosticsLogItem[] items)
        {
            builder.Append('[');
            for (var index = 0; index < items.Length; index++)
            {
                if (index > 0) builder.Append(',');
                builder.Append('{');
                AppendName(builder, "sequence");
                builder.Append(items[index].Sequence.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                AppendName(builder, "type");
                AppendString(builder, GetLogTypeName(items[index].Type));
                builder.Append(',');
                AppendName(builder, "message");
                AppendString(builder, items[index].Message);
                builder.Append(',');
                AppendName(builder, "stackTrace");
                AppendString(builder, items[index].StackTrace);
                builder.Append('}');
            }

            builder.Append(']');
        }

        /// <summary>取得対象log種別のculture非依存名を返す。</summary>
        private static string GetLogTypeName(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return "Warning";
                case LogType.Error:
                    return "Error";
                case LogType.Assert:
                    return "Assert";
                case LogType.Exception:
                    return "Exception";
                default:
                    return "Unknown";
            }
        }

        /// <summary>JSON property名と区切りcolonを追加する。</summary>
        private static void AppendName(StringBuilder builder, string name)
        {
            AppendString(builder, name);
            builder.Append(':');
        }

        /// <summary>JSON規則で制御文字とquoteをescapeした文字列を追加する。</summary>
        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                switch (current)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (current < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)current).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(current);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
