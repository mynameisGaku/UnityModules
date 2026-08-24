namespace GameplayDamage
{
    /// <summary>damage軽減評価を作成できなかった理由です。</summary>
    public enum DamageMitigationError
    {
        /// <summary>失敗はありません。</summary>
        None = 0,
        /// <summary>元damageが有限値ではありません。</summary>
        NonFiniteDamage = 1,
        /// <summary>元damageが負数です。</summary>
        NegativeDamage = 2,
        /// <summary>軽減層配列がnullです。</summary>
        NullLayers = 3,
        /// <summary>軽減層数が上限を超えています。</summary>
        InvalidLayerCount = 4,
        /// <summary>軽減層IDが正数ではありません。</summary>
        InvalidLayerId = 5,
        /// <summary>軽減層IDが重複しています。</summary>
        DuplicateLayerId = 6,
        /// <summary>計算方法が定義済み列挙値ではありません。</summary>
        InvalidKind = 7,
        /// <summary>軽減値が有限値ではありません。</summary>
        NonFiniteValue = 8,
        /// <summary>軽減値が負数です。</summary>
        NegativeValue = 9,
        /// <summary>率軽減値が0〜1の範囲外です。</summary>
        RatioOutOfRange = 10
    }
}
