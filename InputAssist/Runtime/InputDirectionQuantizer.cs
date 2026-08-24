using System;

namespace InputDirectionQuantization
{
    /// <summary>有限2D analog入力をradial dead zone適用後の4-wayまたは8-way方向へ決定論的に変換するimmutable設定。</summary>
    public readonly struct InputDirectionQuantizer : IEquatable<InputDirectionQuantizer>
    {
        /// <summary>8-wayでcardinal方向とdiagonal方向を分けるtan 22.5度の固定境界。</summary>
        public const double DiagonalThreshold = 0.4142135623730951d;

        /// <summary>clamp後の長さがこの値以下ならneutralとする0以上1未満のradial境界。</summary>
        public double DeadZone { get; }

        /// <summary>4-wayまたは8-wayの方向分類方法。</summary>
        public InputDirectionMode Mode { get; }

        /// <summary>default値ではなく、構成範囲を満たすか。</summary>
        public bool IsValid => IsFinite(DeadZone) && DeadZone >= 0d && DeadZone < 1d && (Mode == InputDirectionMode.FourWay || Mode == InputDirectionMode.EightWay);

        private InputDirectionQuantizer(double deadZone, InputDirectionMode mode)
        {
            DeadZone = deadZone;
            Mode = mode;
        }

        /// <summary>radial dead zoneと方向modeを検証してquantizerを作成する。</summary>
        /// <param name="deadZone">clamp後の長さがneutralになる0以上1未満の境界。</param>
        /// <param name="mode">4-wayまたは8-wayの方向分類方法。</param>
        /// <param name="quantizer">成功時のimmutable設定。失敗時はdefault。</param>
        /// <param name="error">成功時None、失敗時InvalidConfiguration。</param>
        /// <returns>構成できた場合true。</returns>
        public static bool TryCreate(double deadZone, InputDirectionMode mode, out InputDirectionQuantizer quantizer, out InputDirectionQuantizationError error)
        {
            if (!IsFinite(deadZone) || deadZone < 0d || deadZone >= 1d || (mode != InputDirectionMode.FourWay && mode != InputDirectionMode.EightWay))
            {
                quantizer = default;
                error = InputDirectionQuantizationError.InvalidConfiguration;
                return false;
            }

            quantizer = new InputDirectionQuantizer(deadZone, mode);
            error = InputDirectionQuantizationError.None;
            return true;
        }

        /// <summary>各成分を[-1,1]へclampし、radial dead zone外を選択modeの方向へ量子化する。</summary>
        /// <param name="horizontal">量子化する有限horizontal値。</param>
        /// <param name="vertical">量子化する有限vertical値。</param>
        /// <returns>成功時は各成分-1、0、1の方向、失敗時は明示error。</returns>
        public InputDirectionQuantizationResult Quantize(double horizontal, double vertical)
        {
            if (!IsValid) return InputDirectionQuantizationResult.Failure(InputDirectionQuantizationError.InvalidConfiguration);
            if (!IsFinite(horizontal) || !IsFinite(vertical)) return InputDirectionQuantizationResult.Failure(InputDirectionQuantizationError.NonFiniteInput);

            var clampedHorizontal = Clamp(horizontal);
            var clampedVertical = Clamp(vertical);
            var magnitudeSquared = clampedHorizontal * clampedHorizontal + clampedVertical * clampedVertical;
            if (magnitudeSquared <= DeadZone * DeadZone) return InputDirectionQuantizationResult.Success(0, 0);

            var horizontalSign = clampedHorizontal < 0d ? -1 : 1;
            var verticalSign = clampedVertical < 0d ? -1 : 1;
            var horizontalMagnitude = Math.Abs(clampedHorizontal);
            var verticalMagnitude = Math.Abs(clampedVertical);
            if (Mode == InputDirectionMode.FourWay)
            {
                return horizontalMagnitude > verticalMagnitude
                    ? InputDirectionQuantizationResult.Success(horizontalSign, 0)
                    : InputDirectionQuantizationResult.Success(0, verticalSign);
            }

            if (verticalMagnitude <= horizontalMagnitude * DiagonalThreshold) return InputDirectionQuantizationResult.Success(horizontalSign, 0);
            if (horizontalMagnitude <= verticalMagnitude * DiagonalThreshold) return InputDirectionQuantizationResult.Success(0, verticalSign);
            return InputDirectionQuantizationResult.Success(horizontalSign, verticalSign);
        }

        /// <summary>dead zoneと方向modeが同じかを返す。</summary>
        public bool Equals(InputDirectionQuantizer other) => DeadZone.Equals(other.DeadZone) && Mode == other.Mode;

        /// <summary>指定objectが同じ設定かを返す。</summary>
        public override bool Equals(object obj) => obj is InputDirectionQuantizer other && Equals(other);

        /// <summary>設定のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (DeadZone.GetHashCode() * 397) ^ (int)Mode;
            }
        }

        /// <summary>2つの設定が同じかを返す。</summary>
        public static bool operator ==(InputDirectionQuantizer left, InputDirectionQuantizer right) => left.Equals(right);

        /// <summary>2つの設定が異なるかを返す。</summary>
        public static bool operator !=(InputDirectionQuantizer left, InputDirectionQuantizer right) => !left.Equals(right);

        private static double Clamp(double value) => value < -1d ? -1d : value > 1d ? 1d : value;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
