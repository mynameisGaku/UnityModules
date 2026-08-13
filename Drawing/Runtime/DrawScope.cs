using System;
using UnityEngine;

namespace Drawing
{
    /// <summary>
    /// <see cref="Draw.Scope"/> が返す、既定値の退避。
    /// <para>
    /// <c>using</c> を抜けた時点で元の値に戻す。構造体なので確保は起きない。
    /// Unity のメインスレッドで作成し、同じスレッドで破棄する。
    /// </para>
    /// </summary>
    public readonly struct DrawScope : IDisposable
    {
        private readonly Color _color;
        private readonly float _duration;
        private readonly float _thickness;
        private readonly bool _depthTest;

        internal DrawScope(Color color, float duration, float thickness, bool depthTest)
        {
            _color = color;
            _duration = duration;
            _thickness = thickness;
            _depthTest = depthTest;
        }

        /// <summary>退避した色、持続時間、太さ、深度設定を元へ戻す。</summary>
        public void Dispose()
        {
            Draw.Color = _color;
            Draw.Duration = _duration;
            Draw.Thickness = _thickness;
            Draw.DepthTest = _depthTest;
        }
    }
}
