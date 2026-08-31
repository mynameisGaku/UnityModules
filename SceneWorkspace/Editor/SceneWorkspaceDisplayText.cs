namespace SceneWorkspace.Editor
{
    /// <summary>内部の列挙値と差分位置を、利用者向けの日本語表示へ変換します。</summary>
    internal static class SceneWorkspaceDisplayText
    {
        /// <summary>失敗理由を日本語へ変換し、未知の値は数値を保ったまま表示します。</summary>
        internal static string FormatError(SceneWorkspaceError error)
        {
            switch (error)
            {
                case SceneWorkspaceError.None:
                    return "問題はありません";
                case SceneWorkspaceError.InvalidProfile:
                    return "作業セット設定が選択されていません";
                case SceneWorkspaceError.ProfileNotSaved:
                    return "作業セット設定が保存されていません";
                case SceneWorkspaceError.NoScenes:
                    return "シーンが登録されていません";
                case SceneWorkspaceError.MissingScene:
                    return "参照できないシーンがあります";
                case SceneWorkspaceError.DuplicateScene:
                    return "同じシーンが重複しています";
                case SceneWorkspaceError.UntitledScene:
                    return "未保存の無題シーンがあります";
                case SceneWorkspaceError.DirtyScene:
                    return "未保存の変更があるシーンがあります";
                case SceneWorkspaceError.UnsupportedScenePath:
                    return "利用できない保存先のシーンがあります";
                case SceneWorkspaceError.NoLoadedScene:
                    return "読み込むシーンがありません";
                case SceneWorkspaceError.InvalidActiveScene:
                    return "使用中にするシーンの指定が不正です";
                case SceneWorkspaceError.PlayModeActive:
                    return "再生モード中です";
                case SceneWorkspaceError.EditorBusy:
                    return "エディターが処理中です";
                case SceneWorkspaceError.PrefabStageOpen:
                    return "プレハブ編集画面が開いています";
                case SceneWorkspaceError.StalePlan:
                    return "差分確認後に構成が変わりました";
                case SceneWorkspaceError.PlanAlreadyConsumed:
                    return "この差分確認結果は使用済みです";
                case SceneWorkspaceError.ApplyInProgress:
                    return "別の切り替え処理が進行中です";
                case SceneWorkspaceError.CaptureFailed:
                    return "シーン構成を取得できませんでした";
                case SceneWorkspaceError.ApplyFailed:
                    return "シーン構成を切り替えられませんでした";
                case SceneWorkspaceError.VerificationFailed:
                    return "切り替え後の構成を確認できませんでした";
                case SceneWorkspaceError.RollbackFailed:
                    return "元のシーン構成を復元できませんでした";
                default:
                    return "不明な問題（" + (int)error + "）";
            }
        }

        /// <summary>失敗理由と補足を、重複を避けた一つの日本語表示へまとめます。</summary>
        internal static string FormatOutcome(SceneWorkspaceError error, string message)
        {
            if (error == SceneWorkspaceError.None)
                return string.IsNullOrEmpty(message) ? FormatError(error) : message;
            return string.IsNullOrEmpty(message) ? FormatError(error) : FormatError(error) + "。" + message;
        }

        /// <summary>差分種別を日本語へ変換し、未知の値は数値を保ったまま表示します。</summary>
        internal static string FormatChangeKind(SceneWorkspaceChangeKind kind)
        {
            switch (kind)
            {
                case SceneWorkspaceChangeKind.Keep:
                    return "変更なし";
                case SceneWorkspaceChangeKind.Open:
                    return "開く";
                case SceneWorkspaceChangeKind.Close:
                    return "閉じる";
                case SceneWorkspaceChangeKind.Load:
                    return "読み込む";
                case SceneWorkspaceChangeKind.Unload:
                    return "読み込みを解除する";
                case SceneWorkspaceChangeKind.Reorder:
                    return "並べ替える";
                case SceneWorkspaceChangeKind.SetActive:
                    return "使用中にする";
                case SceneWorkspaceChangeKind.ClearActive:
                    return "使用中を解除する";
                default:
                    return "不明な変更（" + (int)kind + "）";
            }
        }

        /// <summary>差分行を、利用者向けの一始まりの位置で表示します。</summary>
        internal static string FormatChange(SceneWorkspaceChange change)
        {
            if (change == null)
                return "不明な変更";

            string position;
            if (change.BeforeIndex < 0 && change.AfterIndex >= 0)
                position = "変更後の位置：" + (change.AfterIndex + 1) + "番";
            else if (change.AfterIndex < 0 && change.BeforeIndex >= 0)
                position = "変更前の位置：" + (change.BeforeIndex + 1) + "番";
            else if (change.BeforeIndex >= 0 && change.AfterIndex >= 0)
                position = "変更前：" + (change.BeforeIndex + 1) + "番、変更後：" + (change.AfterIndex + 1) + "番";
            else
                position = "位置情報なし";

            return FormatChangeKind(change.Kind) + "　" + change.Path + "　（" + position + "）";
        }
    }
}
