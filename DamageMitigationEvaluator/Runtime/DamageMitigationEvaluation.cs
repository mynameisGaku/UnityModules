namespace GameplayDamage
{
    /// <summary>元damage、最終damage、入力順の軽減明細を保持する不変評価結果です。</summary>
    public sealed class DamageMitigationEvaluation
    {
        private readonly DamageMitigationStep[] _steps;

        internal DamageMitigationEvaluation(double originalDamage, double finalDamage, DamageMitigationStep[] steps)
        {
            OriginalDamage = originalDamage;
            FinalDamage = finalDamage;
            _steps = steps;
        }

        /// <summary>軽減前の元damageを取得します。</summary>
        public double OriginalDamage { get; }
        /// <summary>全層適用後の0以上のdamageを取得します。</summary>
        public double FinalDamage { get; }
        /// <summary>全層で実際に軽減した合計量を取得します。</summary>
        public double MitigatedDamage => OriginalDamage - FinalDamage;
        /// <summary>最終damageが0になったかを取得します。</summary>
        public bool WasFullyMitigated => FinalDamage == 0d;
        /// <summary>入力順の軽減明細数を取得します。</summary>
        public int StepCount => _steps.Length;

        /// <summary>入力順indexから軽減明細を取得します。</summary>
        /// <param name="index">0以上StepCount未満のindexです。</param>
        /// <param name="step">indexが有効な場合に明細を返します。</param>
        /// <returns>indexが有効な場合はtrueです。</returns>
        public bool TryGetStep(int index, out DamageMitigationStep step)
        {
            if (index < 0 || index >= _steps.Length)
            {
                step = default;
                return false;
            }

            step = _steps[index];
            return true;
        }
    }
}
