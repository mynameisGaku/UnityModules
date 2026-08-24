namespace InputThresholding
{
    /// <summary>有限scalar sampleを2つのthresholdでpressed状態とedgeへ決定論的に分類するmutable状態。</summary>
    public struct InputThresholdClassifier
    {
        /// <summary>pressed中にこの値以下ならReleased edgeとする0以上1未満の境界。</summary>
        public double ReleaseThreshold { get; }

        /// <summary>released中にこの値以上ならPressed edgeとする0超1以下の境界。</summary>
        public double PressThreshold { get; }

        /// <summary>最後に成功したsampleまたはReset後のpressed状態。</summary>
        public bool IsPressed { get; private set; }

        /// <summary>default値ではなく、threshold順序と範囲を満たすか。</summary>
        public bool IsValid => IsFinite(ReleaseThreshold) && IsFinite(PressThreshold) && ReleaseThreshold >= 0d && ReleaseThreshold < PressThreshold && PressThreshold <= 1d;

        private InputThresholdClassifier(double releaseThreshold, double pressThreshold, bool initialIsPressed)
        {
            ReleaseThreshold = releaseThreshold;
            PressThreshold = pressThreshold;
            IsPressed = initialIsPressed;
        }

        /// <summary>release・press thresholdと初期pressed状態を検証してclassifierを作成する。</summary>
        /// <param name="releaseThreshold">pressed中にreleaseする0以上1未満のinclusive境界。</param>
        /// <param name="pressThreshold">released中にpressするreleaseThreshold超1以下のinclusive境界。</param>
        /// <param name="initialIsPressed">再構築する初期pressed状態。</param>
        /// <param name="classifier">成功時の状態。失敗時はdefault。</param>
        /// <param name="error">成功時None、失敗時InvalidConfiguration。</param>
        /// <returns>構成できた場合true。</returns>
        public static bool TryCreate(double releaseThreshold, double pressThreshold, bool initialIsPressed, out InputThresholdClassifier classifier, out InputThresholdClassificationError error)
        {
            if (!IsFinite(releaseThreshold) || !IsFinite(pressThreshold) || releaseThreshold < 0d || releaseThreshold >= pressThreshold || pressThreshold > 1d)
            {
                classifier = default;
                error = InputThresholdClassificationError.InvalidConfiguration;
                return false;
            }

            classifier = new InputThresholdClassifier(releaseThreshold, pressThreshold, initialIsPressed);
            error = InputThresholdClassificationError.None;
            return true;
        }

        /// <summary>有限sampleを[0,1]へclampし、現在状態に対応するinclusive thresholdで分類する。</summary>
        /// <param name="value">分類する有限scalar sample。</param>
        /// <returns>sample後の状態、発生edge、明示error。</returns>
        public InputThresholdClassificationResult Sample(double value)
        {
            if (!IsValid) return InputThresholdClassificationResult.Failure(IsPressed, InputThresholdClassificationError.InvalidConfiguration);
            if (!IsFinite(value)) return InputThresholdClassificationResult.Failure(IsPressed, InputThresholdClassificationError.NonFiniteInput);

            var normalized = Clamp01(value);
            if (!IsPressed && normalized >= PressThreshold)
            {
                IsPressed = true;
                return InputThresholdClassificationResult.Success(true, InputThresholdEvent.Pressed);
            }

            if (IsPressed && normalized <= ReleaseThreshold)
            {
                IsPressed = false;
                return InputThresholdClassificationResult.Success(false, InputThresholdEvent.Released);
            }

            return InputThresholdClassificationResult.Success(IsPressed, InputThresholdEvent.None);
        }

        /// <summary>thresholdを保ったままpressed状態を明示値へ再構築する。</summary>
        /// <param name="isPressed">再構築するpressed状態。</param>
        /// <returns>成功時None、default値ならInvalidConfiguration。</returns>
        public InputThresholdClassificationError Reset(bool isPressed)
        {
            if (!IsValid) return InputThresholdClassificationError.InvalidConfiguration;
            IsPressed = isPressed;
            return InputThresholdClassificationError.None;
        }

        private static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
