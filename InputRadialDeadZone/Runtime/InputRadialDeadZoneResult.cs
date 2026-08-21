using System;

namespace InputDeadZones
{
    /// <summary>補正済み2D成分と失敗理由を同時に表すimmutableな結果。</summary>
    public readonly struct InputRadialDeadZoneResult : IEquatable<InputRadialDeadZoneResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の補正済みhorizontal成分。失敗時は0。</summary>
        public double Horizontal { get; }

        /// <summary>成功時の補正済みvertical成分。失敗時は0。</summary>
        public double Vertical { get; }

        /// <summary>成功時の補正済みmagnitude。0以上1以下。失敗時は0。</summary>
        public double Magnitude { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputRadialDeadZoneError Error { get; }

        /// <summary>有効な補正結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputRadialDeadZoneError.None;

        /// <summary>成功結果のmagnitudeが0か。</summary>
        public bool IsZero => Succeeded && Magnitude == 0d;

        private InputRadialDeadZoneResult(double horizontal, double vertical, double magnitude, InputRadialDeadZoneError error, bool hasValue)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Magnitude = magnitude;
            Error = error;
            _hasValue = hasValue;
        }

        /// <summary>成功結果を作成する。</summary>
        internal static InputRadialDeadZoneResult Success(double horizontal, double vertical, double magnitude) => new InputRadialDeadZoneResult(horizontal, vertical, magnitude, InputRadialDeadZoneError.None, true);

        /// <summary>失敗結果を作成する。</summary>
        internal static InputRadialDeadZoneResult Failure(InputRadialDeadZoneError error) => new InputRadialDeadZoneResult(0d, 0d, 0d, error, false);

        /// <summary>成分、magnitude、error、成功状態が同じかを返す。</summary>
        public bool Equals(InputRadialDeadZoneResult other) => Horizontal.Equals(other.Horizontal) && Vertical.Equals(other.Vertical) && Magnitude.Equals(other.Magnitude) && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        public override bool Equals(object obj) => obj is InputRadialDeadZoneResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Horizontal.GetHashCode();
                hash = (hash * 397) ^ Vertical.GetHashCode();
                hash = (hash * 397) ^ Magnitude.GetHashCode();
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        public static bool operator ==(InputRadialDeadZoneResult left, InputRadialDeadZoneResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        public static bool operator !=(InputRadialDeadZoneResult left, InputRadialDeadZoneResult right) => !left.Equals(right);
    }
}
