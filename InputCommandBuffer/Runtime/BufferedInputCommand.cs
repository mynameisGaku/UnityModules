using System;

namespace InputBuffering
{
    /// <summary>bufferへ記録された1回のcommand入力を表すimmutable値。</summary>
    public readonly struct BufferedInputCommand : IEquatable<BufferedInputCommand>
    {
        /// <summary>利用側が定義した正のcommand id。</summary>
        public int CommandId { get; }

        /// <summary>commandを記録したsimulation tick。</summary>
        public ulong RecordedTick { get; }

        /// <summary>同じtick内を含む記録順序。</summary>
        public ulong Sequence { get; }

        internal BufferedInputCommand(int commandId, ulong recordedTick, ulong sequence)
        {
            CommandId = commandId;
            RecordedTick = recordedTick;
            Sequence = sequence;
        }

        /// <summary>全fieldが同じかを返す。</summary>
        /// <param name="other">比較するcommand。</param>
        /// <returns>同じ場合true。</returns>
        public bool Equals(BufferedInputCommand other) => CommandId == other.CommandId && RecordedTick == other.RecordedTick && Sequence == other.Sequence;

        /// <summary>指定objectが同じcommandかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ場合true。</returns>
        public override bool Equals(object obj) => obj is BufferedInputCommand other && Equals(other);

        /// <summary>全fieldからhash codeを返す。</summary>
        /// <returns>commandのhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CommandId;
                hash = (hash * 397) ^ RecordedTick.GetHashCode();
                return (hash * 397) ^ Sequence.GetHashCode();
            }
        }

        /// <summary>2つのcommandが同じかを返す。</summary>
        /// <param name="left">左辺。</param>
        /// <param name="right">右辺。</param>
        /// <returns>同じ場合true。</returns>
        public static bool operator ==(BufferedInputCommand left, BufferedInputCommand right) => left.Equals(right);

        /// <summary>2つのcommandが異なるかを返す。</summary>
        /// <param name="left">左辺。</param>
        /// <param name="right">右辺。</param>
        /// <returns>異なる場合true。</returns>
        public static bool operator !=(BufferedInputCommand left, BufferedInputCommand right) => !left.Equals(right);
    }
}
