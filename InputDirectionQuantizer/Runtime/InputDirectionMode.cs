namespace InputDirectionQuantization
{
    /// <summary>2D analog入力を分類する方向数。</summary>
    public enum InputDirectionMode
    {
        /// <summary>horizontalまたはverticalの4方向。絶対値tieはvertical。</summary>
        FourWay = 1,

        /// <summary>cardinalとdiagonalを含む8方向。</summary>
        EightWay = 2
    }
}
