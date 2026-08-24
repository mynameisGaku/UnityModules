using System;

namespace GameplayStats
{
    /// <summary>callerが割り当てたID、適用stage、有限値を持つimmutableなmodifier snapshot。</summary>
    public readonly struct StatModifier : IEquatable<StatModifier>
    {
        /// <summary>stack内で一意な正のID。</summary>
        public long Id { get; }

        /// <summary>値を適用するstage。</summary>
        public StatModifierKind Kind { get; }

        /// <summary>stageへ適用する有限値。</summary>
        public double Value { get; }

        internal StatModifier(long id, StatModifierKind kind, double value)
        {
            Id = id;
            Kind = kind;
            Value = value;
        }

        /// <summary>ID、kind、値が同じかを返す。</summary>
        /// <param name="other">比較するmodifier。</param>
        /// <returns>同じmodifierの場合true。</returns>
        public bool Equals(StatModifier other) => Id == other.Id && Kind == other.Kind && Value.Equals(other.Value);

        /// <summary>指定objectが同じmodifierかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じmodifierの場合true。</returns>
        public override bool Equals(object obj) => obj is StatModifier other && Equals(other);

        /// <summary>modifierのhash codeを返す。</summary>
        /// <returns>ID、kind、値から求めたhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Id.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                return (hash * 397) ^ Value.GetHashCode();
            }
        }

        /// <summary>2つのmodifierが同じかを返す。</summary>
        /// <param name="left">左辺のmodifier。</param>
        /// <param name="right">右辺のmodifier。</param>
        /// <returns>同じmodifierの場合true。</returns>
        public static bool operator ==(StatModifier left, StatModifier right) => left.Equals(right);

        /// <summary>2つのmodifierが異なるかを返す。</summary>
        /// <param name="left">左辺のmodifier。</param>
        /// <param name="right">右辺のmodifier。</param>
        /// <returns>異なるmodifierの場合true。</returns>
        public static bool operator !=(StatModifier left, StatModifier right) => !left.Equals(right);
    }
}
