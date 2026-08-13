namespace ScreenTransition
{
    /// <summary>進捗から表示割合を求める変化曲線。</summary>
    public enum ScreenTransitionEasing
    {
        /// <summary>一定の速さで変化する。</summary>
        Linear = 0,

        /// <summary>ゆっくり始まり、終端へ向けて速くなる。</summary>
        EaseIn = 1,

        /// <summary>速く始まり、終端へ向けてゆっくりになる。</summary>
        EaseOut = 2,

        /// <summary>開始と終了を滑らかにつなぐ。</summary>
        EaseInOut = 3,
    }
}
