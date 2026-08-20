using System;

namespace InputThresholding
{
    /// <summary>sample後のpressed状態、edge、失敗理由を表すimmutable結果。</summary>
    public readonly struct InputThresholdClassificationResult : IEquatable<InputThresholdClassificationResult>
    {
        private readonly bool _hasValue;

        /// <summary>sample後のpressed状態。失敗時はclassifierが保持した状態。</summary>
        public bool IsPressed { get; }

        /// <summary>成功時の状態変化。変化無しまたは失敗時はNone。</summary>
        public InputThresholdEvent Event { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputThresholdClassificationError Error { get; }

        /// <summary>有効な分類結果を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputThresholdClassificationError.None;

        /// <summary>成功し、pressedまたはreleased edgeが発生したか。</summary>
        public bool StateChanged => Succeeded && Event != InputThresholdEvent.None;

        private InputThresholdClassificationResult(bool isPressed, InputThresholdEvent thresholdEvent, InputThresholdClassificationError error, bool hasValue)
        {
            IsPressed = isPressed;
            Event = thresholdEvent;
            Error = error;
            _hasValue = hasValue;
        }

        internal static InputThresholdClassificationResult Success(bool isPressed, InputThresholdEvent thresholdEvent) => new InputThresholdClassificationResult(isPressed, thresholdEvent, InputThresholdClassificationError.None, true);

        internal static InputThresholdClassificationResult Failure(bool isPressed, InputThresholdClassificationError error) => new InputThresholdClassificationResult(isPressed, InputThresholdEvent.None, error, true);

        /// <summary>状態、edge、error、結果保持状態が同じかを返す。</summary>
        public bool Equals(InputThresholdClassificationResult other) => IsPressed == other.IsPressed && Event == other.Event && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        public override bool Equals(object obj) => obj is InputThresholdClassificationResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = IsPressed ? 1 : 0;
                hash = (hash * 397) ^ (int)Event;
                hash = (hash * 397) ^ (int)Error;
                return (hash * 397) ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        public static bool operator ==(InputThresholdClassificationResult left, InputThresholdClassificationResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        public static bool operator !=(InputThresholdClassificationResult left, InputThresholdClassificationResult right) => !left.Equals(right);
    }
}
