namespace SceneWorkspace.Editor
{
    /// <summary>構成取得、差分確認、切り替え、結果確認、復元の範囲内で発生する失敗理由を表します。</summary>
    public enum SceneWorkspaceError
    {
        /// <summary>処理は失敗していません。</summary>
        None,

        /// <summary>作業セット設定が未指定か参照できません。</summary>
        InvalidProfile,

        /// <summary>作業セット設定がAssetsフォルダー以下へ保存されていません。</summary>
        ProfileNotSaved,

        /// <summary>現在構成または作業セット設定にシーンがありません。</summary>
        NoScenes,

        /// <summary>シーン参照、アセット、またはGUIDを取得できません。</summary>
        MissingScene,

        /// <summary>同じシーンのGUIDまたはパスが重複しています。</summary>
        DuplicateScene,

        /// <summary>保存されていない無題のシーンがあります。</summary>
        UntitledScene,

        /// <summary>未保存の変更があるシーンを検出しました。</summary>
        DirtyScene,

        /// <summary>シーンがAssetsフォルダー以下の.unityアセットではありません。</summary>
        UnsupportedScenePath,

        /// <summary>切り替え後に読み込むシーンが一つもありません。</summary>
        NoLoadedScene,

        /// <summary>使用中にするシーンが一つではないか、読み込まない設定になっています。</summary>
        InvalidActiveScene,

        /// <summary>再生モード中または再生モードへ切り替え中です。</summary>
        PlayModeActive,

        /// <summary>コンパイル中またはアセット更新中です。</summary>
        EditorBusy,

        /// <summary>プレハブ編集画面が開いています。</summary>
        PrefabStageOpen,

        /// <summary>差分確認後に構成が変わったか、計画が登録されていません。</summary>
        StalePlan,

        /// <summary>同じ差分計画がすでに使用されています。</summary>
        PlanAlreadyConsumed,

        /// <summary>別の切り替えまたは復元処理が進行中です。</summary>
        ApplyInProgress,

        /// <summary>現在構成または作業セット設定を取得できませんでした。</summary>
        CaptureFailed,

        /// <summary>確認済みのシーン構成へ切り替えられませんでした。</summary>
        ApplyFailed,

        /// <summary>切り替え後の構成が確認済みの内容と一致しませんでした。</summary>
        VerificationFailed,

        /// <summary>元のシーン構成を復元または確認できませんでした。</summary>
        RollbackFailed
    }
}
