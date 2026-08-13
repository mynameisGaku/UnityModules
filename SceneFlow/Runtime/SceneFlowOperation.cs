namespace SceneFlow
{
    /// <summary>SceneFlowが直列に扱うScene操作の種類。</summary>
    public enum SceneFlowOperation
    {
        /// <summary>現在のSceneを置き換えて読み込む。</summary>
        LoadSingle = 0,

        /// <summary>現在のSceneを残して追加読込する。</summary>
        LoadAdditive = 1,

        /// <summary>読込済みSceneをアンロードする。</summary>
        Unload = 2,

        /// <summary>読込済みSceneを有効Sceneにする。</summary>
        SetActive = 3,
    }
}
