namespace GameplayResources
{
    /// <summary>resourceが要求amountより少ない時の消費方法。</summary>
    public enum ResourceSpendPolicy
    {
        /// <summary>現在値までを消費し、満たせない量を結果へ返す。</summary>
        AllowPartial = 0,

        /// <summary>全amountを消費できない場合はstateを変えない。</summary>
        RequireFull = 1
    }
}
