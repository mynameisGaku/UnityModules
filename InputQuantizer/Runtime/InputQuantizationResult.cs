using System;

namespace InputQuantization
{
    /// <summary>成功値と失敗理由を同時に表すimmutableな量子化結果。</summary>
    public readonly struct InputQuantizationResult : IEquatable<InputQuantizationResult>
    {
        private readonly bool _hasValue;

        /// <summary>成功時の対称量子化値。失敗時は0。</summary>
        public short Value { get; }

        /// <summary>成功時None、失敗時は具体的な理由。</summary>
        public InputQuantizationError Error { get; }

        /// <summary>有効な成功値を保持するか。</summary>
        public bool Succeeded => _hasValue && Error == InputQuantizationError.None;

        private InputQuantizationResult(short value, InputQuantizationError error, bool hasValue)
        {
            Value = value;
            Error = error;
            _hasValue = hasValue;
        }

        /// <summary>成功結果を作成する。</summary>
        internal static InputQuantizationResult Success(short value) => new InputQuantizationResult(value, InputQuantizationError.None, true);

        /// <summary>失敗結果を作成する。</summary>
        internal static InputQuantizationResult Failure(InputQuantizationError error) => new InputQuantizationResult(0, error, false);

        /// <summary>値、error、成功状態が同じかを返す。</summary>
        public bool Equals(InputQuantizationResult other) => Value == other.Value && Error == other.Error && _hasValue == other._hasValue;

        /// <summary>指定objectが同じ結果かを返す。</summary>
        public override bool Equals(object obj) => obj is InputQuantizationResult other && Equals(other);

        /// <summary>結果のhash codeを返す。</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (((int)Value * 397) ^ (int)Error) * 397 ^ (_hasValue ? 1 : 0);
            }
        }

        /// <summary>2つの結果が同じかを返す。</summary>
        public static bool operator ==(InputQuantizationResult left, InputQuantizationResult right) => left.Equals(right);

        /// <summary>2つの結果が異なるかを返す。</summary>
        public static bool operator !=(InputQuantizationResult left, InputQuantizationResult right) => !left.Equals(right);
    }
}
