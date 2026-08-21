namespace GameplayInventory
{
    /// <summary>入力順で構築した移送元減算と移送先加算の完全な計画です。</summary>
    public sealed class StackTransferPlan
    {
        private readonly StackTransferSourceLine[] _sourceLines;
        private readonly StackTransferDestinationLine[] _destinationLines;

        internal StackTransferPlan(int requestedUnits, int transferredUnits, long availableSourceUnits, long availableDestinationRoom, StackTransferSourceLine[] sourceLines, StackTransferDestinationLine[] destinationLines)
        {
            RequestedUnits = requestedUnits;
            TransferredUnits = transferredUnits;
            AvailableSourceUnits = availableSourceUnits;
            AvailableDestinationRoom = availableDestinationRoom;
            _sourceLines = (StackTransferSourceLine[])sourceLines.Clone();
            _destinationLines = (StackTransferDestinationLine[])destinationLines.Clone();
        }

        /// <summary>呼び出し側が移送を要求したunit数です。</summary>
        public int RequestedUnits { get; }

        /// <summary>両側の制約内で実際に移せるunit数です。</summary>
        public int TransferredUnits { get; }

        /// <summary>要求量から移送可能量を除いた未充足unit数です。</summary>
        public int UnfulfilledUnits => RequestedUnits - TransferredUnits;

        /// <summary>移送前に全sourceが保持するunit合計です。</summary>
        public long AvailableSourceUnits { get; }

        /// <summary>移送前に全destinationが受け入れられるunit合計です。</summary>
        public long AvailableDestinationRoom { get; }

        /// <summary>移送元明細数です。</summary>
        public int SourceLineCount => _sourceLines.Length;

        /// <summary>移送先明細数です。</summary>
        public int DestinationLineCount => _destinationLines.Length;

        /// <summary>指定した入力indexの移送元明細を取得します。</summary>
        /// <param name="index">取得する移送元入力indexです。</param>
        /// <param name="line">取得できた場合の移送元明細です。</param>
        /// <returns>indexが移送元明細の範囲内ならtrueです。</returns>
        public bool TryGetSourceLine(int index, out StackTransferSourceLine line)
        {
            if (index < 0 || index >= _sourceLines.Length)
            {
                line = default;
                return false;
            }

            line = _sourceLines[index];
            return true;
        }

        /// <summary>指定した入力indexの移送先明細を取得します。</summary>
        /// <param name="index">取得する移送先入力indexです。</param>
        /// <param name="line">取得できた場合の移送先明細です。</param>
        /// <returns>indexが移送先明細の範囲内ならtrueです。</returns>
        public bool TryGetDestinationLine(int index, out StackTransferDestinationLine line)
        {
            if (index < 0 || index >= _destinationLines.Length)
            {
                line = default;
                return false;
            }

            line = _destinationLines[index];
            return true;
        }
    }
}
