using System;

namespace GameplaySelection
{
    /// <summary>ID昇順で保持される1件のweighted entry。</summary>
    public readonly struct WeightedChoiceEntry : IEquatable<WeightedChoiceEntry>
    {
        internal WeightedChoiceEntry(int identifier, double weight)
        {
            Identifier = identifier;
            Weight = weight;
        }

        /// <summary>呼出側が割り当てた正のID。</summary>
        public int Identifier { get; }

        /// <summary>このentryへ割り当てた有限の正weight。</summary>
        public double Weight { get; }

        /// <inheritdoc />
        public bool Equals(WeightedChoiceEntry other) => Identifier == other.Identifier && Weight.Equals(other.Weight);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is WeightedChoiceEntry other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Identifier * 397) ^ Weight.GetHashCode();
            }
        }

        /// <summary>2つのentryが同じIDとweightを持つか判定する。</summary>
        public static bool operator ==(WeightedChoiceEntry left, WeightedChoiceEntry right) => left.Equals(right);

        /// <summary>2つのentryのIDまたはweightが異なるか判定する。</summary>
        public static bool operator !=(WeightedChoiceEntry left, WeightedChoiceEntry right) => !left.Equals(right);
    }
}
