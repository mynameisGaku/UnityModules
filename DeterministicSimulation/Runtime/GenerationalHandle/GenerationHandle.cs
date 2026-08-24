using System;
using System.Globalization;

namespace GenerationalHandles
{
    /// <summary>slot番号とgenerationを組にして、再利用後の古い参照を区別するimmutable handle。</summary>
    [Serializable]
    public readonly struct GenerationHandle : IEquatable<GenerationHandle>, IComparable<GenerationHandle>
    {
        /// <summary>pool内で割り当てられた0開始のslot番号。</summary>
        public int Slot { get; }

        /// <summary>slotが再利用されるたびに増える1以上の世代番号。</summary>
        public uint Generation { get; }

        /// <summary>default値ではなく、構造上利用可能なhandleか。</summary>
        public bool IsValid => Slot >= 0 && Generation != 0;

        /// <summary>poolだけが新しいhandleを構築する。</summary>
        /// <param name="slot">0開始のslot番号。</param>
        /// <param name="generation">1以上の世代番号。</param>
        internal GenerationHandle(int slot, uint generation)
        {
            Slot = slot;
            Generation = generation;
        }

        /// <summary>slot、generationの順に比較する。</summary>
        /// <param name="other">比較するhandle。</param>
        /// <returns>並び順を表す値。</returns>
        public int CompareTo(GenerationHandle other)
        {
            var slotComparison = Slot.CompareTo(other.Slot);
            return slotComparison != 0 ? slotComparison : Generation.CompareTo(other.Generation);
        }

        /// <summary>slotとgenerationが同じかを返す。</summary>
        /// <param name="other">比較するhandle。</param>
        /// <returns>同じhandleならtrue。</returns>
        public bool Equals(GenerationHandle other)
        {
            return Slot == other.Slot && Generation == other.Generation;
        }

        /// <summary>指定objectが同じhandleかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じhandleならtrue。</returns>
        public override bool Equals(object obj)
        {
            return obj is GenerationHandle other && Equals(other);
        }

        /// <summary>slotとgenerationから安定したhash codeを返す。</summary>
        /// <returns>handleのhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Slot * 397) ^ (int)Generation;
            }
        }

        /// <summary>cultureに依存しない診断文字列を返す。</summary>
        /// <returns>無効ならInvalid、有効ならslotとgeneration。</returns>
        public override string ToString()
        {
            return IsValid
                ? string.Concat("Slot ", Slot.ToString(CultureInfo.InvariantCulture), " / Generation ", Generation.ToString(CultureInfo.InvariantCulture))
                : "Invalid";
        }

        /// <summary>2つのhandleが同じかを返す。</summary>
        public static bool operator ==(GenerationHandle left, GenerationHandle right) => left.Equals(right);

        /// <summary>2つのhandleが異なるかを返す。</summary>
        public static bool operator !=(GenerationHandle left, GenerationHandle right) => !left.Equals(right);
    }
}
