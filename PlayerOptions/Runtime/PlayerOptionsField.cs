// SPDX-License-Identifier: MIT

using System;

namespace PlayerOptions
{
    /// <summary>player option操作が書込、rollback失敗、または結果不明にしたfield。</summary>
    [Flags]
    public enum PlayerOptionsField
    {
        /// <summary>対象fieldがない。</summary>
        None = 0,

        /// <summary>画面表示設定。</summary>
        Display = 1 << 0,

        /// <summary>target frame rate。</summary>
        TargetFrameRate = 1 << 1,

        /// <summary>master volume。</summary>
        MasterVolume = 1 << 2,

        /// <summary>品質level。</summary>
        Quality = 1 << 3,
    }
}
