using System;

namespace ReplayTape
{
    /// <summary>1つのtick、command id、opaque payloadを持つ読取entry。</summary>
    public readonly struct ReplayTapeEntry : IEquatable<ReplayTapeEntry>
    {
        private readonly byte[] _payload;

        /// <summary>利用側が記録した整数tick。</summary>
        public ulong Tick { get; }

        /// <summary>利用側schemaが定義する0以外のcommand id。</summary>
        public uint CommandId { get; }

        /// <summary>payload byte数。</summary>
        public int PayloadByteCount => _payload?.Length ?? 0;

        /// <summary>readerから作られた有効entryか。</summary>
        public bool IsValid => CommandId != 0 && _payload != null;

        internal ReplayTapeEntry(ulong tick, uint commandId, byte[] payload)
        {
            Tick = tick;
            CommandId = commandId;
            _payload = payload;
        }

        /// <summary>payloadの独立copyを返す。無効値では空配列。</summary>
        /// <returns>callerが所有するpayload配列。</returns>
        public byte[] ToPayloadArray()
        {
            if (_payload == null) return Array.Empty<byte>();
            var copy = new byte[_payload.Length];
            Buffer.BlockCopy(_payload, 0, copy, 0, copy.Length);
            return copy;
        }

        /// <summary>payloadを呼出側の領域へcopyする。</summary>
        /// <param name="destination">payload以上の長さを持つcopy先。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>copyできた場合にtrue。</returns>
        public bool TryCopyPayload(Span<byte> destination, out ReplayTapeError error)
        {
            if (_payload == null)
            {
                error = ReplayTapeError.InvalidInput;
                return false;
            }

            if (destination.Length < _payload.Length)
            {
                error = ReplayTapeError.DestinationTooSmall;
                return false;
            }

            _payload.AsSpan().CopyTo(destination);
            error = ReplayTapeError.None;
            return true;
        }

        /// <summary>tick、command id、payloadが一致するかを返す。</summary>
        /// <param name="other">比較するentry。</param>
        /// <returns>内容が一致する場合にtrue。</returns>
        public bool Equals(ReplayTapeEntry other)
        {
            if (Tick != other.Tick || CommandId != other.CommandId) return false;
            if (_payload == null || other._payload == null) return _payload == other._payload;
            return _payload.AsSpan().SequenceEqual(other._payload);
        }

        /// <summary>指定objectが同じentryかを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じentryの場合にtrue。</returns>
        public override bool Equals(object obj) => obj is ReplayTapeEntry other && Equals(other);

        /// <summary>entry内容からhash codeを返す。</summary>
        /// <returns>内容に基づくhash code。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ((int)Tick * 397) ^ (int)(Tick >> 32) ^ (int)CommandId;
                if (_payload != null)
                {
                    for (var index = 0; index < _payload.Length; index++) hash = (hash * 31) + _payload[index];
                }

                return hash;
            }
        }

        /// <summary>2つのentryが一致するかを返す。</summary>
        /// <param name="left">左辺entry。</param>
        /// <param name="right">右辺entry。</param>
        /// <returns>一致する場合にtrue。</returns>
        public static bool operator ==(ReplayTapeEntry left, ReplayTapeEntry right) => left.Equals(right);

        /// <summary>2つのentryが異なるかを返す。</summary>
        /// <param name="left">左辺entry。</param>
        /// <param name="right">右辺entry。</param>
        /// <returns>異なる場合にtrue。</returns>
        public static bool operator !=(ReplayTapeEntry left, ReplayTapeEntry right) => !left.Equals(right);
    }
}
