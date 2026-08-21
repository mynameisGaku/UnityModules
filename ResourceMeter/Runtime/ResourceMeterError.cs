namespace GameplayResources
{
    /// <summary>resourceの構成または変更要求を処理できなかった理由。</summary>
    public enum ResourceMeterError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>capacityが非有限または0以下だった。</summary>
        InvalidCapacity = 1,

        /// <summary>currentまたはreset値がNaNかInfinityだった。</summary>
        NonFiniteValue = 2,

        /// <summary>currentまたはreset値が0以上capacity以下の範囲外だった。</summary>
        ValueOutOfRange = 3,

        /// <summary>回復または消費amountがNaNかInfinityだった。</summary>
        NonFiniteAmount = 4,

        /// <summary>回復または消費amountが負だった。</summary>
        NegativeAmount = 5,

        /// <summary>消費policyが未定義値だった。</summary>
        InvalidPolicy = 6
    }
}
