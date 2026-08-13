using System;
using UnityEngine;

namespace Inspector
{
    /// <summary>
    /// このメンバーを描く間だけ GUI の色を変える。危険な設定を赤くする、といった用途。
    /// <code>
    /// [GUIColor(InspectorColor.Red)]
    /// [SerializeField] private bool _wipeSaveOnBoot;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class GUIColorAttribute : StyleAttribute
    {
        /// <summary>対象メンバーを描く間に使う名前付き色を指定する。</summary>
        /// <param name="color">描画に使う名前付き色。</param>
        public GUIColorAttribute(InspectorColor color) => Named = color;

        /// <summary>対象メンバーを描く間に使う色を各成分で指定する。</summary>
        /// <param name="r">赤 0..1。</param>
        /// <param name="g">緑 0..1。</param>
        /// <param name="b">青 0..1。</param>
        /// <param name="a">不透明度 0..1。</param>
        public GUIColorAttribute(float r, float g, float b, float a = 1f)
        {
            Named = InspectorColor.Default;
            Explicit = new Color(r, g, b, a);
        }

        /// <summary>名前で指定された色。<see cref="InspectorColor.Default"/> なら <see cref="Explicit"/> を見る。</summary>
        public InspectorColor Named { get; }

        /// <summary>数値で直接指定された色。名前指定のときは <c>null</c>。</summary>
        public Color? Explicit { get; }

        /// <summary>実際に使う色を返す。どちらの指定もなければ <paramref name="fallback"/>。</summary>
        /// <param name="fallback">名前付き色が既定色のときに使う現在の GUI 色。</param>
        /// <returns>数値指定または名前指定から解決した描画色。</returns>
        public Color Resolve(Color fallback)
        {
            if (Explicit.HasValue) return Explicit.Value;
            return Named.ToColor(fallback);
        }
    }
}
