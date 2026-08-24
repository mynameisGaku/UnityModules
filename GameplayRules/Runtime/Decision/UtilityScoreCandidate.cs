using System;

namespace GameplayDecision
{
    /// <summary>選択候補と、その候補を評価するfactor列を表します。</summary>
    public readonly struct UtilityScoreCandidate
    {
        private readonly UtilityScoreFactor[] _factors;

        /// <summary>識別値とfactor列から候補を構築し、入力配列を複製します。</summary>
        public UtilityScoreCandidate(int identifier, UtilityScoreFactor[] factors)
        {
            Identifier = identifier;
            _factors = factors == null ? null : (UtilityScoreFactor[])factors.Clone();
        }

        /// <summary>候補を区別する正の識別値です。</summary>
        public int Identifier { get; }

        /// <summary>保持しているfactor数です。未設定候補では0です。</summary>
        public int FactorCount => _factors?.Length ?? 0;

        /// <summary>指定indexのfactorを取得します。</summary>
        public bool TryGetFactor(int index, out UtilityScoreFactor factor)
        {
            if (_factors == null || index < 0 || index >= _factors.Length)
            {
                factor = default;
                return false;
            }

            factor = _factors[index];
            return true;
        }

        internal UtilityScoreFactor[] CopyFactors()
        {
            return _factors == null ? Array.Empty<UtilityScoreFactor>() : (UtilityScoreFactor[])_factors.Clone();
        }
    }
}
