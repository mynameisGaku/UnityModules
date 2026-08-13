using UnityEngine;

namespace Inspector
{
    /// <summary>
    /// 属性で色を指定するための名前付き色。
    /// <para>
    /// 属性の引数には <see cref="Color"/> のような構造体を渡せない（定数しか書けない）ため、
    /// よく使う色を列挙で用意している。任意の色が要る場所には <c>float</c> 3 つを取る
    /// 別のコンストラクタを用意してある（<see cref="GUIColorAttribute"/> など）。
    /// </para>
    /// </summary>
    public enum InspectorColor
    {
        /// <summary>エディタの既定色をそのまま使う。</summary>
        Default,

        Gray,
        White,
        Black,
        Red,
        Green,
        Blue,
        Yellow,
        Orange,
        Cyan,
        Magenta,
        Pink,
        Violet,
    }

    /// <summary><see cref="InspectorColor"/> を実際の色に直す。</summary>
    public static class InspectorColors
    {
        /// <summary>
        /// 名前付き色を <see cref="Color"/> にする。
        /// <para>
        /// <see cref="InspectorColor.Default"/> は呼び出し側で「今の GUI 色を維持する」意味に
        /// 使うため、ここでは <paramref name="fallback"/> をそのまま返す。
        /// </para>
        /// </summary>
        public static Color ToColor(this InspectorColor color, Color fallback)
        {
            switch (color)
            {
                case InspectorColor.Gray: return new Color(0.5f, 0.5f, 0.5f, 1f);
                case InspectorColor.White: return Color.white;
                case InspectorColor.Black: return Color.black;
                case InspectorColor.Red: return new Color(0.85f, 0.24f, 0.24f, 1f);
                case InspectorColor.Green: return new Color(0.27f, 0.72f, 0.35f, 1f);
                case InspectorColor.Blue: return new Color(0.24f, 0.51f, 0.85f, 1f);
                case InspectorColor.Yellow: return new Color(0.93f, 0.82f, 0.24f, 1f);
                case InspectorColor.Orange: return new Color(0.93f, 0.56f, 0.20f, 1f);
                case InspectorColor.Cyan: return new Color(0.25f, 0.78f, 0.80f, 1f);
                case InspectorColor.Magenta: return new Color(0.80f, 0.27f, 0.70f, 1f);
                case InspectorColor.Pink: return new Color(0.94f, 0.55f, 0.68f, 1f);
                case InspectorColor.Violet: return new Color(0.58f, 0.42f, 0.87f, 1f);
                default: return fallback;
            }
        }
    }
}
