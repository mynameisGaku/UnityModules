namespace SceneFlow
{
    /// <summary>1件のScene操作と対象を表す不変の要求。</summary>
    public readonly struct SceneFlowRequest
    {
        private SceneFlowRequest(SceneFlowOperation operation, SceneReference scene)
        {
            Operation = operation;
            Scene = scene;
        }

        /// <summary>実行するScene操作。</summary>
        public SceneFlowOperation Operation { get; }

        /// <summary>操作対象のScene。</summary>
        public SceneReference Scene { get; }

        /// <summary>現在のSceneを置き換える読込要求を作る。</summary>
        /// <param name="scene">読み込むScene。</param>
        /// <returns>Single読込要求。</returns>
        public static SceneFlowRequest LoadSingle(SceneReference scene) => new SceneFlowRequest(SceneFlowOperation.LoadSingle, scene);

        /// <summary>追加読込要求を作る。</summary>
        /// <param name="scene">追加読込するScene。</param>
        /// <returns>Additive読込要求。</returns>
        public static SceneFlowRequest LoadAdditive(SceneReference scene) => new SceneFlowRequest(SceneFlowOperation.LoadAdditive, scene);

        /// <summary>アンロード要求を作る。</summary>
        /// <param name="scene">アンロードするScene。</param>
        /// <returns>アンロード要求。</returns>
        public static SceneFlowRequest Unload(SceneReference scene) => new SceneFlowRequest(SceneFlowOperation.Unload, scene);

        /// <summary>有効Scene変更要求を作る。</summary>
        /// <param name="scene">有効にするScene。</param>
        /// <returns>有効Scene変更要求。</returns>
        public static SceneFlowRequest SetActive(SceneReference scene) => new SceneFlowRequest(SceneFlowOperation.SetActive, scene);

        /// <summary>ログ表示に使える要求内容を返す。</summary>
        /// <returns>操作名とScene完全パス。</returns>
        public override string ToString() => $"{Operation}: {Scene.Path}";
    }
}
