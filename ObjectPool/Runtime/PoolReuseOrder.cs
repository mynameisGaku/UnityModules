// SPDX-License-Identifier: MIT

namespace ObjectPool
{
    /// <summary>idleから取り出すinstanceを選ぶ順序。</summary>
    public enum PoolReuseOrder
    {
        /// <summary>最後に返却されたinstanceから再利用する。直前に使ったinstanceが温かいうちに使える既定動作。</summary>
        Lifo = 0,

        /// <summary>最も古く返却されたinstanceから再利用する。全instanceを均等に回したい場合に向く。</summary>
        Fifo = 1,
    }
}
