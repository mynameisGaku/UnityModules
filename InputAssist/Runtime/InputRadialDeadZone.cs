using System;

namespace InputDeadZones
{
    /// <summary>有限2D analog入力へ方向を保つradial dead zone補正を行うimmutable設定。</summary>
    public readonly struct InputRadialDeadZone : IEquatable<InputRadialDeadZone>
    {
        /// <summary>0へ収束させるinclusiveなinner境界。</summary>
        public double InnerDeadZone { get; }

        /// <summary>unit magnitudeへ収束させるinclusiveなouter境界。</summary>
        public double OuterDeadZone { get; }

        /// <summary>default値ではなく、0以上inner未満outer以下1の構成を満たすか。</summary>
        public bool IsValid => IsFinite(InnerDeadZone) && IsFinite(OuterDeadZone) && InnerDeadZone >= 0d && InnerDeadZone < OuterDeadZone && OuterDeadZone <= 1d;

        private InputRadialDeadZone(double innerDeadZone, double outerDeadZone)
        {
            InnerDeadZone = innerDeadZone;
            OuterDeadZone = outerDeadZone;
        }

        /// <summary>inner・outer境界を検証してradial dead zone設定を作成する。</summary>
        /// <param name="innerDeadZone">このmagnitude以下を0へ収束させる0以上の境界。</param>
        /// <param name="outerDeadZone">このmagnitude以上を1へ収束させるinnerより大きく1以下の境界。</param>
        /// <param name="deadZone">成功時のimmutable設定。失敗時はdefault。</param>
        /// <param name="error">成功時None、失敗時InvalidConfiguration。</param>
        /// <returns>構成できた場合true。</returns>
        public static bool TryCreate(double innerDeadZone, double outerDeadZone, out InputRadialDeadZone deadZone, out InputRadialDeadZoneError error)
        {
            if (!IsFinite(innerDeadZone) || !IsFinite(outerDeadZone) || innerDeadZone < 0d || innerDeadZone >= outerDeadZone || outerDeadZone > 1d)
            {
                deadZone = default;
                error = InputRadialDeadZoneError.InvalidConfiguration;
                return false;
            }

            deadZone = new InputRadialDeadZone(innerDeadZone, outerDeadZone);
            error = InputRadialDeadZoneError.None;
            return true;
        }

        /// <summary>2D入力の方向を保ち、magnitudeをinnerからouterの間で0から1へ線形補正する。</summary>
        /// <param name="horizontal">補正する有限horizontal成分。</param>
        /// <param name="vertical">補正する有限vertical成分。</param>
        /// <returns>成功時は補正済み成分とmagnitude、失敗時は明示error。</returns>
        public InputRadialDeadZoneResult Process(double horizontal, double vertical)
        {
            if (!IsValid) return InputRadialDeadZoneResult.Failure(InputRadialDeadZoneError.InvalidConfiguration);
            if (!IsFinite(horizontal) || !IsFinite(vertical)) return InputRadialDeadZoneResult.Failure(InputRadialDeadZoneError.NonFiniteInput);

            var horizontalMagnitude = Math.Abs(horizontal);
            var verticalMagnitude = Math.Abs(vertical);
            var maximumMagnitude = Math.Max(horizontalMagnitude, verticalMagnitude);
            if (maximumMagnitude == 0d) return InputRadialDeadZoneResult.Success(0d, 0d, 0d);

            var scaledHorizontal = horizontal / maximumMagnitude;
            var scaledVertical = vertical / maximumMagnitude;
            var scaledMagnitude = Math.Sqrt(scaledHorizontal * scaledHorizontal + scaledVertical * scaledVertical);
            var directionHorizontal = scaledHorizontal / scaledMagnitude;
            var directionVertical = scaledVertical / scaledMagnitude;

            if (maximumMagnitude > OuterDeadZone / scaledMagnitude)
                return InputRadialDeadZoneResult.Success(directionHorizontal, directionVertical, 1d);

            var inputMagnitude = maximumMagnitude * scaledMagnitude;
            if (inputMagnitude <= InnerDeadZone) return InputRadialDeadZoneResult.Success(0d, 0d, 0d);
            if (inputMagnitude >= OuterDeadZone) return InputRadialDeadZoneResult.Success(directionHorizontal, directionVertical, 1d);

            var outputMagnitude = (inputMagnitude - InnerDeadZone) / (OuterDeadZone - InnerDeadZone);
            return InputRadialDeadZoneResult.Success(directionHorizontal * outputMagnitude, directionVertical * outputMagnitude, outputMagnitude);
        }

        /// <summary>inner・outer境界が同じかを返す。</summary>
        public bool Equals(InputRadialDeadZone other) => InnerDeadZone.Equals(other.InnerDeadZone) && OuterDeadZone.Equals(other.OuterDeadZone);

        /// <summary>指定objectが同じ設定かを返す。</summary>
        public override bool Equals(object obj) => obj is InputRadialDeadZone other && Equals(other);

        /// <summary>設定のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (InnerDeadZone.GetHashCode() * 397) ^ OuterDeadZone.GetHashCode();
            }
        }

        /// <summary>2つの設定が同じかを返す。</summary>
        public static bool operator ==(InputRadialDeadZone left, InputRadialDeadZone right) => left.Equals(right);

        /// <summary>2つの設定が異なるかを返す。</summary>
        public static bool operator !=(InputRadialDeadZone left, InputRadialDeadZone right) => !left.Equals(right);

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
