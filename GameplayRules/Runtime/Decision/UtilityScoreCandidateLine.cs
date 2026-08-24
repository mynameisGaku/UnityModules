namespace GameplayDecision
{
    /// <summary>1候補のscoreとfactor別寄与を再構築できる明細です。</summary>
    public readonly struct UtilityScoreCandidateLine
    {
        private readonly UtilityScoreFactorLine[] _factorLines;

        internal UtilityScoreCandidateLine(int candidateIdentifier, int inputIndex, double totalWeight, double score, UtilityScoreFactorLine[] factorLines)
        {
            CandidateIdentifier = candidateIdentifier;
            InputIndex = inputIndex;
            TotalWeight = totalWeight;
            Score = score;
            _factorLines = (UtilityScoreFactorLine[])factorLines.Clone();
        }

        /// <summary>候補識別値です。</summary>
        public int CandidateIdentifier { get; }

        /// <summary>候補の入力indexです。</summary>
        public int InputIndex { get; }

        /// <summary>factor weightの合計です。</summary>
        public double TotalWeight { get; }

        /// <summary>weighted meanで算出した候補scoreです。</summary>
        public double Score { get; }

        /// <summary>保持しているfactor明細数です。</summary>
        public int FactorCount => _factorLines?.Length ?? 0;

        /// <summary>指定indexのfactor明細を取得します。</summary>
        public bool TryGetFactorLine(int index, out UtilityScoreFactorLine line)
        {
            if (_factorLines == null || index < 0 || index >= _factorLines.Length)
            {
                line = default;
                return false;
            }

            line = _factorLines[index];
            return true;
        }
    }
}
