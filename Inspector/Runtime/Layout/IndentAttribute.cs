using System;

namespace Inspector
{
    /// <summary>
    /// 字下げしてから描く。直前のフィールドに従属することを見た目で示したいときに使う。
    /// <code>
    /// [SerializeField] private bool _useFog;
    ///
    /// [ShowIf(nameof(_useFog))] [Indent]
    /// [SerializeField] private Color _fogColor;
    /// </code>
    /// <para>
    /// 字下げはこのメンバーの描画中だけ適用され、次のメンバーには波及しない。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class IndentAttribute : InspectorAttribute
    {
        /// <summary>指定した段数だけ対象メンバーを字下げする。</summary>
        /// <param name="levels">字下げする段数。負の値なら外側へ移動する。</param>
        public IndentAttribute(int levels = 1) => Levels = levels;

        /// <summary>字下げの段数。負の値で外側に押し出すこともできる。</summary>
        public int Levels { get; }
    }
}
