namespace GameplayMath
{
    /// <summary>Piecewise Linear Curveの操作を拒否した理由。</summary>
    public enum CurveError
    {
        /// <summary>操作が成功した。</summary>
        None = 0,

        /// <summary>Xが有限値ではない。</summary>
        InvalidX = 1,

        /// <summary>Yが有限値ではない。</summary>
        InvalidY = 2,

        /// <summary>同じXのpointが既に存在する。</summary>
        DuplicateX = 3,

        /// <summary>指定Xのpointが存在しない。</summary>
        PointNotFound = 4,

        /// <summary>point数が上限へ達している。</summary>
        CapacityReached = 5,

        /// <summary>評価できるpointが存在しない。</summary>
        EmptyCurve = 6,

        /// <summary>queryが有限値ではない。</summary>
        InvalidQuery = 7,

        /// <summary>指定indexが現在のpoint範囲外である。</summary>
        IndexOutOfRange = 8,

        /// <summary>補間結果が有限範囲を超える。</summary>
        NumericOverflow = 9
    }
}
