namespace PlayModeTuning.Editor
{
    /// <summary>通常設定と実行領域の再読込を無効にした設定の両方で、一つの有界な調整作業を保持します。</summary>
    internal interface IPlayModeTuningSessionStore
    {
        PlayModeTuningPersistedSession Load();
        void Save(PlayModeTuningPersistedSession session);
        void Clear();
    }
}
