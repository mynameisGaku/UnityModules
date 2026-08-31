using System;
using System.IO;
using System.Linq;
using System.Security;
using UnityEditor;

namespace BuildAssistant.Editor
{
    /// <summary>安全なデスクトップ単体実行形式のビルドを1件ずつ計画、実行、記録し、明示的に書き出します。</summary>
    public static class BuildAssistantService
    {
        /// <summary>実際に使われるプロファイル、設定、シーン、取り込み済みの内容の版、パッケージ目録、ストリーミング用素材を変更せずに記録します。</summary>
        /// <param name="absoluteOutputRoot">既存フォルダーの絶対パス、または既存フォルダー直下に1階層だけ未作成部分がある絶対パス。</param>
        /// <returns>変更不能なビルド計画。実行できない場合は定義済みの失敗理由を保持します。</returns>
        public static BuildAssistantPlan Preview(string absoluteOutputRoot)
        {
            EnvironmentSnapshot environment = null;
            try
            {
                environment = new UnityBuildEnvironment().Capture();
                var safeOutput = new SafeBuildOutput(ProjectRoot);
                var location = safeOutput.Inspect(absoluteOutputRoot);
                if (!location.IsValid)
                    return PlanFactory.CreateFailure(environment, location.Error, location.Message, location.NormalizedPath, location.Mode);

                var createdAtUtc = DateTime.UtcNow;
                var entropy = Guid.NewGuid().ToString("N").Substring(0, 8);
                var runId = PlanFactory.CreateRunId(createdAtUtc, entropy);
                var history = new HistoryStore(ProjectRoot).Load();
                var previous = HistoryComparer.FindLatestComparable(history.Entries, environment);
                var context = new PlanningContext(environment, location.NormalizedPath, location.Mode, createdAtUtc, entropy, safeOutput.IsRunPathBusy(location.NormalizedPath, runId), previous);
                return PlanFactory.Create(context);
            }
            catch (EnvironmentCaptureException exception)
            {
                return PlanFactory.CreateFailure(environment, exception.Error, exception.Message);
            }
            catch (Exception)
            {
                return PlanFactory.CreateFailure(environment, BuildAssistantError.InvalidOutputRoot, "ビルド計画に必要な情報を安全に取得できませんでした。Unityの状態、出力先、アクセス権を確認してください。");
            }
        }

        /// <summary>計画時の入力を再取得して一致を確かめ、新しい実行フォルダーを予約した後に、未実行の計画を1件だけビルドします。</summary>
        /// <param name="plan">計画作成処理が返した実行可能な計画。</param>
        /// <returns>履歴保存の成否とは別にビルド結果を示す、Unityオブジェクトに依存しない結果。</returns>
        public static BuildAssistantBuildResult Build(BuildAssistantPlan plan)
        {
            if (plan == null)
                return Failure(BuildAssistantError.StalePlan, "ビルド計画が必要です。");
            if (!plan.IsReady)
                return Failure(plan.Error, plan.Message);
            if (!ExecutionGuard.TryEnter(out var lease))
                return Failure(BuildAssistantError.BuildAlreadyRunning, "ビルド実行アシスタントは既にビルドを実行中です。");

            using (lease)
            {
                if (BuildPipeline.isBuildingPlayer)
                    return Failure(BuildAssistantError.BuildAlreadyRunning, "Unityは既にプレイヤーをビルド中です。");
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                    return Failure(BuildAssistantError.EditorBusy, "Unityエディターはコンパイル、更新、または再生状態の切り替え中です。");

                var historyStore = new HistoryStore(ProjectRoot);
                var recoveryId = Guid.NewGuid().ToString("N");
                var historyFailure = RecoverInvalidHistory(historyStore, recoveryId);
                if (historyFailure != null)
                    return historyFailure;
                var recoveryFailure = RecoverPreviousRunState(historyStore, DateTime.UtcNow, recoveryId);
                if (recoveryFailure != null)
                    return recoveryFailure;

                EnvironmentSnapshot current;
                try
                {
                    current = new UnityBuildEnvironment().Capture();
                }
                catch (EnvironmentCaptureException exception)
                {
                    return Failure(exception.Error, exception.Message);
                }

                if (!SnapshotComparer.AreEquivalent(plan, current, out var difference))
                    return Failure(BuildAssistantError.StalePlan, difference);

                if (!TryCreateSafeOutput(() => new SafeBuildOutput(ProjectRoot), out var safeOutput, out var outputFailure))
                    return outputFailure;
                Func<DateTime> currentUtc = () => DateTime.UtcNow;
                return ExecuteReservedBuild(plan, historyStore, safeOutput, currentUtc, (activePlan, reservation, startedAtUtc) =>
                {
                    var report = new UnityBuildExecutor().Execute(activePlan, reservation);
                    return BuildReportReducer.Reduce(report, activePlan, startedAtUtc, currentUtc());
                });
            }
        }

