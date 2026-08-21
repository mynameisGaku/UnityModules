namespace GameplayTiming
{
    /// <summary>Charge Cooldownの要求を処理できなかった理由です。</summary>
    public enum ChargeCooldownError
    {
        /// <summary>失敗していません。</summary>
        None = 0,
        /// <summary>最大charge数が1〜32の範囲外です。</summary>
        InvalidMaximumCharges = 1,
        /// <summary>1 chargeの回復tick数が正ではありません。</summary>
        InvalidRechargeInterval = 2,
        /// <summary>指定tickが負です。</summary>
        InvalidTick = 3,
        /// <summary>初期charge数が0〜最大charge数の範囲外です。</summary>
        InvalidInitialCharges = 4,
        /// <summary>復元したstateがrulesと整合しません。</summary>
        InvalidState = 5,
        /// <summary>指定tickがstateの最終評価tickより前です。</summary>
        TickMovedBackward = 6,
        /// <summary>次回復tickを64-bit整数で表現できません。</summary>
        TickOverflow = 7
    }
}
