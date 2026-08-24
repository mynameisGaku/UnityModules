using System;

namespace ReplayTape
{
    /// <summary>検証済みtapeを先頭から追加順に読む独立cursor。</summary>
    public sealed class ReplayTapeReader
    {
        private readonly byte[] _bytes;
        private readonly int _entryCount;
        private int _offset;

        internal ReplayTapeReader(byte[] bytes, int entryCount)
        {
            _bytes = bytes;
            _entryCount = entryCount;
            _offset = ReplayTapeFormat.HeaderByteCount;
        }

        /// <summary>tape全体のentry数。</summary>
        public int EntryCount => _entryCount;

        /// <summary>次に読むentryの0始まりindex。</summary>
        public int Position { get; private set; }

        /// <summary>未読entry数。</summary>
        public int RemainingCount => _entryCount - Position;

        /// <summary>次のentryを読み、cursorを1つ進める。</summary>
        /// <param name="entry">成功時の独立payload copyを持つentry。</param>
        /// <param name="error">末尾の場合の理由。</param>
        /// <returns>entryを読めた場合にtrue。</returns>
        public bool TryRead(out ReplayTapeEntry entry, out ReplayTapeError error)
        {
            entry = default;
            if (Position >= _entryCount)
            {
                error = ReplayTapeError.EndOfTape;
                return false;
            }

            var tick = ReplayTapeFormat.ReadUInt64(_bytes, _offset);
            var commandId = ReplayTapeFormat.ReadUInt32(_bytes, _offset + 8);
            var payloadByteCount = (int)ReplayTapeFormat.ReadUInt32(_bytes, _offset + 12);
            var payload = new byte[payloadByteCount];
            if (payloadByteCount > 0) Buffer.BlockCopy(_bytes, _offset + ReplayTapeFormat.RecordHeaderByteCount, payload, 0, payloadByteCount);
            _offset += ReplayTapeFormat.RecordHeaderByteCount + payloadByteCount;
            Position++;
            entry = new ReplayTapeEntry(tick, commandId, payload);
            error = ReplayTapeError.None;
            return true;
        }

        /// <summary>同じtapeの先頭entryへ戻す。</summary>
        public void Reset()
        {
            _offset = ReplayTapeFormat.HeaderByteCount;
            Position = 0;
        }
    }
}
