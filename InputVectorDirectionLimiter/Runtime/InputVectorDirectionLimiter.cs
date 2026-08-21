using System;

namespace InputSmoothing
{
    /// <summary>target magnitudeを即時反映しながら方向回転だけを明示stepごとに制限する状態fulな純粋2D processor。</summary>
    public sealed class InputVectorDirectionLimiter
    {
        private const double AngularTolerance = 3.5527136788005009e-15d;

        /// <summary>1 stepで適用する有限の0以上PI以下の最大radian。</summary>
        public double MaximumTurnRadians { get; }

        /// <summary>現在のhorizontal成分。</summary>
        public double CurrentHorizontal { get; private set; }

        /// <summary>現在のvertical成分。</summary>
        public double CurrentVertical { get; private set; }

        private InputVectorDirectionLimiter(double maximumTurnRadians, double initialHorizontal, double initialVertical)
        {
            MaximumTurnRadians = maximumTurnRadians;
            CurrentHorizontal = initialHorizontal;
            CurrentVertical = initialVertical;
        }

        /// <summary>最大回転量と明示初期値を検証してprocessorを作成する。</summary>
        /// <param name="maximumTurnRadians">1 stepの有限な0以上PI以下の最大radian。</param>
        /// <param name="initialHorizontal">unit circle内の有限初期horizontal成分。</param>
        /// <param name="initialVertical">unit circle内の有限初期vertical成分。</param>
        /// <param name="limiter">成功時のprocessor。失敗時はnull。</param>
        /// <param name="error">成功時None、失敗時は構成または初期値error。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(double maximumTurnRadians, double initialHorizontal, double initialVertical, out InputVectorDirectionLimiter limiter, out InputVectorDirectionLimiterError error)
        {
            if (!IsFinite(maximumTurnRadians) || maximumTurnRadians < 0d || maximumTurnRadians > Math.PI)
            {
                limiter = null;
                error = InputVectorDirectionLimiterError.InvalidConfiguration;
                return false;
            }

            if (!TryValidateInput(initialHorizontal, initialVertical, out _, out error))
            {
                limiter = null;
                return false;
            }

            limiter = new InputVectorDirectionLimiter(maximumTurnRadians, initialHorizontal, initialVertical);
            error = InputVectorDirectionLimiterError.None;
            return true;
        }

        /// <summary>target magnitudeを反映し、現在方向からtarget方向への回転をMaximumTurnRadians以内に制限する。</summary>
        /// <param name="targetHorizontal">unit circle内の有限target horizontal成分。</param>
        /// <param name="targetVertical">unit circle内の有限target vertical成分。</param>
        /// <returns>成功時は更新後成分、target magnitude、実適用・残り回転量。失敗時は状態を変えず明示error。</returns>
        public InputVectorDirectionLimitResult Process(double targetHorizontal, double targetVertical)
        {
            if (!TryValidateInput(targetHorizontal, targetVertical, out var targetMagnitude, out var error)) return InputVectorDirectionLimitResult.Failure(error);

            var currentMagnitude = CalculateMagnitude(CurrentHorizontal, CurrentVertical);
            var hadPriorDirection = currentMagnitude != 0d;
            if (targetMagnitude == 0d)
            {
                CurrentHorizontal = 0d;
                CurrentVertical = 0d;
                return InputVectorDirectionLimitResult.Success(0d, 0d, 0d, 0d, 0d, hadPriorDirection, true, false);
            }

            if (!hadPriorDirection)
            {
                CurrentHorizontal = targetHorizontal;
                CurrentVertical = targetVertical;
                return InputVectorDirectionLimitResult.Success(CurrentHorizontal, CurrentVertical, targetMagnitude, 0d, 0d, false, true, false);
            }

            var currentUnitHorizontal = CurrentHorizontal / currentMagnitude;
            var currentUnitVertical = CurrentVertical / currentMagnitude;
            var targetUnitHorizontal = targetHorizontal / targetMagnitude;
            var targetUnitVertical = targetVertical / targetMagnitude;
            var dot = Clamp(currentUnitHorizontal * targetUnitHorizontal + currentUnitVertical * targetUnitVertical, -1d, 1d);
            var requiredTurn = Math.Acos(dot);
            if (requiredTurn <= MaximumTurnRadians + AngularTolerance)
            {
                CurrentHorizontal = targetHorizontal;
                CurrentVertical = targetVertical;
                return InputVectorDirectionLimitResult.Success(CurrentHorizontal, CurrentVertical, targetMagnitude, requiredTurn, 0d, true, true, requiredTurn > MaximumTurnRadians);
            }

            var cross = currentUnitHorizontal * targetUnitVertical - currentUnitVertical * targetUnitHorizontal;
            var signedSine = (cross < 0d ? -1d : 1d) * Math.Sin(MaximumTurnRadians);
            var cosine = Math.Cos(MaximumTurnRadians);
            var nextHorizontal = (currentUnitHorizontal * cosine - currentUnitVertical * signedSine) * targetMagnitude;
            var nextVertical = (currentUnitHorizontal * signedSine + currentUnitVertical * cosine) * targetMagnitude;
            var wasNumericallyClamped = NormalizeNumericalOverflow(targetMagnitude, ref nextHorizontal, ref nextVertical);
            CurrentHorizontal = nextHorizontal;
            CurrentVertical = nextVertical;
            return InputVectorDirectionLimitResult.Success(CurrentHorizontal, CurrentVertical, targetMagnitude, MaximumTurnRadians, requiredTurn - MaximumTurnRadians, true, false, wasNumericallyClamped);
        }

