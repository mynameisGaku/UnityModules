// SPDX-License-Identifier: MIT

namespace Haptics
{
    /// <summary>呼出し側の意図を表す7種の標準振動要求。preset patternへ解決される。</summary>
    public enum HapticsIntent
    {
        /// <summary>UI選択など短い確認tick。</summary>
        SelectionTick = 0,

        /// <summary>軽い衝突や小さな成功。</summary>
        ImpactLight = 1,

        /// <summary>中程度の衝突。</summary>
        ImpactMedium = 2,

        /// <summary>強い衝突や重大な変化。</summary>
        ImpactHeavy = 3,

        /// <summary>操作成功の通知。</summary>
        NotificationSuccess = 4,

        /// <summary>注意喚起の通知。</summary>
        NotificationWarning = 5,

        /// <summary>失敗や危険の通知。</summary>
        NotificationError = 6,
    }
}
