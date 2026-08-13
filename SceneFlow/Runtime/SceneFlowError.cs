namespace SceneFlow
{
    /// <summary>Scene操作を開始または完了できなかった理由。</summary>
    public enum SceneFlowError
    {
        /// <summary>失敗していない。</summary>
        None = 0,

        /// <summary>要求の種類またはScene参照が不正。</summary>
        InvalidRequest = 1,

        /// <summary>Unityメインスレッド以外から呼ばれた。</summary>
        MainThreadRequired = 2,

        /// <summary>同じサービスが別の要求を処理中、またはcallbackを通知中。</summary>
        Busy = 3,

        /// <summary>現在のPlayerまたはBuild ProfileからSceneを読み込めない。</summary>
        SceneNotInBuild = 4,

        /// <summary>同じパスのSceneが既に読み込まれている。</summary>
        AlreadyLoaded = 5,

        /// <summary>対象Sceneが読み込まれていない。</summary>
        NotLoaded = 6,

        /// <summary>同じパスのSceneが複数あり、操作対象を一意に決められない。</summary>
        AmbiguousScene = 7,

        /// <summary>最後の読込済みSceneなのでアンロードできない。</summary>
        LastSceneCannotBeUnloaded = 8,

        /// <summary>有効Sceneなので先に別のSceneを有効にする必要がある。</summary>
        ActiveSceneCannotBeUnloaded = 9,

        /// <summary>読込済みSceneを有効にできなかった。</summary>
        ActivationFailed = 10,

        /// <summary>外部のSceneManager操作により完了後の状態が要求と一致しない。</summary>
        ExternalSceneChange = 11,

        /// <summary>UnityのScene操作が開始または完了できなかった。</summary>
        OperationFailed = 12,

        /// <summary>Play Mode終了またはアプリ終了で待機を終えた。</summary>
        ApplicationExiting = 13,
    }
}
