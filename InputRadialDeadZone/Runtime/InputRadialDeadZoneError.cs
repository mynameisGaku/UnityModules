namespace InputDeadZones
{
    /// <summary>2D radial dead zone補正を完了できなかった理由。</summary>
    public enum InputRadialDeadZoneError
    {
        /// <summary>補正が成功した。</summary>
        None = 0,

        /// <summary>default値またはinner・outer境界が不正な設定が使われた。</summary>
        InvalidConfiguration = 1,

        /// <summary>horizontalまたはverticalへNaNかInfinityが入力された。</summary>
        NonFiniteInput = 2
    }
}
