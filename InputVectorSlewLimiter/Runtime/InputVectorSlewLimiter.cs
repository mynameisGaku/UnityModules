using System;

namespace InputSmoothing
{
    /// <summary>明示stepごとの最大vector差で有限2D入力をtargetへ近づける状態fulな純粋processor。</summary>
    public sealed class InputVectorSlewLimiter
    {
        /// <summary>1回のProcessで適用できる最大vector差。有限かつ0より大きい。</summary>
        public double MaximumDeltaPerStep { get; }

        /// <summary>現在のhorizontal成分。</summary>
        public double CurrentHorizontal { get; private set; }

        /// <summary>現在のvertical成分。</summary>
        public double CurrentVertical { get; private set; }

        private InputVectorSlewLimiter(double maximumDeltaPerStep, double initialHorizontal, double initialVertical)
        {
            MaximumDeltaPerStep = maximumDeltaPerStep;
            CurrentHorizontal = initialHorizontal;
            CurrentVertical = initialVertical;
        }

        /// <summary>最大変化量と明示初期値を検証してlimiterを作成する。</summary>
        /// <param name="maximumDeltaPerStep">1回のProcessで適用する有限かつ0より大きい最大vector差。</param>
        /// <param name="initialHorizontal">-1以上1以下の有限初期horizontal成分。</param>
        /// <param name="initialVertical">-1以上1以下の有限初期vertical成分。</param>
        /// <param name="limiter">成功時のprocessor。失敗時はnull。</param>
        /// <param name="error">成功時None、失敗時は構成または初期値error。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(double maximumDeltaPerStep, double initialHorizontal, double initialVertical, out InputVectorSlewLimiter limiter, out InputVectorSlewLimiterError error)
        {
            if (!IsFinite(maximumDeltaPerStep) || maximumDeltaPerStep <= 0d)
            {
                limiter = null;
                error = InputVectorSlewLimiterError.InvalidConfiguration;
                return false;
            }

            if (!TryValidateInput(initialHorizontal, initialVertical, out error))
            {
                limiter = null;
                return false;
            }

            limiter = new InputVectorSlewLimiter(maximumDeltaPerStep, initialHorizontal, initialVertical);
            error = InputVectorSlewLimiterError.None;
            return true;
        }

        /// <summary>現在値からtargetへのvector差を最大変化量以内で適用する。</summary>
        /// <param name="targetHorizontal">-1以上1以下の有限target horizontal成分。</param>
        /// <param name="targetVertical">-1以上1以下の有限target vertical成分。</param>
        /// <returns>成功時は更新後成分、適用差分、到達状態。失敗時は状態を変えず明示error。</returns>
        public InputVectorSlewResult Process(double targetHorizontal, double targetVertical)
        {
            if (!TryValidateInput(targetHorizontal, targetVertical, out var error)) return InputVectorSlewResult.Failure(error);

            var deltaHorizontal = targetHorizontal - CurrentHorizontal;
            var deltaVertical = targetVertical - CurrentVertical;
            var deltaMagnitude = Math.Sqrt(deltaHorizontal * deltaHorizontal + deltaVertical * deltaVertical);
            if (deltaMagnitude <= MaximumDeltaPerStep)
            {
                CurrentHorizontal = targetHorizontal;
                CurrentVertical = targetVertical;
                return InputVectorSlewResult.Success(CurrentHorizontal, CurrentVertical, deltaMagnitude, true);
            }

            var scale = MaximumDeltaPerStep / deltaMagnitude;
            CurrentHorizontal += deltaHorizontal * scale;
            CurrentVertical += deltaVertical * scale;
            return InputVectorSlewResult.Success(CurrentHorizontal, CurrentVertical, MaximumDeltaPerStep, false);
        }

        /// <summary>検証済みの明示値へ現在状態を再構築する。</summary>
        /// <param name="horizontal">-1以上1以下の有限horizontal成分。</param>
        /// <param name="vertical">-1以上1以下の有限vertical成分。</param>
        /// <param name="error">成功時None、失敗時は入力error。</param>
        /// <returns>再構築できた場合true。失敗時は現在状態を変えない。</returns>
        public bool TryReset(double horizontal, double vertical, out InputVectorSlewLimiterError error)
        {
            if (!TryValidateInput(horizontal, vertical, out error)) return false;
            CurrentHorizontal = horizontal;
            CurrentVertical = vertical;
            return true;
        }

        private static bool TryValidateInput(double horizontal, double vertical, out InputVectorSlewLimiterError error)
        {
            if (!IsFinite(horizontal) || !IsFinite(vertical))
            {
                error = InputVectorSlewLimiterError.NonFiniteInput;
                return false;
            }

            if (horizontal < -1d || horizontal > 1d || vertical < -1d || vertical > 1d)
            {
                error = InputVectorSlewLimiterError.InputOutOfRange;
                return false;
            }

            error = InputVectorSlewLimiterError.None;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
