using UnityEditor;
using UnityEngine;

namespace PlayModeTuning.Editor
{
    /// <summary>プロジェクト資産を書き換えず、一つの調整作業をJSONとしてUnityのSessionStateへ保持します。</summary>
    internal sealed class UnityPlayModeTuningSessionStore : IPlayModeTuningSessionStore
    {
        private const string SessionKey = "PlayModeTuning.Session.v1";

        public PlayModeTuningPersistedSession Load()
        {
            var json = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return null;
            try
            {
                return JsonUtility.FromJson<PlayModeTuningPersistedSession>(json) ?? InvalidSessionData();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                return InvalidSessionData();
            }
        }

        public void Save(PlayModeTuningPersistedSession session)
        {
            if (session == null)
            {
                Clear();
                return;
            }
            SessionState.SetString(SessionKey, JsonUtility.ToJson(session));
        }

        public void Clear()
        {
            SessionState.EraseString(SessionKey);
        }

        private static PlayModeTuningPersistedSession InvalidSessionData()
        {
            return new PlayModeTuningPersistedSession { schemaVersion = 0 };
        }
    }
}
