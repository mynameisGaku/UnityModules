namespace PlayModeTuning.Editor
{
    /// <summary>調整作業の開始、記録、確認、反映、復元で返す、範囲を限定した失敗理由を表します。</summary>
    public enum PlayModeTuningError
    {
        /// <summary>失敗がなく、処理が正常に完了したことを示します。</summary>
        None,

        /// <summary>選択が空、未解決、または必要な情報を満たさないことを示します。</summary>
        InvalidSelection,

        /// <summary>指定した調整作業が存在しないか、識別子が一致しないことを示します。</summary>
        InvalidSession,

        /// <summary>現在の段階では要求した操作を実行できないことを示します。</summary>
        WrongPhase,

        /// <summary>コンパイルやアセット更新などでエディターが処理中であることを示します。</summary>
        EditorBusy,

        /// <summary>再生中だけ実行できる操作を編集状態で要求したことを示します。</summary>
        PlayModeRequired,

        /// <summary>編集状態だけ実行できる操作を再生中または切り替え中に要求したことを示します。</summary>
        EditModeRequired,

        /// <summary>シーン再読み込みを無効にした再生設定には対応していないことを示します。</summary>
        DisableSceneReloadUnsupported,

        /// <summary>スクリプト再読み込みの設定または実際の再読み込み結果が開始時と一致しないことを示します。</summary>
        DomainReloadMismatch,

        /// <summary>選択対象が1作業あたり32コンポーネントの上限を超えたことを示します。</summary>
        TooManyComponents,

        /// <summary>選択項目が1作業あたり256項目の上限を超えたことを示します。</summary>
        TooManyProperties,

        /// <summary>開始値と記録値の合計が256 KiBの上限を超えたことを示します。</summary>
        PayloadTooLarge,

        /// <summary>選択した文字列がUTF-8で4096バイトの上限を超えたことを示します。</summary>
        StringTooLong,

        /// <summary>対象が保存済みシーン上の対応コンポーネントではないことを示します。</summary>
        UnsupportedTarget,

        /// <summary>項目が対応する最上位の値形式ではないことを示します。</summary>
        UnsupportedProperty,

        /// <summary>同じコンポーネントの同じ項目が複数回選択されたことを示します。</summary>
        DuplicateProperty,

        /// <summary>開始時に固定した対象または項目を現在のシーンから解決できないことを示します。</summary>
        TargetMissing,

        /// <summary>対象、型、項目などの識別情報が開始時または確認時から変わったことを示します。</summary>
        IdentityMismatch,

        /// <summary>NaNまたは無限大など、有限でない数値が含まれることを示します。</summary>
        NonFiniteValue,

        /// <summary>選択値の読み取りまたは符号化を完了できなかったことを示します。</summary>
        CaptureFailed,

        /// <summary>再生中の記録値が編集状態の開始値と同じで、反映する差分がないことを示します。</summary>
        NoChanges,

        /// <summary>開始後に編集状態の値や作業情報が変わり、作業を継続できないことを示します。</summary>
        StaleSession,

        /// <summary>確認後に対象状態や計画情報が変わり、計画を反映できないことを示します。</summary>
        StalePlan,

        /// <summary>一度だけ使える計画がすでに反映処理で消費されたことを示します。</summary>
        PlanAlreadyConsumed,

        /// <summary>別の反映処理が進行中で、新しい反映を開始できないことを示します。</summary>
        ApplyInProgress,

        /// <summary>選択値を対象へ書き込めなかったことを示します。</summary>
        ApplyFailed,

        /// <summary>反映後の選択値または未選択項目の再確認に失敗したことを示します。</summary>
        VerificationFailed,

        /// <summary>反映対象のシーンを変更済みとして確定できなかったことを示します。</summary>
        SceneDirtyFailed,

        /// <summary>反映失敗後に反映直前の状態へ完全に戻せなかったことを示します。</summary>
        RollbackFailed,

        /// <summary>保存された作業データが欠損、不整合、または未対応の形式であることを示します。</summary>
        SessionDataInvalid,

        /// <summary>調整作業データの読み書きまたは安全な消去を完了できなかったことを示します。</summary>
        SessionStorageFailed
    }
}
