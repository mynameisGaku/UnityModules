using System;

namespace StartupFlow
{
    /// <summary>起動処理の成否、停止位置、完了件数、説明を表す不変値。</summary>
    public readonly struct StartupFlowResult : IEquatable<StartupFlowResult>
    {
        internal StartupFlowResult(StartupFlowError error, string failedStepId, int completedStepCount, int totalStepCount, string message)
        {
            Error = error;
            FailedStepId = failedStepId ?? string.Empty;
            CompletedStepCount = completedStepCount;
            TotalStepCount = totalStepCount;
            Message = message ?? string.Empty;
        }

        /// <summary>すべてのstepが成功した場合にtrue。</summary>
        public bool IsSuccess => Error == StartupFlowError.None;

        /// <summary>失敗理由。成功時はNone。</summary>
        public StartupFlowError Error { get; }

        /// <summary>失敗または中止が確定したstep識別子。成功時やstep開始前は空文字列。</summary>
        public string FailedStepId { get; }

        /// <summary>成功して完了したstep数。</summary>
        public int CompletedStepCount { get; }

        /// <summary>受理または検証対象になったstep総数。</summary>
        public int TotalStepCount { get; }

        /// <summary>利用側へ表示できる簡潔な説明。</summary>
        public string Message { get; }

        /// <summary>すべての結果値が等しい場合にtrue。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(StartupFlowResult other) => Error == other.Error && string.Equals(FailedStepId, other.FailedStepId, StringComparison.Ordinal) && CompletedStepCount == other.CompletedStepCount && TotalStepCount == other.TotalStepCount && string.Equals(Message, other.Message, StringComparison.Ordinal);

        /// <summary>同じ型で、すべての結果値が等しい場合にtrue。</summary>
        /// <param name="obj">比較対象。</param>
        public override bool Equals(object obj) => obj is StartupFlowResult other && Equals(other);

        /// <summary>すべての結果値からhash codeを作る。</summary>
        public override int GetHashCode() => HashCode.Combine((int)Error, FailedStepId, CompletedStepCount, TotalStepCount, Message);

        /// <summary>2つの結果値が等しい場合にtrue。</summary>
        public static bool operator ==(StartupFlowResult left, StartupFlowResult right) => left.Equals(right);

        /// <summary>2つの結果値が異なる場合にtrue。</summary>
        public static bool operator !=(StartupFlowResult left, StartupFlowResult right) => !left.Equals(right);

        internal static StartupFlowResult Success(int totalStepCount) => new StartupFlowResult(StartupFlowError.None, string.Empty, totalStepCount, totalStepCount, "すべてのstartup stepが完了しました。");

        internal static StartupFlowResult Failure(StartupFlowError error, string stepId, int completedStepCount, int totalStepCount, string message) => new StartupFlowResult(error, stepId, completedStepCount, totalStepCount, message);
    }
}