        /// <summary>出力予約から履歴の後片付けまでを一続きで実行し、各保存段階の失敗を安全な結果へ変換します。</summary>
        internal static BuildAssistantBuildResult ExecuteReservedBuild(BuildAssistantPlan plan, HistoryStore historyStore, SafeBuildOutput safeOutput, Func<DateTime> currentUtc, Func<BuildAssistantPlan, OutputReservation, DateTime, BuildReportReduction> executeBuild)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (historyStore == null)
                throw new ArgumentNullException(nameof(historyStore));
            if (safeOutput == null)
                throw new ArgumentNullException(nameof(safeOutput));
            if (currentUtc == null)
                throw new ArgumentNullException(nameof(currentUtc));
            if (executeBuild == null)
                throw new ArgumentNullException(nameof(executeBuild));

            using (var reservation = safeOutput.Reserve(plan))
            {
                if (!reservation.IsReserved)
                    return Failure(reservation.Error, reservation.Message);

                var startedAtUtc = currentUtc();
                var runningState = RunState.CreateRunning(plan, startedAtUtc);
                try
                {
                    historyStore.SaveRunState(runningState);
                }
                catch (Exception)
                {
                    return Failure(BuildAssistantError.OutputReservationFailed, "実行中状態をLibraryフォルダーへ安全に記録できませんでした。アクセス権と空き容量を確認してください。");
                }

                BuildReportReduction reduction;
                BuildAssistantError revalidationError;
                string revalidationMessage;
                try
                {
                    revalidationError = reservation.Revalidate(plan, out revalidationMessage);
                }
                catch (Exception)
                {
                    revalidationError = BuildAssistantError.OutputReservationFailed;
                    revalidationMessage = "ビルド開始前に出力先を再確認できませんでした。";
                }
                if (revalidationError != BuildAssistantError.None)
                {
                    reduction = BuildReportReducer.CreateFailure(plan, startedAtUtc, currentUtc(), revalidationError, revalidationMessage);
                }
                else
                {
                    try
                    {
                        reduction = executeBuild(plan, reservation, startedAtUtc);
                        if (reduction == null || reduction.Entry == null)
                            reduction = BuildReportReducer.CreateFailure(plan, startedAtUtc, currentUtc(), BuildAssistantError.BuildReportUnavailable, "Unityからビルド報告を取得できませんでした。");
                    }
                    catch (BuildInputChangedException exception)
                    {
                        reduction = BuildReportReducer.CreateFailure(plan, startedAtUtc, currentUtc(), exception.Error, exception.Message);
                    }
                    catch (Exception)
                    {
                        reduction = BuildReportReducer.CreateFailure(plan, startedAtUtc, currentUtc(), BuildAssistantError.BuildInvocationFailed, "Unityのプレイヤービルド呼び出し中に予期しない問題が発生しました。");
                    }
                }

                var terminalStatePersisted = false;
                try
                {
                    historyStore.SaveRunState(new RunState(true, reduction.Entry));
                    terminalStatePersisted = true;
                }
                catch (Exception)
                {
                }

                try
                {
                    var history = historyStore.Load();
                    historyStore.Save(history.Entries.Where(entry => !StringComparer.Ordinal.Equals(entry.RunId, reduction.Entry.RunId)).Concat(new[] { reduction.Entry }), reduction.Entry.RunId);
                    var message = reduction.Message;
                    if (!terminalStatePersisted)
                    {
                        message = message.Length == 0 ? "履歴は保存しましたが、実行状態の後片付けを次回に再試行します。" : message + " 実行状態の後片付けを次回に再試行します。";
                    }
                    else
                    {
                        try
                        {
                            historyStore.DeleteRunState();
                        }
                        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException || exception is ArgumentException || exception is NotSupportedException)
                        {
                            message = message.Length == 0 ? "履歴は保存しましたが、実行状態の後片付けを次回に再試行します。" : message + " 実行状態の後片付けを次回に再試行します。";
                        }
                    }

                    return new BuildAssistantBuildResult(reduction.BuildSucceeded, true, reduction.Error, message, reduction.Entry);
                }
                catch (Exception)
                {
                    var error = reduction.BuildSucceeded ? BuildAssistantError.HistoryWriteFailed : reduction.Error;
                    var message = reduction.Message.Length == 0 ? "ビルドは完了しましたが、履歴を保存できませんでした。" : reduction.Message + " 履歴も保存できませんでした。";
                    if (!terminalStatePersisted)
                        message += " 終了結果を実行状態へ保存できなかったため、実行中記録を次回の復旧用に残しました。現在の結果をJSONへ書き出して保管してください。";
                    return new BuildAssistantBuildResult(reduction.BuildSucceeded, false, error, message, reduction.Entry);
                }
            }
        }

        /// <summary>Unityオブジェクトに依存しない最新20件の履歴を読み込み、現在動作していない実行中記録が残っていれば中断として記録します。</summary>
        /// <returns>新しい順に並んだ変更不能な履歴の記録。</returns>
        public static BuildAssistantHistory LoadHistory()
        {
            return LoadHistory(new HistoryStore(ProjectRoot), DateTime.UtcNow, BuildPipeline.isBuildingPlayer);
        }

        internal static BuildAssistantHistory LoadHistory(HistoryStore store, DateTime nowUtc)
        {
            return LoadHistory(store, nowUtc, false);
        }

        internal static BuildAssistantHistory LoadHistory(HistoryStore store, DateTime nowUtc, bool buildInProgress)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (buildInProgress)
                return LoadHistoryWithoutRecovery(store);
            if (!ExecutionGuard.TryEnter(out var lease))
                return LoadHistoryWithoutRecovery(store);
            using (lease)
            {
                try
                {
                    return store.HasRunState ? store.RecoverInterrupted(nowUtc) : store.Load();
                }
                catch (Exception)
                {
                    return HistoryReadFailure();
                }
            }
        }

        /// <summary>前回の実行状態を回収し、壊れた主・予備だけを復旧可能な別名へ隔離します。</summary>
        internal static BuildAssistantBuildResult RecoverPreviousRunState(HistoryStore store, DateTime nowUtc, string quarantineId)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            if (!store.HasRunState)
                return null;
            try
            {
                store.RecoverInterrupted(nowUtc);
                return null;
            }
            catch (Exception)
            {
                try
                {
                    var quarantined = store.QuarantineInvalidRunState(quarantineId);
                    if (quarantined.Count > 0)
                        return Failure(BuildAssistantError.HistoryWriteFailed, "前回の実行状態は主ファイルと予備ファイルの両方が壊れていたため、削除せず別名へ隔離しました。Library/BuildAssistantを確認してから、新しい計画を作成してください。");
                }
                catch (Exception)
                {
                }
                return Failure(BuildAssistantError.HistoryWriteFailed, "前回の実行状態を安全に回収できませんでした。Library/BuildAssistantのアクセス権と空き容量を確認してください。");
            }
        }

        /// <summary>有効な履歴が一つもない場合だけ、壊れた履歴を復旧可能な別名へ隔離します。</summary>
        internal static BuildAssistantBuildResult RecoverInvalidHistory(HistoryStore store, string quarantineId)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));
            try
            {
                if (!store.HasUnreadableHistory)
                    return null;
                var quarantined = store.QuarantineInvalidHistory(quarantineId);
                if (quarantined.Count > 0)
                    return Failure(BuildAssistantError.HistoryWriteFailed, "履歴の主ファイルと予備ファイルに有効な内容がなかったため、削除せず別名へ隔離しました。Library/BuildAssistantを確認してから、新しい計画を作成してください。");
            }
            catch (Exception)
            {
            }
            return Failure(BuildAssistantError.HistoryWriteFailed, "履歴を安全に確認できませんでした。Library/BuildAssistantの内容、アクセス権、空き容量を確認してください。");
        }

        private static BuildAssistantHistory LoadHistoryWithoutRecovery(HistoryStore store)
        {
            try
            {
                return store.Load();
            }
            catch (Exception)
            {
                return HistoryReadFailure();
            }
        }

        /// <summary>内部例外の英語詳細を画面へ出さず、履歴確認に必要な日本語案内を返します。</summary>
        private static BuildAssistantHistory HistoryReadFailure()
        {
            return new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, "履歴を読み込めませんでした。Library/BuildAssistantの内容、アクセス権、空き容量を確認してください。");
        }

        /// <summary>履歴項目を1件だけ、第1版の構造を持つJSONとして新規ファイルへ書き出します。</summary>
        /// <param name="entry">書き出す、Unityオブジェクトに依存しない履歴項目。</param>
        /// <param name="absolutePath">出力安全規則を満たす既存のローカル親フォルダー内にある、新しい.jsonファイルの絶対パス。既存ファイルは上書きしません。</param>
        /// <returns>成功時はエラーなし。失敗時は定義済みのパスまたは保存エラー。</returns>
        public static BuildAssistantError ExportJson(BuildAssistantHistoryEntry entry, string absolutePath)
        {
            return new JsonExporter(ProjectRoot).Export(entry, absolutePath);
        }

        internal static bool TryCreateSafeOutput(Func<SafeBuildOutput> factory, out SafeBuildOutput safeOutput, out BuildAssistantBuildResult failure)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            try
            {
                safeOutput = factory();
                if (safeOutput == null)
                {
                    failure = Failure(BuildAssistantError.UnsafeOutputPath, "出力先の安全規則を準備できませんでした。");
                    return false;
                }
                failure = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                safeOutput = null;
                failure = Failure(BuildAssistantError.InvalidOutputRoot, "出力先の安全規則でプロジェクトの基準フォルダーを解決できませんでした。");
                return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException)
            {
                safeOutput = null;
                failure = Failure(BuildAssistantError.UnsafeOutputPath, "出力先の安全規則でプロジェクトの基準フォルダーを確認できませんでした。");
                return false;
            }
        }

        private static string ProjectRoot => Directory.GetParent(UnityEngine.Application.dataPath).FullName;

        private static BuildAssistantBuildResult Failure(BuildAssistantError error, string message) => new BuildAssistantBuildResult(false, false, error, message, null);
    }
}