        /// <summary>検証済みの明示値へ現在状態を再構築する。</summary>
        /// <param name="horizontal">unit circle内の有限horizontal成分。</param>
        /// <param name="vertical">unit circle内の有限vertical成分。</param>
        /// <param name="error">成功時None、失敗時は入力error。</param>
        /// <returns>再構築できた場合true。失敗時は現在状態を変えない。</returns>
        public bool TryReset(double horizontal, double vertical, out InputVectorDirectionLimiterError error)
        {
            if (!TryValidateInput(horizontal, vertical, out _, out error)) return false;
            CurrentHorizontal = horizontal;
            CurrentVertical = vertical;
            return true;
        }

        private static bool TryValidateInput(double horizontal, double vertical, out double magnitude, out InputVectorDirectionLimiterError error)
        {
            magnitude = 0d;
            if (!IsFinite(horizontal) || !IsFinite(vertical))
            {
                error = InputVectorDirectionLimiterError.NonFiniteInput;
                return false;
            }

            if (horizontal < -1d || horizontal > 1d || vertical < -1d || vertical > 1d)
            {
                error = InputVectorDirectionLimiterError.InputOutOfRange;
                return false;
            }

            magnitude = CalculateMagnitude(horizontal, vertical);
            if (magnitude > 1d)
            {
                error = InputVectorDirectionLimiterError.InputOutsideUnitCircle;
                return false;
            }

            error = InputVectorDirectionLimiterError.None;
            return true;
        }

        private static bool NormalizeNumericalOverflow(double targetMagnitude, ref double horizontal, ref double vertical)
        {
            var wasClamped = false;
            var magnitude = CalculateMagnitude(horizontal, vertical);
            if (magnitude > targetMagnitude && magnitude != 0d)
            {
                var scale = targetMagnitude / magnitude;
                horizontal *= scale;
                vertical *= scale;
                wasClamped = true;
            }

            var clampedHorizontal = Clamp(horizontal, -1d, 1d);
            var clampedVertical = Clamp(vertical, -1d, 1d);
            if (clampedHorizontal != horizontal || clampedVertical != vertical) wasClamped = true;
            horizontal = clampedHorizontal;
            vertical = clampedVertical;
            return wasClamped;
        }

        private static double CalculateMagnitude(double horizontal, double vertical)
        {
            var absoluteHorizontal = Math.Abs(horizontal);
            var absoluteVertical = Math.Abs(vertical);
            var maximum = Math.Max(absoluteHorizontal, absoluteVertical);
            if (maximum == 0d) return 0d;
            var minimumRatio = Math.Min(absoluteHorizontal, absoluteVertical) / maximum;
            return maximum * Math.Sqrt(1d + minimumRatio * minimumRatio);
        }

        private static double Clamp(double value, double minimum, double maximum) => value < minimum ? minimum : value > maximum ? maximum : value;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
