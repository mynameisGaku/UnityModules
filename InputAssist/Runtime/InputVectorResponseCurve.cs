using System;

namespace InputResponse
{
    /// <summary>単位円内の有限2D入力へ方向を保つmagnitude curveを適用するimmutable設定。</summary>
    public readonly struct InputVectorResponseCurve : IEquatable<InputVectorResponseCurve>
    {
        /// <summary>入力magnitudeへ適用するcurve。</summary>
        public InputVectorResponseMode Mode { get; }

        /// <summary>default値ではなく、定義済みmodeを保持するか。</summary>
        public bool IsValid => IsDefined(Mode);

        private InputVectorResponseCurve(InputVectorResponseMode mode)
        {
            Mode = mode;
        }

        /// <summary>modeを検証してresponse curve設定を作成する。</summary>
        /// <param name="mode">入力magnitudeへ適用する定義済みcurve。</param>
        /// <param name="curve">成功時のimmutable設定。失敗時はdefault。</param>
        /// <param name="error">成功時None、失敗時InvalidConfiguration。</param>
        /// <returns>構成できた場合true。</returns>
        public static bool TryCreate(InputVectorResponseMode mode, out InputVectorResponseCurve curve, out InputVectorResponseCurveError error)
        {
            if (!IsDefined(mode))
            {
                curve = default;
                error = InputVectorResponseCurveError.InvalidConfiguration;
                return false;
            }

            curve = new InputVectorResponseCurve(mode);
            error = InputVectorResponseCurveError.None;
            return true;
        }

        /// <summary>単位円内の2D入力の方向を保ち、magnitudeへ設定済みcurveを適用する。</summary>
        /// <param name="horizontal">処理する有限horizontal成分。</param>
        /// <param name="vertical">処理する有限vertical成分。</param>
        /// <returns>成功時は処理済み成分とmagnitude、失敗時は明示error。</returns>
        public InputVectorResponseResult Process(double horizontal, double vertical)
        {
            if (!IsValid) return InputVectorResponseResult.Failure(InputVectorResponseCurveError.InvalidConfiguration);
            if (!IsFinite(horizontal) || !IsFinite(vertical)) return InputVectorResponseResult.Failure(InputVectorResponseCurveError.NonFiniteInput);

            var maximumMagnitude = Math.Max(Math.Abs(horizontal), Math.Abs(vertical));
            if (maximumMagnitude == 0d) return InputVectorResponseResult.Success(0d, 0d, 0d);
            if (maximumMagnitude > 1d) return InputVectorResponseResult.Failure(InputVectorResponseCurveError.InputOutOfRange);

            var scaledHorizontal = horizontal / maximumMagnitude;
            var scaledVertical = vertical / maximumMagnitude;
            var scaledMagnitude = Math.Sqrt(scaledHorizontal * scaledHorizontal + scaledVertical * scaledVertical);
            var inputMagnitude = maximumMagnitude * scaledMagnitude;
            if (inputMagnitude > 1d) return InputVectorResponseResult.Failure(InputVectorResponseCurveError.InputOutOfRange);

            var outputMagnitude = Apply(inputMagnitude, Mode);
            var scale = outputMagnitude / inputMagnitude;
            return InputVectorResponseResult.Success(horizontal * scale, vertical * scale, outputMagnitude);
        }

        /// <summary>modeが同じかを返す。</summary>
        /// <param name="other">比較する設定。</param>
        /// <returns>同じmodeの場合true。</returns>
        public bool Equals(InputVectorResponseCurve other) => Mode == other.Mode;

        /// <summary>指定objectが同じ設定かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ設定の場合true。</returns>
        public override bool Equals(object obj) => obj is InputVectorResponseCurve other && Equals(other);

        /// <summary>設定のhash codeを返す。</summary>
        /// <returns>modeから求めたhash code。</returns>
        public override int GetHashCode() => (int)Mode;

        /// <summary>2つの設定が同じかを返す。</summary>
        /// <param name="left">左辺の設定。</param>
        /// <param name="right">右辺の設定。</param>
        /// <returns>同じ設定の場合true。</returns>
        public static bool operator ==(InputVectorResponseCurve left, InputVectorResponseCurve right) => left.Equals(right);

        /// <summary>2つの設定が異なるかを返す。</summary>
        /// <param name="left">左辺の設定。</param>
        /// <param name="right">右辺の設定。</param>
        /// <returns>異なる設定の場合true。</returns>
        public static bool operator !=(InputVectorResponseCurve left, InputVectorResponseCurve right) => !left.Equals(right);

        private static double Apply(double magnitude, InputVectorResponseMode mode)
        {
            switch (mode)
            {
                case InputVectorResponseMode.Linear:
                    return magnitude;
                case InputVectorResponseMode.Squared:
                    return magnitude * magnitude;
                case InputVectorResponseMode.Cubic:
                    return magnitude * magnitude * magnitude;
                case InputVectorResponseMode.SmoothStep:
                    return magnitude * magnitude * (3d - 2d * magnitude);
                default:
                    throw new InvalidOperationException("Undefined response curve mode reached the processing path.");
            }
        }

        private static bool IsDefined(InputVectorResponseMode mode) => mode >= InputVectorResponseMode.Linear && mode <= InputVectorResponseMode.SmoothStep;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
