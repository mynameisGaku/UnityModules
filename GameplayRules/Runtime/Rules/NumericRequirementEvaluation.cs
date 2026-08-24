namespace GameplayRules
{
    /// <summary>全条件の成立可否と、入力順のimmutableな条件明細を保持します。</summary>
    public sealed class NumericRequirementEvaluation
    {
        private readonly NumericRequirementLine[] _lines;

        internal NumericRequirementEvaluation(bool allSatisfied, NumericRequirementLine[] lines)
        {
            AllSatisfied = allSatisfied;
            _lines = lines;
        }

        /// <summary>全条件を満たした場合はtrueです。</summary>
        public bool AllSatisfied { get; }
        /// <summary>評価した条件明細数を取得します。</summary>
        public int LineCount => _lines.Length;

        /// <summary>入力順のindexから条件明細を取得します。</summary>
        public bool TryGetLine(int index, out NumericRequirementLine line)
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
