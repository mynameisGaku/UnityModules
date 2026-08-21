using System;
using UnityEngine;

namespace AdaptiveLayout
{
    /// <summary>
    /// Describes a validated safe area relative to a screen-sized viewport.
    /// </summary>
    public readonly struct SafeAreaSnapshot : IEquatable<SafeAreaSnapshot>
    {
        internal SafeAreaSnapshot(Vector2Int screenSize, Rect safeArea)
        {
            ScreenSize = screenSize;
            SafeArea = safeArea;
        }

        /// <summary>Gets the viewport size in screen pixels.</summary>
        public Vector2Int ScreenSize { get; }

        /// <summary>Gets the safe rectangle in bottom-left-origin screen pixels.</summary>
        public Rect SafeArea { get; }

        /// <summary>Gets the unsafe distance from the left edge in screen pixels.</summary>
        public float LeftInset => SafeArea.xMin;

        /// <summary>Gets the unsafe distance from the top edge in screen pixels.</summary>
        public float TopInset => ScreenSize.y - SafeArea.yMax;

        /// <summary>Gets the unsafe distance from the right edge in screen pixels.</summary>
        public float RightInset => ScreenSize.x - SafeArea.xMax;

        /// <summary>Gets the unsafe distance from the bottom edge in screen pixels.</summary>
        public float BottomInset => SafeArea.yMin;

        /// <summary>Gets whether the safe area covers the complete viewport.</summary>
        public bool IsFullViewport => SafeArea.xMin == 0f
            && SafeArea.yMin == 0f
            && SafeArea.xMax == ScreenSize.x
            && SafeArea.yMax == ScreenSize.y;

        /// <inheritdoc />
        public bool Equals(SafeAreaSnapshot other)
        {
            return ScreenSize == other.ScreenSize && SafeArea == other.SafeArea;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is SafeAreaSnapshot other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(ScreenSize, SafeArea);
        }

        /// <summary>Compares two snapshots for exact equality.</summary>
        public static bool operator ==(SafeAreaSnapshot left, SafeAreaSnapshot right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two snapshots for inequality.</summary>
        public static bool operator !=(SafeAreaSnapshot left, SafeAreaSnapshot right)
        {
            return !left.Equals(right);
        }
    }
}
