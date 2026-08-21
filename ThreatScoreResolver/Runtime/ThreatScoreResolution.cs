using System;

namespace GameplayThreat
{
    /// <summary>
    /// 入力を変更せずに解決した全対象の最終score、全増減明細、安定した首位を保持します。
    /// </summary>
    public sealed class ThreatScoreResolution
    {
        private readonly ThreatScoreEntry[] _entries;
        private readonly ThreatScoreStep[] _steps;

        /// <summary>最終entry数を取得します。</summary>
        public int EntryCount => _entries.Length;
        /// <summary>適用した増減明細数を取得します。</summary>
        public int StepCount => _steps.Length;
        /// <summary>最大scoreを持つ対象識別子を取得します。同点時は小さい識別子です。</summary>
        public int LeaderTargetId { get; }
        /// <summary>最大の最終scoreを取得します。</summary>
        public double LeaderScore { get; }

        internal ThreatScoreResolution(ThreatScoreEntry[] entries, ThreatScoreStep[] steps, int leaderTargetId, double leaderScore)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            LeaderTargetId = leaderTargetId;
            LeaderScore = leaderScore;
        }

        /// <summary>
        /// 入力順を保った最終entryを取得します。範囲外indexではfalseを返します。
        /// </summary>
        public bool TryGetEntry(int index, out ThreatScoreEntry entry)
        {
            if ((uint)index >= (uint)_entries.Length)
            {
                entry = default;
                return false;
            }

            entry = _entries[index];
            return true;
        }

        /// <summary>
        /// 入力順の増減明細を取得します。範囲外indexではfalseを返します。
        /// </summary>
        public bool TryGetStep(int index, out ThreatScoreStep step)
        {
            if ((uint)index >= (uint)_steps.Length)
            {
                step = default;
                return false;
            }

            step = _steps[index];
            return true;
        }
    }
}
