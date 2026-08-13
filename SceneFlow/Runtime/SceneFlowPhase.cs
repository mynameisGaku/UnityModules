namespace SceneFlow
{
    /// <summary>SceneFlowServiceが公開する処理段階。</summary>
    public enum SceneFlowPhase
    {
        /// <summary>要求を受け付けられる。</summary>
        Idle = 0,

        /// <summary>要求と現在のScene状態を検査している。</summary>
        Validating = 1,

        /// <summary>Sceneを非同期読込している。</summary>
        Loading = 2,

        /// <summary>Unity操作完了後のScene状態を要求と照合している。</summary>
        Verifying = 3,

        /// <summary>Sceneを非同期アンロードしている。</summary>
        Unloading = 4,

        /// <summary>有効Sceneを変更している。</summary>
        SettingActive = 5,

        /// <summary>要求が成功したことを通知している。</summary>
        Completed = 6,

        /// <summary>要求が失敗したことを通知している。</summary>
        Failed = 7,
    }
}
