using System;

namespace BuildAssistant.Editor
{
    /// <summary>本モジュールから実行したビルドだけを、最終入力・出力検査の対象として管理します。</summary>
    internal static class BuildInputGuard
    {
        private static readonly object Gate = new object();
        private static BuildAssistantPlan activePlan;
        private static OutputReservation activeReservation;
        private static BuildAssistantError failureError;
        private static string failureMessage = string.Empty;
        private static long nextLeaseId;
        private static long activeLeaseId;

        /// <summary>確認済み計画を登録し、必ず破棄する貸出を返します。</summary>
        internal static IDisposable Begin(BuildAssistantPlan plan)
        {
            return Begin(plan, null);
        }

        /// <summary>確認済み計画と出力予約を登録し、必ず破棄する貸出を返します。</summary>
        internal static IDisposable Begin(BuildAssistantPlan plan, OutputReservation reservation)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            lock (Gate)
            {
                if (activePlan != null)
                    throw new InvalidOperationException("ビルド直前の入力検査は既に実行中です。");
                activePlan = plan;
                activeReservation = reservation;
                failureError = BuildAssistantError.None;
                failureMessage = string.Empty;
                activeLeaseId = ++nextLeaseId;
                return new Lease(activeLeaseId);
            }
        }

        /// <summary>登録中の計画があれば現在入力と出力予約を再検査し、差異時は理由を保持します。</summary>
        internal static bool Validate(Func<EnvironmentSnapshot> capture, out BuildAssistantError error, out string message)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));

            BuildAssistantPlan plan;
            OutputReservation reservation;
            long leaseId;
            lock (Gate)
            {
                plan = activePlan;
                reservation = activeReservation;
                leaseId = activeLeaseId;
            }
            if (plan == null)
            {
                error = BuildAssistantError.None;
                message = string.Empty;
                return true;
            }

            try
            {
                var current = capture();
                if (!SnapshotComparer.AreEquivalent(plan, current, out _))
                {
                    error = BuildAssistantError.StalePlan;
                    message = "ビルド前処理中に入力が変更されたため、ビルドを中止しました。計画を作り直してください。";
                    RecordFailure(leaseId, error, message);
                    return false;
                }

            }
            catch (Exception)
            {
                error = BuildAssistantError.StalePlan;
                message = "ビルド開始直前の入力を再確認できなかったため、ビルドを中止しました。設定を確認し、計画を作り直してください。";
                RecordFailure(leaseId, error, message);
                return false;
            }

            if (reservation == null)
            {
                error = BuildAssistantError.None;
                message = string.Empty;
                return true;
            }

            try
            {
                error = reservation.Revalidate(plan, out message);
                if (error == BuildAssistantError.None)
                    return true;
            }
            catch (Exception)
            {
                error = BuildAssistantError.OutputReservationFailed;
                message = "ビルド開始直前に出力先の予約状態を再確認できなかったため、ビルドを中止しました。出力先とアクセス権を確認してください。";
            }
            RecordFailure(leaseId, error, message);
            return false;
        }

        /// <summary>前処理で記録した失敗があれば、管理済み例外として送出します。</summary>
        internal static void ThrowIfRejected()
        {
            BuildAssistantError error;
            string message;
            lock (Gate)
            {
                error = failureError;
                message = failureMessage;
            }
            if (!string.IsNullOrEmpty(message))
                throw new BuildInputChangedException(error, message);
        }

        private static void RecordFailure(long leaseId, BuildAssistantError error, string message)
        {
            lock (Gate)
            {
                if (activePlan != null && activeLeaseId == leaseId && string.IsNullOrEmpty(failureMessage))
                {
                    failureError = error;
                    failureMessage = message ?? string.Empty;
                }
            }
        }

        private sealed class Lease : IDisposable
        {
            private readonly long leaseId;
            private bool disposed;

            internal Lease(long leaseId)
            {
                this.leaseId = leaseId;
            }

            public void Dispose()
            {
                lock (Gate)
                {
                    if (disposed)
                        return;
                    disposed = true;
                    if (activeLeaseId != leaseId)
                        return;
                    activePlan = null;
                    activeReservation = null;
                    failureError = BuildAssistantError.None;
                    failureMessage = string.Empty;
                    activeLeaseId = 0;
                }
            }
        }
    }
}
