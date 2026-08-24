using System;

namespace GenerationalHandles
{
    /// <summary>handle poolの操作を完了できなかった理由。</summary>
    public enum GenerationHandleError
    {
        /// <summary>操作が成功した。</summary>
        None = 0,

        /// <summary>利用可能なslotが残っていない。</summary>
        CapacityReached = 1,

        /// <summary>default値またはpool範囲外のhandleが渡された。</summary>
        InvalidHandle = 2,

        /// <summary>解放済み、または別generationへ進んだhandleが渡された。</summary>
        StaleHandle = 3
    }
}
