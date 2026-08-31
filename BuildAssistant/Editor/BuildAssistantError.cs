namespace BuildAssistant.Editor
{
    /// <summary>ビルド実行アシスタントが報告する定義済みの失敗理由を表します。</summary>
    public enum BuildAssistantError
    {
        /// <summary>エラーは発生していません。</summary>
        None = 0,
        /// <summary>出力先の基準フォルダーが空、相対パス、ファイル、または2階層以上未作成のパスです。</summary>
        InvalidOutputRoot = 1,
        /// <summary>出力先が許可範囲を外れた、再解析ポイントを通った、またはUnity管理フォルダーと重なっています。</summary>
        UnsafeOutputPath = 2,
        /// <summary>選択した対象機種またはビルド設定が、対応するデスクトップ単体実行形式の範囲外です。</summary>
        UnsupportedBuildTarget = 3,
        /// <summary>エディターがコンパイル中、更新中、再生モードへの移行中などで、ビルドを開始できません。</summary>
        EditorBusy = 4,
        /// <summary>実際に使われるビルドプロファイルに、有効なシーンがありません。</summary>
        NoEnabledScenes = 5,
        /// <summary>計画を作成した後にビルド入力が変わりました。</summary>
        StalePlan = 6,
        /// <summary>ビルド実行アシスタントまたはUnityが、すでにプレイヤーをビルドしています。</summary>
        BuildAlreadyRunning = 7,
        /// <summary>計画した実行フォルダーまたは予約情報が、すでに存在します。</summary>
        OutputAlreadyExists = 8,
        /// <summary>出力フォルダーまたは永続化する実行状態を予約できませんでした。</summary>
        OutputReservationFailed = 9,
        /// <summary>Unityが、計画したプレイヤービルドを開始できなかったか、開始を拒否しました。</summary>
        BuildInvocationFailed = 10,
        /// <summary>開始したビルドについて、Unityからビルド報告が返されませんでした。</summary>
        BuildReportUnavailable = 11,
        /// <summary>返されたビルド報告を、Unityオブジェクトに依存しない結果へ変換できませんでした。</summary>
        ReportReadFailed = 12,
        /// <summary>ビルド履歴または明示的に書き出すJSONを永続保存できませんでした。</summary>
        HistoryWriteFailed = 13,
        /// <summary>独自のビルドプロファイルと、現在コンパイル済みの対象機種または種別が一致しません。</summary>
        BuildTargetMismatch = 14
    }
}
