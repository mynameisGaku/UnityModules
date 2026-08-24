namespace GameplayDamage
{
    /// <summary>damageへ適用する軽減層の計算方法です。</summary>
    public enum DamageMitigationKind
    {
        /// <summary>現在damageから固定量を減らします。</summary>
        FlatReduction = 0,
        /// <summary>現在damageへ0〜1の軽減率を適用します。</summary>
        RatioReduction = 1
    }
}
