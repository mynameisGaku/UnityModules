namespace GameplayInventory
{
    /// <summary>stack移送計画を構築できなかった理由です。</summary>
    public enum StackTransferError
    {
        /// <summary>失敗していません。</summary>
        None = 0,
        /// <summary>移送元配列がnullです。</summary>
        NullSources = 1,
        /// <summary>移送元件数が対応範囲外です。</summary>
        InvalidSourceCount = 2,
        /// <summary>移送先配列がnullです。</summary>
        NullDestinations = 3,
        /// <summary>移送先件数が対応範囲外です。</summary>
        InvalidDestinationCount = 4,
        /// <summary>要求unit数が対応範囲外です。</summary>
        InvalidRequestedUnits = 5,
        /// <summary>移送元IDが正ではありません。</summary>
        InvalidSourceIdentifier = 6,
        /// <summary>移送元IDが重複しています。</summary>
        DuplicateSourceIdentifier = 7,
        /// <summary>移送元unit数が対応範囲外です。</summary>
        InvalidSourceUnits = 8,
        /// <summary>移送先IDが正ではありません。</summary>
        InvalidDestinationIdentifier = 9,
        /// <summary>移送先IDが重複しています。</summary>
        DuplicateDestinationIdentifier = 10,
        /// <summary>移送先capacityが対応範囲外です。</summary>
        InvalidDestinationCapacity = 11,
        /// <summary>移送先の現在unit数が0からcapacityの範囲外です。</summary>
        InvalidDestinationUnits = 12
    }
}
