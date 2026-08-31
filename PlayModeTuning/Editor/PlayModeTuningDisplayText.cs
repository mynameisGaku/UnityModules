namespace PlayModeTuning.Editor
{
    /// <summary>内部の状態値を利用者向けの日本語へ変換します。</summary>
    internal static class PlayModeTuningDisplayText
    {
        /// <summary>失敗理由を日本語へ変換し、未知の値では数値を残します。</summary>
        internal static string Error(PlayModeTuningError error)
        {
            switch (error)
            {
                case PlayModeTuningError.None: return "問題なし";
                case PlayModeTuningError.InvalidSelection: return "選択内容が無効です";
                case PlayModeTuningError.InvalidSession: return "調整作業が無効です";
                case PlayModeTuningError.WrongPhase: return "現在の段階では実行できません";
                case PlayModeTuningError.EditorBusy: return "Unityエディターが処理中です";
                case PlayModeTuningError.PlayModeRequired: return "再生中に実行してください";
                case PlayModeTuningError.EditModeRequired: return "編集状態で実行してください";
                case PlayModeTuningError.DisableSceneReloadUnsupported: return "シーン再読み込みを無効にした設定には対応していません";
                case PlayModeTuningError.DomainReloadMismatch: return "スクリプト再読み込みの状態が開始時と異なります";
                case PlayModeTuningError.TooManyComponents: return "対象コンポーネントが多すぎます";
                case PlayModeTuningError.TooManyProperties: return "対象項目が多すぎます";
                case PlayModeTuningError.PayloadTooLarge: return "記録データが大きすぎます";
                case PlayModeTuningError.StringTooLong: return "文字列が長すぎます";
                case PlayModeTuningError.UnsupportedTarget: return "対象コンポーネントには対応していません";
                case PlayModeTuningError.UnsupportedProperty: return "対象項目には対応していません";
                case PlayModeTuningError.DuplicateProperty: return "同じ対象項目が重複しています";
                case PlayModeTuningError.TargetMissing: return "対象が見つかりません";
                case PlayModeTuningError.IdentityMismatch: return "対象の識別情報が一致しません";
                case PlayModeTuningError.NonFiniteValue: return "有限でない数値は扱えません";
                case PlayModeTuningError.CaptureFailed: return "値を記録できませんでした";
                case PlayModeTuningError.NoChanges: return "変更がありません";
                case PlayModeTuningError.StaleSession: return "調整作業が古くなっています";
                case PlayModeTuningError.StalePlan: return "反映予定が古くなっています";
                case PlayModeTuningError.PlanAlreadyConsumed: return "反映予定はすでに使用済みです";
                case PlayModeTuningError.ApplyInProgress: return "別の反映処理が進行中です";
                case PlayModeTuningError.ApplyFailed: return "変更を反映できませんでした";
                case PlayModeTuningError.VerificationFailed: return "反映後の確認に失敗しました";
                case PlayModeTuningError.SceneDirtyFailed: return "シーンを変更済みにできませんでした";
                case PlayModeTuningError.RollbackFailed: return "元の値へ戻せませんでした";
                case PlayModeTuningError.SessionDataInvalid: return "保存された調整データが壊れています";
                case PlayModeTuningError.SessionStorageFailed: return "調整データを保存できませんでした";
                default: return "不明な失敗（" + (int)error + "）";
            }
        }

        /// <summary>作業段階を日本語へ変換し、未知の値では数値を残します。</summary>
        internal static string Phase(PlayModeTuningPhase phase)
        {
            switch (phase)
            {
                case PlayModeTuningPhase.Idle: return "未開始";
                case PlayModeTuningPhase.Armed: return "再生開始待ち";
                case PlayModeTuningPhase.Capturable: return "値を記録可能";
                case PlayModeTuningPhase.Captured: return "値を記録済み";
                case PlayModeTuningPhase.ReadyToPreview: return "差分確認待ち";
                case PlayModeTuningPhase.Previewed: return "差分確認済み";
                case PlayModeTuningPhase.Completed: return "完了";
                case PlayModeTuningPhase.Stale: return "無効";
                default: return "不明な段階（" + (int)phase + "）";
            }
        }

        /// <summary>値の種類を日本語へ変換し、未知の値では数値を残します。</summary>
        internal static string ValueKind(PlayModeTuningValueKind valueKind)
        {
            switch (valueKind)
            {
                case PlayModeTuningValueKind.Boolean: return "真偽値";
                case PlayModeTuningValueKind.SignedInteger: return "符号付き整数";
                case PlayModeTuningValueKind.UnsignedInteger: return "符号なし整数";
                case PlayModeTuningValueKind.Character: return "文字";
                case PlayModeTuningValueKind.Float: return "単精度小数";
                case PlayModeTuningValueKind.Double: return "倍精度小数";
                case PlayModeTuningValueKind.String: return "文字列";
                case PlayModeTuningValueKind.Enum: return "列挙値";
                case PlayModeTuningValueKind.LayerMask: return "レイヤーマスク";
                case PlayModeTuningValueKind.Color: return "色";
                case PlayModeTuningValueKind.Vector2: return "2次元ベクトル";
                case PlayModeTuningValueKind.Vector3: return "3次元ベクトル";
                case PlayModeTuningValueKind.Vector4: return "4次元ベクトル";
                case PlayModeTuningValueKind.Vector2Int: return "2次元整数ベクトル";
                case PlayModeTuningValueKind.Vector3Int: return "3次元整数ベクトル";
                case PlayModeTuningValueKind.Rect: return "長方形";
                case PlayModeTuningValueKind.RectInt: return "整数長方形";
                case PlayModeTuningValueKind.Bounds: return "境界範囲";
                case PlayModeTuningValueKind.BoundsInt: return "整数境界範囲";
                case PlayModeTuningValueKind.Quaternion: return "回転";
                default: return "不明な値の種類（" + (int)valueKind + "）";
            }
        }

        /// <summary>失敗理由と補足を、空文字を残さず一つの表示文へまとめます。</summary>
        internal static string Failure(PlayModeTuningError error, string message)
        {
            return string.IsNullOrWhiteSpace(message) ? Error(error) : Error(error) + "：" + message;
        }
    }
}
