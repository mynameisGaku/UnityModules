namespace GameplayDamage
{
    /// <summary>入力順に適用する1件のdamage軽減層を保持する不変値です。</summary>
    public readonly struct DamageMitigationLayer
    {
        /// <summary>識別子、計算方法、軽減値を指定して層を作成します。</summary>
        /// <param name="layerId">結果明細と対応付ける正の識別子です。</param>
        /// <param name="kind">固定量または軽減率の計算方法です。</param>
        /// <param name="value">固定軽減量、または0〜1の軽減率です。</param>
        public DamageMitigationLayer(int layerId, DamageMitigationKind kind, double value)
        {
            LayerId = layerId;
            Kind = kind;
            Value = value;
        }

        /// <summary>結果明細と対応付ける識別子を取得します。</summary>
        public int LayerId { get; }

        /// <summary>この層の計算方法を取得します。</summary>
        public DamageMitigationKind Kind { get; }

        /// <summary>固定軽減量、または0〜1の軽減率を取得します。</summary>
        public double Value { get; }
    }
}
