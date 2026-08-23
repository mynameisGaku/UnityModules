namespace PlayModeTuning.Editor
{
    /// <summary>Stores one bounded session across default and disabled Domain Reload configurations.</summary>
    internal interface IPlayModeTuningSessionStore
    {
        PlayModeTuningPersistedSession Load();
        void Save(PlayModeTuningPersistedSession session);
        void Clear();
    }
}
