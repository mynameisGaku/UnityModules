using System;

namespace DebugMenu
{
    /// <summary>パスブラウザー内だけに置く一時的な候補行。</summary>
    internal sealed class DebugPathBrowserRow : DebugElement
    {
        private readonly Action _decide;

        /// <summary>表示内容と決定処理を指定して作る。</summary>
        internal DebugPathBrowserRow(string label, string subTitle, Action decide = null) : base(label, subTitle)
        {
            _decide = decide;
            IsExpandable = false;
            MarkerVisibility = DebugMarkerVisibility.Never;
        }

        /// <summary>一時候補なので設定へ保存しない。</summary>
        public override bool IsSaveable => false;

        /// <summary>一時候補なので全体検索の索引へ載せない。</summary>
        public override bool IsSearchable => false;

        /// <summary>移動または選択処理を実行する。エラー表示行では何もしない。</summary>
        public override void OnDecide() => _decide?.Invoke();
    }
}
