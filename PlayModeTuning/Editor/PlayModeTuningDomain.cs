using System;

namespace PlayModeTuning.Editor
{
    /// <summary>対応する二つの再読込設定で、再生モード進入時の変化を確認するための印を提供します。</summary>
    internal static class PlayModeTuningDomain
    {
        internal static readonly string Token = Guid.NewGuid().ToString("N");
    }
}
