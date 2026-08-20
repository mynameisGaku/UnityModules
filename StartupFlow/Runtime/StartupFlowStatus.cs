using System;

namespace StartupFlow
{
    /// <summary>現在の段階、step、件数、step内進捗、flow全体進捗を表す不変値。</summary>
    public readonly struct StartupFlowStatus : IEquatable<StartupFlowStatus>
    {
        internal StartupFlowStatus(StartupFlowPhase phase, string stepId, int stepIndex, int totalStepCount, float stepProgress, float overallProgress)
        {
            Phase = phase;
            StepId = stepId ?? string.Empty;
            StepIndex = stepIndex;
            TotalStepCount = totalStepCount;
            StepProgress = stepProgress;
            OverallProgress = overallProgress;
        }

        /// <summary>現在の処理段階。</summary>
        public StartupFlowPhase Phase { get; }

        /// <summary>現在のstep識別子。stepを実行していない場合は空文字列。</summary>
        public string StepId { get; }

        /// <summary>現在の0始まりstep位置。stepを実行していない場合は-1。</summary>
        public int StepIndex { get; }

        /// <summary>受理したstep総数。</summary>
        public int TotalStepCount { get; }

        /// <summary>現在stepの0以上1以下の進捗。</summary>
        public float StepProgress { get; }

        /// <summary>完了件数とstep進捗から求めたflow全体の0以上1以下の進捗。</summary>
        public float OverallProgress { get; }

        /// <summary>すべての状態値が等しい場合にtrue。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(StartupFlowStatus other) => Phase == other.Phase && string.Equals(StepId, other.StepId, StringComparison.Ordinal) && StepIndex == other.StepIndex && TotalStepCount == other.TotalStepCount && StepProgress.Equals(other.StepProgress) && OverallProgress.Equals(other.OverallProgress);

        /// <summary>同じ型で、すべての状態値が等しい場合にtrue。</summary>
        /// <param name="obj">比較対象。</param>
        public override bool Equals(object obj) => obj is StartupFlowStatus other && Equals(other);

        /// <summary>すべての状態値からhash codeを作る。</summary>
        public override int GetHashCode() => HashCode.Combine((int)Phase, StepId, StepIndex, TotalStepCount, StepProgress, OverallProgress);

        /// <summary>2つの状態値が等しい場合にtrue。</summary>
        public static bool operator ==(StartupFlowStatus left, StartupFlowStatus right) => left.Equals(right);

        /// <summary>2つの状態値が異なる場合にtrue。</summary>
        public static bool operator !=(StartupFlowStatus left, StartupFlowStatus right) => !left.Equals(right);
    }
}
