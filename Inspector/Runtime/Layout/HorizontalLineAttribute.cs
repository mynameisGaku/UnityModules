using System;

namespace Inspector
{
    /// <summary>
    /// フィールドの上に区切り線を引く。見出しを付けるほどではないが、話が変わる場所に使う。
    /// <code>
    /// [HorizontalLine]
    /// [SerializeField] private AudioClip _hitSound;
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HorizontalLineAttribute : DecoratorAttribute
    {
        /// <summary>対象メンバーの上に指定した太さと色の区切り線を引く。</summary>
        /// <param name="height">線の太さ。</param>
        /// <param name="color">線に使う色。</param>
        public HorizontalLineAttribute(float height = 1f, InspectorColor color = InspectorColor.Gray)
        {
            Height = height;
            Color = color;
        }

        /// <summary>線の太さ。</summary>
        public float Height { get; }

        /// <summary>線に使う色。</summary>
        public InspectorColor Color { get; }

        /// <summary>線の上に空ける余白。</summary>
        public float SpaceBefore { get; set; } = 6f;

        /// <summary>線の下に空ける余白。</summary>
        public float SpaceAfter { get; set; } = 6f;

        /// <inheritdoc/>
        public override DecoratorPosition Position => DecoratorPosition.Before;
    }
}
