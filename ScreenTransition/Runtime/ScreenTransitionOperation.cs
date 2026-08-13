namespace ScreenTransition
{
    /// <summary>画面を覆う向きを表す操作の種類。</summary>
    public enum ScreenTransitionOperation
    {
        /// <summary>透明な状態から指定色で画面を覆う。</summary>
        Cover = 0,

        /// <summary>指定色で覆われた状態から画面を見せる。</summary>
        Reveal = 1,
    }
}
