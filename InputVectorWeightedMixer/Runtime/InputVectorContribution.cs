using System;

namespace InputMixing
{
    /// <summary>Weighted Mixerへ渡す有限2D成分と非負weightを表すimmutableな入力値。</summary>
    public readonly struct InputVectorContribution : IEquatable<InputVectorContribution>
    {
        /// <summary>-1以上1以下を期待するhorizontal成分。</summary>
        public double Horizontal { get; }

        /// <summary>-1以上1以下を期待するvertical成分。</summary>
        public double Vertical { get; }

        /// <summary>0以上1以下を期待する相対weight。</summary>
        public double Weight { get; }

        /// <summary>検証前の2D成分と相対weightを保持する。</summary>
        /// <param name="horizontal">horizontal成分。</param>
        /// <param name="vertical">vertical成分。</param>
        /// <param name="weight">相対weight。</param>
        public InputVectorContribution(double horizontal, double vertical, double weight)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Weight = weight;
        }

        /// <summary>全入力値が同じかを返す。</summary>
        /// <param name="other">比較する入力値。</param>
        /// <returns>同じ場合true。</returns>
        public bool Equals(InputVectorContribution other) => Horizontal.Equals(other.Horizontal) && Vertical.Equals(other.Vertical) && Weight.Equals(other.Weight);

        /// <summary>指定objectが同じ入力値かを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ場合true。</returns>
        public override bool Equals(object obj) => obj is InputVectorContribution other && Equals(other);

        /// <summary>全入力値からhash codeを返す。</summary>
        /// <returns>入力値に対応するhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Horizontal.GetHashCode();
                hash = (hash * 397) ^ Vertical.GetHashCode();
                return (hash * 397) ^ Weight.GetHashCode();
            }
        }

        /// <summary>2つの入力値が同じかを返す。</summary>
        /// <param name="left">左辺の入力値。</param>
        /// <param name="right">右辺の入力値。</param>
        /// <returns>同じ場合true。</returns>
        public static bool operator ==(InputVectorContribution left, InputVectorContribution right) => left.Equals(right);

        /// <summary>2つの入力値が異なるかを返す。</summary>
        /// <param name="left">左辺の入力値。</param>
        /// <param name="right">右辺の入力値。</param>
        /// <returns>異なる場合true。</returns>
        public static bool operator !=(InputVectorContribution left, InputVectorContribution right) => !left.Equals(right);
    }
}
