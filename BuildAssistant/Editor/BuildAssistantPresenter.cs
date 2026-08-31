using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>計画作成、実行確認、ビルド、履歴、書き出しの画面状態を管理します。</summary>
    internal sealed class BuildAssistantPresenter
    {
        private readonly Func<string, BuildAssistantPlan> preview;
        private readonly Func<BuildAssistantPlan, BuildAssistantBuildResult> build;
        private readonly Func<BuildAssistantHistory> loadHistory;
        private readonly Func<BuildAssistantHistoryEntry, string, BuildAssistantError> exportJson;
        private int selectedHistoryIndex = -1;

        internal BuildAssistantPresenter(Func<string, BuildAssistantPlan> preview = null, Func<BuildAssistantPlan, BuildAssistantBuildResult> build = null, Func<BuildAssistantHistory> loadHistory = null, Func<BuildAssistantHistoryEntry, string, BuildAssistantError> exportJson = null)
        {
            this.preview = preview ?? BuildAssistantService.Preview;
            this.build = build ?? BuildAssistantService.Build;
            this.loadHistory = loadHistory ?? BuildAssistantService.LoadHistory;
            this.exportJson = exportJson ?? BuildAssistantService.ExportJson;
            History = EmptyHistory();
        }

        internal string OutputRoot { get; private set; } = string.Empty;
        internal BuildAssistantPlan Plan { get; private set; }
        internal BuildAssistantBuildResult Result { get; private set; }
        internal BuildAssistantHistory History { get; private set; }
        internal bool ConfirmationAccepted { get; private set; }
        internal string Message { get; private set; } = string.Empty;
        internal string ExportMessage { get; private set; } = string.Empty;
        internal BuildAssistantError LastExportError { get; private set; } = BuildAssistantError.None;
        internal bool CanBuild => Plan != null && Plan.IsReady && ConfirmationAccepted;
        internal int SelectedHistoryIndex => selectedHistoryIndex;
        internal BuildAssistantHistoryEntry SelectedHistoryEntry => selectedHistoryIndex >= 0 && selectedHistoryIndex < History.Entries.Count ? History.Entries[selectedHistoryIndex] : null;
        internal BuildAssistantHistoryEntry ExportEntry => SelectedHistoryEntry ?? Result?.Entry;

        /// <summary>出力先を更新し、別の値を記録した計画と確認状態を破棄します。</summary>
        internal void SetOutputRoot(string value)
        {
            var normalized = value ?? string.Empty;
            if (StringComparer.Ordinal.Equals(OutputRoot, normalized))
                return;

            OutputRoot = normalized;
            InvalidatePlan();
        }

        /// <summary>現在の出力先で新しい計画を作成し、あらためて実行確認を求めます。</summary>
        internal void Preview()
        {
            ConfirmationAccepted = false;
            Result = null;
            if (selectedHistoryIndex < 0 && History.Entries.Count > 0)
                selectedHistoryIndex = 0;
            ExportMessage = string.Empty;
            LastExportError = BuildAssistantError.None;
            try
            {
                Plan = preview(OutputRoot);
                if (Plan == null)
                {
                    Message = "ビルド計画を作成できませんでした。出力先とビルド設定を確認してください。";
                    return;
                }

                Message = Plan.IsReady
                    ? "ビルド計画を作成しました。取得した入力を確認し、実行確認を有効にしてください。"
                    : FormatError(Plan.Error, Plan.Message);
            }
            catch (Exception)
            {
                Plan = null;
                Message = "ビルド計画の作成中に予期しない問題が発生しました。出力先とビルド設定を確認してください。";
            }
        }

        /// <summary>実行可能な計画を表示中の場合だけ、実行確認を受け付けます。</summary>
        internal void SetConfirmation(bool value)
        {
            ConfirmationAccepted = value && Plan != null && Plan.IsReady;
        }

        /// <summary>確認済み計画を1回だけ実行し、Unityオブジェクトに依存しない履歴を再読み込みします。</summary>
        internal void Build()
        {
            if (!CanBuild)
            {
                Message = "準備済みのビルド計画を作成し、内容を確認してから実行してください。";
                return;
            }

            var consumedPlan = Plan;
            Plan = null;
            ConfirmationAccepted = false;
            try
            {
                Result = build(consumedPlan);
                Message = FormatResult(Result);
            }
            catch (Exception)
            {
                Result = new BuildAssistantBuildResult(false, false, BuildAssistantError.BuildInvocationFailed, "ビルド実行中に予期しない問題が発生しました。", null);
                Message = FormatResult(Result);
            }

            RefreshHistory(Result?.Entry?.RunId);
            ReconcileCurrentResult();
        }

        /// <summary>現在の選択を可能な限り維持しながら、件数制限付き履歴を再読み込みします。</summary>
        internal void RefreshHistory()
        {
            RefreshHistory(SelectedHistoryEntry?.RunId ?? Result?.Entry?.RunId);
            ReconcileCurrentResult();
        }

        /// <summary>新しい順で表示した番号により、履歴項目を選択します。</summary>
        internal void SetHistoryIndex(int value)
        {
            selectedHistoryIndex = value >= 0 && value < History.Entries.Count ? value : -1;
            ExportMessage = string.Empty;
            LastExportError = BuildAssistantError.None;
        }

        /// <summary>選択した履歴項目を新しいJSONファイルへ書き出します。</summary>
        internal void Export(string absolutePath)
        {
            var entry = ExportEntry;
            if (entry == null)
            {
                LastExportError = BuildAssistantError.InvalidOutputRoot;
                ExportMessage = "書き出す実行結果または履歴を選択してください。";
                return;
            }

            try
            {
                LastExportError = exportJson(entry, absolutePath);
                ExportMessage = LastExportError == BuildAssistantError.None
                    ? "新しいJSONファイルへ書き出しました。"
                    : "JSONを書き出せませんでした。" + FormatError(LastExportError, string.Empty) + "既存ファイルは上書きしません。";
            }
            catch (Exception)
            {
                LastExportError = BuildAssistantError.HistoryWriteFailed;
                ExportMessage = "JSONを書き出せませんでした。出力先とアクセス権を確認してください。既存ファイルは上書きしません。";
            }
        }

        /// <summary>エディター表示用に、バイト数を一定の2進単位で整形します。</summary>
        internal static string FormatBytes(ulong bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            var value = (double)bytes;
            var unit = 0;
            while (value >= 1024d && unit < units.Length - 1)
            {
                value /= 1024d;
                unit++;
            }

            return unit == 0
                ? bytes.ToString(CultureInfo.InvariantCulture) + " " + units[unit]
                : value.ToString("0.00", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        /// <summary>符号付き容量差を、最小値でも桁あふれさせず整形します。</summary>
        internal static string FormatDelta(long bytes)
        {
            var magnitude = bytes < 0 ? (ulong)(-(bytes + 1)) + 1UL : (ulong)bytes;
            return (bytes > 0 ? "+" : bytes < 0 ? "-" : string.Empty) + FormatBytes(magnitude);
        }

        /// <summary>新しい順の履歴選択肢に使う短い表示を作ります。</summary>
        internal static string FormatHistoryLabel(BuildAssistantHistoryEntry entry)
        {
            if (entry == null)
                return "履歴なし";
            return entry.CompletedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "  " + FormatHistoryStatus(entry.Status) + "  " + entry.RunId;
        }

        /// <summary>ビルドプロファイルの種類を日本語で表示します。</summary>
        internal static string FormatProfileKind(BuildAssistantProfileKind kind)
        {
            switch (kind)
            {
                case BuildAssistantProfileKind.Platform:
                    return "プラットフォーム設定";
                case BuildAssistantProfileKind.Custom:
                    return "独自設定";
                default:
                    return "確認できない種類（数値 " + ((int)kind).ToString(CultureInfo.InvariantCulture) + "）";
            }
        }

        /// <summary>対象機種を日本語で表示します。</summary>
        internal static string FormatTarget(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    return "Windows 64ビット";
                case BuildTarget.StandaloneWindows:
                    return "Windows 32ビット（未対応）";
                case BuildTarget.StandaloneOSX:
                    return "macOS";
                case BuildTarget.StandaloneLinux64:
                    return "Linux 64ビット";
                default:
                    return "確認できない対象機種（数値 " + ((int)target).ToString(CultureInfo.InvariantCulture) + "）";
            }
        }

        /// <summary>コード生成方式を日本語で表示します。</summary>
        internal static string FormatScriptingBackend(ScriptingImplementation backend)
        {
            switch (backend)
            {
                case ScriptingImplementation.Mono2x:
                    return "Mono";
                case ScriptingImplementation.IL2CPP:
                    return "IL2CPP";
                default:
                    return "確認できない方式（数値 " + ((int)backend).ToString(CultureInfo.InvariantCulture) + "）";
            }
        }

        /// <summary>ビルド選択肢を日本語で列挙します。</summary>
        internal static string FormatBuildOptions(BuildOptions options)
        {
            if (options == BuildOptions.None)
                return "なし";

            var labels = new List<string>();
            var remaining = options;
            AddBuildOption(labels, ref remaining, BuildOptions.DetailedBuildReport, "詳細報告");
            AddBuildOption(labels, ref remaining, BuildOptions.Development, "開発用");
            AddBuildOption(labels, ref remaining, BuildOptions.ConnectWithProfiler, "性能測定へ接続");
            AddBuildOption(labels, ref remaining, BuildOptions.AllowDebugging, "デバッグ許可");
            AddBuildOption(labels, ref remaining, BuildOptions.WaitForPlayerConnection, "プレイヤー接続待ち");
            AddBuildOption(labels, ref remaining, BuildOptions.EnableCodeCoverage, "網羅率計測");
            AddBuildOption(labels, ref remaining, BuildOptions.EnableDeepProfilingSupport, "詳細な性能測定");
            AddBuildOption(labels, ref remaining, BuildOptions.CompressWithLz4, "LZ4圧縮");
            AddBuildOption(labels, ref remaining, BuildOptions.CompressWithLz4HC, "LZ4高圧縮");
            AddBuildOption(labels, ref remaining, BuildOptions.SymlinkSources, "ソースをシンボリックリンクで参照");
            if (remaining != BuildOptions.None)
                labels.Add("その他（数値 " + ((int)remaining).ToString(CultureInfo.InvariantCulture) + "）");
            return string.Join("、", labels);
        }

        /// <summary>履歴の終了状態を日本語で表示します。</summary>
        internal static string FormatHistoryStatus(BuildAssistantHistoryStatus status)
        {
            switch (status)
            {
                case BuildAssistantHistoryStatus.Succeeded:
                    return "成功";
                case BuildAssistantHistoryStatus.Failed:
                    return "失敗";
                case BuildAssistantHistoryStatus.Interrupted:
                    return "中断";
                default:
                    return "確認できない状態（数値 " + ((int)status).ToString(CultureInfo.InvariantCulture) + "）";
            }
        }

        /// <summary>履歴の診断を、旧版の英語原文を画面へ出さずに日本語で表示します。</summary>
        internal static string FormatHistoryMessage(BuildAssistantHistoryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Message))
                return string.Empty;
            if (entry.Error == BuildAssistantError.None)
                return "ビルドは完了しましたが、追加の注意事項があります。書き出したJSONで記録を確認してください。";
            return FormatError(entry.Error, entry.Message);
        }

        /// <summary>履歴全体の注意事項を、保存済みの英語原文を出さずに日本語で表示します。</summary>
        internal static string FormatHistoryNotice(BuildAssistantHistory history)
        {
            if (history == null)
                return "履歴を読み込めませんでした。Libraryフォルダーへのアクセス権と空き容量を確認してください。";
            if (history.RecoveredFromBackup)
                return "履歴の主ファイルを読み込めなかったため、予備ファイルから復旧しました。Libraryフォルダーと空き容量を確認してください。";
            return string.IsNullOrEmpty(history.Message)
                ? string.Empty
                : "履歴の読み込みまたは実行状態の後片付けに注意事項があります。必要な結果はJSONへ書き出して保管してください。";
        }

        /// <summary>履歴へ復旧した項目が、現在の終了結果と同一か判定します。</summary>
        internal static bool IsSameTerminalResult(BuildAssistantHistoryEntry historyEntry, BuildAssistantHistoryEntry resultEntry)
        {
            if (historyEntry == null || resultEntry == null)
                return false;
            return StringComparer.Ordinal.Equals(historyEntry.RunId, resultEntry.RunId)
                && historyEntry.CreatedAtUtc == resultEntry.CreatedAtUtc
                && historyEntry.Status == resultEntry.Status
                && historyEntry.Error == resultEntry.Error
                && historyEntry.StartedAtUtc == resultEntry.StartedAtUtc
                && historyEntry.CompletedAtUtc == resultEntry.CompletedAtUtc
                && StringComparer.Ordinal.Equals(historyEntry.Message, resultEntry.Message)
                && StringComparer.Ordinal.Equals(historyEntry.OutputRoot, resultEntry.OutputRoot)
                && StringComparer.Ordinal.Equals(historyEntry.RunDirectory, resultEntry.RunDirectory)
                && StringComparer.Ordinal.Equals(historyEntry.ArtifactPath, resultEntry.ArtifactPath)
                && historyEntry.ProfileKind == resultEntry.ProfileKind
                && StringComparer.Ordinal.Equals(historyEntry.ProfileGuid, resultEntry.ProfileGuid)
                && StringComparer.Ordinal.Equals(historyEntry.ProfileName, resultEntry.ProfileName)
                && StringComparer.Ordinal.Equals(historyEntry.ProfilePath, resultEntry.ProfilePath)
                && StringComparer.Ordinal.Equals(historyEntry.ProfileDependencyHash, resultEntry.ProfileDependencyHash)
                && StringComparer.Ordinal.Equals(historyEntry.ProfileStableId, resultEntry.ProfileStableId)
                && historyEntry.Target == resultEntry.Target
                && historyEntry.TargetGroup == resultEntry.TargetGroup
                && StringComparer.Ordinal.Equals(historyEntry.NamedBuildTarget, resultEntry.NamedBuildTarget)
                && historyEntry.Subtarget == resultEntry.Subtarget
                && historyEntry.ScriptingBackend == resultEntry.ScriptingBackend
                && historyEntry.Options == resultEntry.Options
                && ListsEqual(historyEntry.EffectiveDefines, resultEntry.EffectiveDefines, (first, second) => StringComparer.Ordinal.Equals(first, second))
                && ListsEqual(historyEntry.Scenes, resultEntry.Scenes, AreSameScenes)
                && historyEntry.TotalErrors == resultEntry.TotalErrors
                && historyEntry.TotalWarnings == resultEntry.TotalWarnings
                && historyEntry.TotalOutputBytes == resultEntry.TotalOutputBytes
                && historyEntry.PackedContentBytes == resultEntry.PackedContentBytes
                && historyEntry.PackedOverheadBytes == resultEntry.PackedOverheadBytes
                && ListsEqual(historyEntry.Assets, resultEntry.Assets, AreSameAssets)
                && ListsEqual(historyEntry.Types, resultEntry.Types, AreSameTypes)
                && StringComparer.Ordinal.Equals(historyEntry.PreviousRunId, resultEntry.PreviousRunId)
                && historyEntry.TotalOutputDeltaBytes == resultEntry.TotalOutputDeltaBytes
                && historyEntry.PackedContentDeltaBytes == resultEntry.PackedContentDeltaBytes;
        }

        /// <summary>順序を含めて、2つの読み取り専用一覧が同じ内容かを確認します。</summary>
        private static bool ListsEqual<T>(IReadOnlyList<T> first, IReadOnlyList<T> second, Func<T, T, bool> equals)
        {
            if (first == null || second == null || equals == null || first.Count != second.Count)
                return false;
            for (var index = 0; index < first.Count; index++)
            {
                if (!equals(first[index], second[index]))
                    return false;
            }
            return true;
        }

        /// <summary>2つのシーン記録が同じ入力を表すかを確認します。</summary>
        private static bool AreSameScenes(BuildAssistantScene first, BuildAssistantScene second)
        {
            return first != null && second != null
                && first.Order == second.Order
                && StringComparer.Ordinal.Equals(first.Guid, second.Guid)
                && StringComparer.Ordinal.Equals(first.AssetPath, second.AssetPath)
                && first.Enabled == second.Enabled
                && StringComparer.Ordinal.Equals(first.DependencyHash, second.DependencyHash);
        }

        /// <summary>2つの素材容量記録が同じ集計値を表すかを確認します。</summary>
        private static bool AreSameAssets(BuildAssistantAssetSize first, BuildAssistantAssetSize second)
        {
            return first != null && second != null
                && StringComparer.Ordinal.Equals(first.AssetPath, second.AssetPath)
                && first.PackedBytes == second.PackedBytes
                && first.OccurrenceCount == second.OccurrenceCount;
        }

        /// <summary>2つの型容量記録が同じ集計値を表すかを確認します。</summary>
        private static bool AreSameTypes(BuildAssistantTypeSize first, BuildAssistantTypeSize second)
        {
            return first != null && second != null
                && StringComparer.Ordinal.Equals(first.TypeName, second.TypeName)
                && first.PackedBytes == second.PackedBytes
                && first.OccurrenceCount == second.OccurrenceCount
                && first.AssetCount == second.AssetCount;
        }

        private void RefreshHistory(string preferredRunId)
        {
            try
            {
                History = loadHistory() ?? EmptyHistory();
            }
            catch (Exception)
            {
                History = new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, "履歴を読み込めませんでした。Libraryフォルダーへのアクセス権と空き容量を確認してください。");
            }

            selectedHistoryIndex = string.IsNullOrEmpty(preferredRunId)
                ? History.Entries.Count > 0 ? 0 : -1
                : History.Entries.Select((entry, index) => new { entry, index }).Where(item => StringComparer.Ordinal.Equals(item.entry.RunId, preferredRunId)).Select(item => item.index).DefaultIfEmpty(-1).First();
        }

        /// <summary>未保存の現在結果を、同じ実行識別子を持つ別の中断履歴へ切り替えず、同一の終了結果だけと照合します。</summary>
        private void ReconcileCurrentResult()
        {
            if (Result?.Entry == null || Result.HistoryPersisted)
                return;

            var recoveredEntry = History.Entries.FirstOrDefault(entry => IsSameTerminalResult(entry, Result.Entry));
            if (recoveredEntry != null)
            {
                Result = new BuildAssistantBuildResult(Result.BuildSucceeded, true, Result.Entry.Error, string.Empty, Result.Entry);
                Message = FormatResult(Result);
                return;
            }

            if (selectedHistoryIndex >= 0 && selectedHistoryIndex < History.Entries.Count && StringComparer.Ordinal.Equals(History.Entries[selectedHistoryIndex].RunId, Result.Entry.RunId))
                selectedHistoryIndex = -1;
        }

        private void InvalidatePlan()
        {
            Plan = null;
            Result = null;
            ConfirmationAccepted = false;
            if (selectedHistoryIndex < 0 && History.Entries.Count > 0)
                selectedHistoryIndex = 0;
            Message = string.Empty;
            ExportMessage = string.Empty;
            LastExportError = BuildAssistantError.None;
        }

        private static string FormatResult(BuildAssistantBuildResult result)
        {
            if (result == null)
                return "ビルド結果を取得できませんでした。Unityのコンソールを確認してください。";
            if (result.BuildSucceeded && result.HistoryPersisted)
            {
                var summary = "ビルドが完了し、履歴を保存しました。";
                if (result.Error == BuildAssistantError.None && string.IsNullOrEmpty(result.Message))
                    return summary;
                var detail = result.Error == BuildAssistantError.None
                    ? "後処理に注意事項があります。書き出したJSONで記録を確認してください。"
                    : FormatError(result.Error, result.Message);
                return summary + detail;
            }
            if (result.BuildSucceeded)
                return "ビルドは完了しましたが、履歴を保存できませんでした。" + FormatError(result.Error, result.Message);
            return "ビルドに失敗しました。" + FormatError(result.Error, result.Message);
        }

        internal static string FormatError(BuildAssistantError error, string message)
        {
            switch (error)
            {
                case BuildAssistantError.None:
                    return string.IsNullOrEmpty(message) ? "問題はありません。" : "追加の注意事項があります。";
                case BuildAssistantError.InvalidOutputRoot:
                    return "出力先が未設定、相対パス、ファイル、または許可範囲外の階層です。ローカルドライブの絶対フォルダーを選び直してください。";
                case BuildAssistantError.UnsafeOutputPath:
                    return "出力先がUnity管理フォルダー、ネットワーク、再解析点、または安全な範囲外です。別のローカルフォルダーを選んでください。";
                case BuildAssistantError.UnsupportedBuildTarget:
                    return "現在の対象機種またはビルド設定には対応していません。Windows、macOS、Linuxの通常プレイヤー設定を確認してください。";
                case BuildAssistantError.BuildTargetMismatch:
                    return "独自のビルドプロファイルと、エディターで選択中の対象機種または種別が一致しません。Unityのビルドプロファイル画面で同じ対象へ切り替え、コンパイル完了後に計画を作り直してください。";
                case BuildAssistantError.EditorBusy:
                    return "Unityがコンパイル、更新、または再生状態の切り替え中です。完了してから新しい計画を作成してください。";
                case BuildAssistantError.NoEnabledScenes:
                    return "ビルド対象の有効なシーンがありません。ビルドプロファイルのシーン一覧を確認してください。";
                case BuildAssistantError.StalePlan:
                    return "計画作成後にビルド入力が変わりました。新しい計画を作成し直してください。";
                case BuildAssistantError.BuildAlreadyRunning:
                    return "別のプレイヤービルドを実行中です。完了してからやり直してください。";
                case BuildAssistantError.OutputAlreadyExists:
                    return "今回の実行フォルダー、予約、または書き出し先が既に存在します。新しい出力先でやり直してください。";
                case BuildAssistantError.OutputReservationFailed:
                    return "出力先を安全に予約できませんでした。アクセス権、空き容量、出力先の変更有無を確認してください。";
                case BuildAssistantError.BuildInvocationFailed:
                    return "Unityがプレイヤービルドを完了できませんでした。コンソールとビルド設定を確認してください。";
                case BuildAssistantError.BuildReportUnavailable:
                    return "Unityのビルド報告を取得できませんでした。コンソールを確認してください。";
                case BuildAssistantError.ReportReadFailed:
                    return "ビルド報告の集計情報を読み取れませんでした。ビルド成果物と書き出したJSONを確認してください。";
                case BuildAssistantError.HistoryWriteFailed:
                    return "履歴またはJSONを保存できませんでした。Libraryフォルダーや出力先のアクセス権と空き容量を確認してください。";
                default:
                    return "確認できない問題が発生しました。Unityのコンソールを確認してください。";
            }
        }

        private static void AddBuildOption(List<string> labels, ref BuildOptions remaining, BuildOptions option, string label)
        {
            if ((remaining & option) == BuildOptions.None)
                return;
            labels.Add(label);
            remaining &= ~option;
        }

        private static BuildAssistantHistory EmptyHistory()
        {
            return new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, string.Empty);
        }
    }
}
