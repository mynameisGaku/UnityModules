// SPDX-License-Identifier: MIT

namespace ObjectPool
{
    /// <summary>prefab instanceの取出し、返却、整理が失敗した理由。</summary>
    public enum PoolError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>instance引数がC#参照としてnullだった。</summary>
        NullInstance = 1,

        /// <summary>instanceがこのpoolの管理外。marker不在またはPoolId不一致。</summary>
        ForeignInstance = 2,

        /// <summary>instanceは既にidleへ返却済みで、二重返却だった。</summary>
        AlreadyReleased = 3,

        /// <summary>同時アクティブ数が上限に達し、新規生成が必要な取出しだった。idle再利用は対象外。</summary>
        ActiveLimitReached = 4,

        /// <summary>poolは既に破棄され、操作を受け付けられない。</summary>
        PoolDisposed = 5,

        /// <summary>管理下のinstanceが外部から破壊され、返却できなかった。</summary>
        InstanceExternallyDestroyed = 6,

        /// <summary>preload数が負だった。</summary>
        NegativePreloadCount = 7,

        /// <summary>trim数が負だった。</summary>
        NegativeTrimCount = 8,
    }
}
