using System;
using System.Collections.Generic;

namespace DebugMenu
{
    /// <summary>
    /// 子ページを親ページへ繋ぐ行。
    /// <para>
    /// この行は<b>自分では画面を切り替えない</b>。遷移先を指し示すだけにして、
    /// 実際の切り替えはメニュー本体が行う。こうしておくと行の層がメニュー全体を
    /// 知らずに済み、行だけを取り出してテストできる。
    /// </para>
    /// </summary>
    public sealed class DebugPageLink : DebugElement
    {
        /// <summary>遷移先と組み込み方を指定して作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="target">遷移先のページ。</param>
        /// <param name="mode">画面を切り替えるか、その場に展開するか。</param>
        public DebugPageLink(string label, DebugPage target, DebugAttachMode mode)
            : base(label, mode == DebugAttachMode.Page ? "▸" : null)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Mode = mode;

            if (mode == DebugAttachMode.Page)
            {
                // 画面ごと切り替える行は開閉しない。マーカーは「先がある」印として常に出す。
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }
        }

        /// <summary>遷移先のページ。</summary>
        public DebugPage Target { get; }

        /// <summary>画面を切り替えるか、その場に展開するか。</summary>
        public DebugAttachMode Mode { get; }

        /// <summary>
        /// その場に展開する場合は、遷移先ページの行をそのまま自分の子として見せる。
        /// 実体は 1 つなので、どちらから触っても同じ値を指す。
        /// </summary>
        public override IReadOnlyList<DebugElement> Children =>
            Mode == DebugAttachMode.Inline ? Target.Root.Children : base.Children;

        /// <summary>保存対象にしない。値を持たず、位置を示すだけの行のため。</summary>
        public override bool IsSaveable => false;
    }
}
