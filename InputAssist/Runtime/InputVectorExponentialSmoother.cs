using System;

namespace InputFiltering
{
    /// <summary>明示stepごとにtarget差の一定割合を適用する状態fulな純粋2D processor。</summary>
    public sealed class InputVectorExponentialSmoother
    {
        /// <summary>1回のProcessでtarget差へ掛ける有限の0より大きく1以下の割合。</summary>
        public double SmoothingFactor { get; }

        /// <summary>現在のhorizontal成分。</summary>
        public double CurrentHorizontal { get; private set; }

        /// <summary>現在のvertical成分。</summary>
        public double CurrentVertical { get; private set; }

        private InputVectorExponentialSmoother(double smoothingFactor, double initialHorizontal, double initialVertical)
        {
            SmoothingFactor = smoothingFactor;
            CurrentHorizontal = initialHorizontal;
            CurrentVertical = initialVertical;
        }

        /// <summary>smoothing factorと明示初期値を検証してprocessorを作成する。</summary>
        /// <param name="smoothingFactor">target差へ掛ける有限の0より大きく1以下の割合。</param>
        /// <param name="initialHorizontal">-1以上1以下の有限初期horizontal成分。</param>
        /// <param name="initialVertical">-1以上1以下の有限初期vertical成分。</param>
        /// <param name="smoother">成功時のprocessor。失敗時はnull。</param>
        /// <param name="error">成功時None、失敗時は構成または初期値error。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(double smoothingFactor, double initialHorizontal, double initialVertical, out InputVectorExponentialSmoother smoother, out InputVectorExponentialSmootherError error)
        {
            if (!IsFinite(smoothingFactor) || smoothingFactor <= 0d || smoothingFactor > 1d)
            {
                smoother = null;
                error = InputVectorExponentialSmootherError.InvalidConfiguration;
                return false;
            }

            if (!TryValidateInput(initialHorizontal, initialVertical, out error))
            {
                smoother = null;
                return false;
            }

            smoother = new InputVectorExponentialSmoother(smoothingFactor, initialHorizontal, initialVertical);
            error = InputVectorExponentialSmootherError.None;
            return true;
        }

        /// <summary>現在値へtarget差のSmoothingFactor倍を適用する。</summary>
        /// <param name="targetHorizontal">-1以上1以下の有限target horizontal成分。</param>
        /// <param name="targetVertical">-1以上1以下の有限target vertical成分。</param>
        /// <returns>成功時は更新後成分、実適用差分、残差、到達状態。失敗時は状態を変えず明示error。</returns>
        public InputVectorExponentialResult Process(double targetHorizontal, double targetVertical)
        {
            if (!TryValidateInput(targetHorizontal, targetVertical, out var error)) return InputVectorExponentialResult.Failure(error);

            var previousHorizontal = CurrentHorizontal;
            var previousVertical = CurrentVertical;
            var deltaHorizontal = targetHorizontal - previousHorizontal;
            var deltaVertical = targetVertical - previousVertical;
            if (deltaHorizontal == 0d && deltaVertical == 0d) return InputVectorExponentialResult.Success(CurrentHorizontal, CurrentVertical, 0d, 0d, true);

            if (SmoothingFactor == 1d)
            {
                CurrentHorizontal = targetHorizontal;
                CurrentVertical = targetVertical;
            }
            else
            {
                CurrentHorizontal = previousHorizontal + deltaHorizontal * SmoothingFactor;
                CurrentVertical = previousVertical + deltaVertical * SmoothingFactor;
            }

            var appliedHorizontal = CurrentHorizontal - previousHorizontal;
            var appliedVertical = CurrentVertical - previousVertical;
            var remainingHorizontal = targetHorizontal - CurrentHorizontal;
            var remainingVertical = targetVertical - CurrentVertical;
            var appliedMagnitude = CalculateMagnitude(appliedHorizontal, appliedVertical);
            var remainingMagnitude = CalculateMagnitude(remainingHorizontal, remainingVertical);
            return InputVectorExponentialResult.Success(CurrentHorizontal, CurrentVertical, appliedMagnitude, remainingMagnitude, remainingMagnitude == 0d);
        }

        /// <summary>検証済みの明示値へ現在状態を再構築する。</summary>
        /// <param name="horizontal">-1以上1以下の有限horizontal成分。</param>
        /// <param name="vertical">-1以上1以下の有限vertical成分。</param>
        /// <param name="error">成功時None、失敗時は入力error。</param>
        /// <returns>再構築できた場合true。失敗時は現在状態を変えない。</returns>
        public bool TryReset(double horizontal, double vertical, out InputVectorExponentialSmootherError error)
        {
            if (!TryValidateInput(horizontal, vertical, out error)) return false;
            CurrentHorizontal = horizontal;
            CurrentVertical = vertical;
            return true;
        }

        private static bool TryValidateInput(double horizontal, double vertical, out InputVectorExponentialSmootherError error)
        {
            if (!IsFinite(horizontal) || !IsFinite(vertical))
            {
                error = InputVectorExponentialSmootherError.NonFiniteInput;
                return false;
            }

            if (horizontal < -1d || horizontal > 1d || vertical < -1d || vertical > 1d)
            {
                error = InputVectorExponentialSmootherError.InputOutOfRange;
                return false;
            }

            error = InputVectorExponentialSmootherError.None;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static double CalculateMagnitude(double horizontal, double vertical)
        {
            var absoluteHorizontal = Math.Abs(horizontal);
            var absoluteVertical = Math.Abs(vertical);
            var maximum = Math.Max(absoluteHorizontal, absoluteVertical);
            if (maximum == 0d) return 0d;
            var minimumRatio = Math.Min(absoluteHorizontal, absoluteVertical) / maximum;
            return maximum * Math.Sqrt(1d + minimumRatio * minimumRatio);
        }
    }
}
