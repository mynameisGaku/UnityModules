namespace GameplayDamage
{
    /// <summary>1件の軽減層がdamageへ与えた入出力を保持する不変明細です。</summary>
    public readonly struct DamageMitigationStep
    {
        internal DamageMitigationStep(int layerId, DamageMitigationKind kind, double value, double inputDamage, double requestedReduction, double appliedReduction, double outputDamage)
        {
            LayerId = layerId;
            Kind = kind;
            Value = value;
            InputDamage = inputDamage;
            RequestedReduction = requestedReduction;
            AppliedReduction = appliedReduction;
            OutputDamage = outputDamage;
        }

        /// <summary>入力層の識別子を取得します。</summary>
        public int LayerId { get; }
        /// <summary>適用した計算方法を取得します。</summary>
        public DamageMitigationKind Kind { get; }
        /// <summary>入力層の固定量または軽減率を取得します。</summary>
        public double Value { get; }
        /// <summary>この層へ入る前のdamageを取得します。</summary>
        public double InputDamage { get; }
        /// <summary>層が要求した軽減量を取得します。</summary>
        public double RequestedReduction { get; }
        /// <summary>0未満へ下げない範囲で実際に適用した軽減量を取得します。</summary>
        public double AppliedReduction { get; }
        /// <summary>この層を適用した後のdamageを取得します。</summary>
        public double OutputDamage { get; }
        /// <summary>要求量が残damageを超えたため実適用量が制限されたかを取得します。</summary>
        public bool WasClamped => AppliedReduction < RequestedReduction;
    }
}
