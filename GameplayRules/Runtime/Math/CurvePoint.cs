using System;

namespace GameplayMath
{
    /// <summary>X昇順で保持される1つの有限curve point。</summary>
    public readonly struct CurvePoint : IEquatable<CurvePoint>
    {
        internal CurvePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>pointの一意な有限X座標。</summary>
        public double X { get; }

        /// <summary>pointの有限Y座標。</summary>
        public double Y { get; }

        /// <inheritdoc />
        public bool Equals(CurvePoint other) => X.Equals(other.X) && Y.Equals(other.Y);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CurvePoint other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        /// <summary>2つのpointが同じXとYを持つか判定する。</summary>
        public static bool operator ==(CurvePoint left, CurvePoint right) => left.Equals(right);

        /// <summary>2つのpointのXまたはYが異なるか判定する。</summary>
        public static bool operator !=(CurvePoint left, CurvePoint right) => !left.Equals(right);
    }
}
