namespace ScreenTransition
{
    /// <summary>画面遷移処理の現在段階。</summary>
    public enum ScreenTransitionPhase
    {
        /// <summary>要求を処理していない。</summary>
        Idle = 0,

        /// <summary>指定時間に沿って表示を変えている。</summary>
        Transitioning = 1,

        /// <summary>要求どおりの表示へ到達した。</summary>
        Completed = 2,

        /// <summary>要求を完了できなかった。</summary>
        Failed = 3,
    }
}
