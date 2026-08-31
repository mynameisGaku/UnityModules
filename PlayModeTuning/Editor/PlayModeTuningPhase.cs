namespace PlayModeTuning.Editor
{
    /// <summary>利用者が明示的に進める調整作業の現在段階を表します。</summary>
    public enum PlayModeTuningPhase
    {
        /// <summary>有効な調整作業がない状態を示します。</summary>
        Idle,

        /// <summary>編集状態で対象と開始値を固定し、再生開始を待つ状態を示します。</summary>
        Armed,

        /// <summary>再生中となり、利用者が値を記録できる状態を示します。</summary>
        Capturable,

        /// <summary>再生中の値を明示的に記録し、再生終了を待つ状態を示します。</summary>
        Captured,

        /// <summary>記録後に編集状態へ戻り、差分確認を作成できる状態を示します。</summary>
        ReadyToPreview,

        /// <summary>一度だけ使える反映計画を作成し、利用者の確認を待つ状態を示します。</summary>
        Previewed,

        /// <summary>反映、差分なし、復元完了、または破棄によって作業を終了した状態を示します。</summary>
        Completed,

        /// <summary>識別情報や値の変化などにより、現在の作業を継続できない状態を示します。</summary>
        Stale
    }
}
