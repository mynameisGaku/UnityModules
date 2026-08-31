namespace PlayModeTuning.Editor
{
    /// <summary>記録と反映に対応する最上位のシリアル化項目の値形式を表します。</summary>
    public enum PlayModeTuningValueKind
    {
        /// <summary>真偽値を表します。</summary>
        Boolean,

        /// <summary>符号付き整数値を表します。</summary>
        SignedInteger,

        /// <summary>符号なし整数値を表します。</summary>
        UnsignedInteger,

        /// <summary>1文字の値を表します。</summary>
        Character,

        /// <summary>単精度浮動小数点値を表します。</summary>
        Float,

        /// <summary>倍精度浮動小数点値を表します。</summary>
        Double,

        /// <summary>文字列値を表します。</summary>
        String,

        /// <summary>列挙値を表します。</summary>
        Enum,

        /// <summary>Unityのレイヤーマスク値を表します。</summary>
        LayerMask,

        /// <summary>Unityの色値を表します。</summary>
        Color,

        /// <summary>Unityの2次元ベクトル値を表します。</summary>
        Vector2,

        /// <summary>Unityの3次元ベクトル値を表します。</summary>
        Vector3,

        /// <summary>Unityの4次元ベクトル値を表します。</summary>
        Vector4,

        /// <summary>Unityの2次元整数ベクトル値を表します。</summary>
        Vector2Int,

        /// <summary>Unityの3次元整数ベクトル値を表します。</summary>
        Vector3Int,

        /// <summary>Unityの浮動小数点矩形値を表します。</summary>
        Rect,

        /// <summary>Unityの整数矩形値を表します。</summary>
        RectInt,

        /// <summary>Unityの浮動小数点境界値を表します。</summary>
        Bounds,

        /// <summary>Unityの整数境界値を表します。</summary>
        BoundsInt,

        /// <summary>Unityの回転を表す四元数値を表します。</summary>
        Quaternion
    }
}
