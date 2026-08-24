namespace GameplayDecision
{
    /// <summary>採用候補と入力順の全候補明細を保持する評価結果です。</summary>
    public sealed class UtilityScoreEvaluation
    {
        private readonly UtilityScoreCandidateLine[] _lines;

        internal UtilityScoreEvaluation(int selectedCandidateIdentifier, int selectedInputIndex, double selectedScore, UtilityScoreCandidateLine[] lines)
        {
            SelectedCandidateIdentifier = selectedCandidateIdentifier;
            SelectedInputIndex = selectedInputIndex;
            SelectedScore = selectedScore;
            _lines = (UtilityScoreCandidateLine[])lines.Clone();
        }

        /// <summary>採用された候補の識別値です。</summary>
        public int SelectedCandidateIdentifier { get; }

        /// <summary>採用された候補の入力indexです。</summary>
        public int SelectedInputIndex { get; }

        /// <summary>採用された候補のweighted mean scoreです。</summary>
        public double SelectedScore { get; }

        /// <summary>評価した候補数です。</summary>
        public int CandidateCount => _lines.Length;

        /// <summary>指定した入力indexの候補明細を取得します。</summary>
        public bool TryGetCandidateLine(int index, out UtilityScoreCandidateLine line)
        {
            if (index < 0 || index >= _lines.Length)
            {
                line = default;
                return false;
            }

            line = _lines[index];
            return true;
        }
    }
}
