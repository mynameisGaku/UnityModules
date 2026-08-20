using System;

namespace InputQuantization
{
    /// <summary>有限1軸入力をdead zone適用後の対称short値へ決定論的に変換するimmutable設定。</summary>
    public readonly struct AxisQuantizer : IEquatable<AxisQuantizer>
    {
        /// <summary>片側へ指定できる最大段階数。</summary>
        public const int MaximumStepsPerDirection = short.MaxValue;

        /// <summary>絶対値がこの値以下なら0とする0以上1未満の境界。</summary>
        public double DeadZone { get; }

        /// <summary>正負それぞれへ割り当てる1以上32767以下の段階数。</summary>
        public int StepsPerDirection { get; }

        /// <summary>default値ではなく、構成範囲を満たすか。</summary>
        public bool IsValid => IsFinite(DeadZone) && DeadZone >= 0d && DeadZone < 1d && StepsPerDirection >= 1 && StepsPerDirection <= MaximumStepsPerDirection;

        private AxisQuantizer(double deadZone, int stepsPerDirection)
        {
            DeadZone = deadZone;
            StepsPerDirection = stepsPerDirection;
        }

        /// <summary>dead zoneと片側段階数を検証してquantizerを作成する。</summary>
        /// <param name="deadZone">絶対値が0になる0以上1未満の境界。</param>
        /// <param name="stepsPerDirection">正負それぞれの1以上32767以下の段階数。</param>
        /// <param name="quantizer">成功時のimmutable設定。失敗時はdefault。</param>
        /// <param name="error">成功時None、失敗時InvalidConfiguration。</param>
        /// <returns>構成できた場合true。</returns>
        public static bool TryCreate(double deadZone, int stepsPerDirection, out AxisQuantizer quantizer, out InputQuantizationError error)
        {
            if (!IsFinite(deadZone) || deadZone < 0d || deadZone >= 1d || stepsPerDirection < 1 || stepsPerDirection > MaximumStepsPerDirection)
            {
                quantizer = default;
                error = InputQuantizationError.InvalidConfiguration;
                return false;
            }

            quantizer = new AxisQuantizer(deadZone, stepsPerDirection);
            error = InputQuantizationError.None;
            return true;
        }

        /// <summary>入力を[-1,1]へclampし、dead zone外を線形remapして最も近い整数へ量子化する。</summary>
        /// <param name="input">量子化する有限1軸値。</param>
        /// <returns>成功時short値、失敗時は明示error。</returns>
        public InputQuantizationResult Quantize(double input)
        {
            if (!IsValid) return InputQuantizationResult.Failure(InputQuantizationError.InvalidConfiguration);
            if (!IsFinite(input)) return InputQuantizationResult.Failure(InputQuantizationError.NonFiniteInput);

            var sign = input < 0d ? -1 : 1;
            var magnitude = Math.Min(Math.Abs(input), 1d);
            if (magnitude <= DeadZone) return InputQuantizationResult.Success(0);

            var normalized = (magnitude - DeadZone) / (1d - DeadZone);
            var roundedMagnitude = (int)Math.Round(normalized * StepsPerDirection, MidpointRounding.AwayFromZero);
            if (roundedMagnitude > StepsPerDirection) roundedMagnitude = StepsPerDirection;
            return InputQuantizationResult.Success((short)(sign * roundedMagnitude));
        }

        /// <summary>dead zoneと段階数が同じかを返す。</summary>
        public bool Equals(AxisQuantizer other) => DeadZone.Equals(other.DeadZone) && StepsPerDirection == other.StepsPerDirection;

        /// <summary>指定objectが同じ設定かを返す。</summary>
        public override bool Equals(object obj) => obj is AxisQuantizer other && Equals(other);

        /// <summary>設定のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (DeadZone.GetHashCode() * 397) ^ StepsPerDirection;
            }
        }

        /// <summary>2つの設定が同じかを返す。</summary>
        public static bool operator ==(AxisQuantizer left, AxisQuantizer right) => left.Equals(right);

        /// <summary>2つの設定が異なるかを返す。</summary>
        public static bool operator !=(AxisQuantizer left, AxisQuantizer right) => !left.Equals(right);

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
