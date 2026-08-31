namespace SceneWorkspace.Editor
{
    /// <summary>現在構成の取得、差分確認、確認済み計画の単回切り替えを提供するエディター専用の公開入口です。</summary>
    public static class SceneWorkspaceService
    {
        /// <summary>保存済みで未変更かつ有効な現在のシーン構成を、変更せずに取得します。</summary>
        /// <returns>取得したシーン構成と指紋値、またはシーン変更前に検出した失敗理由です。</returns>
        public static SceneWorkspaceCaptureResult CaptureCurrentSetup()
        {
            return CreateOperations().CaptureCurrentSetup();
        }

        /// <summary>シーンを開閉、読込、保存せず、変更不能で単回使用の差分計画を作成します。</summary>
        /// <param name="profile">切り替え後の順番、読込状態、使用中状態を保存した設定アセットです。</param>
        /// <returns>現在構成、目標構成、確定順の差分、指紋値を含む計画、または変更前の失敗理由です。</returns>
        public static SceneWorkspacePlan Preview(SceneWorkspaceProfile profile)
        {
            return CreateOperations().Preview(profile);
        }

        /// <summary>差分確認済みの計画を再検証して一度だけ適用し、結果確認または元構成の復元結果を返します。</summary>
        /// <param name="plan"><see cref="Preview"/>が返した同一オブジェクトの単回使用計画です。</param>
        /// <returns>切り替えの実行有無と成否、必要な場合は元構成への復元結果です。</returns>
        public static SceneWorkspaceApplyResult Apply(SceneWorkspacePlan plan)
        {
            return CreateOperations().Apply(plan);
        }

        /// <summary>実際のUnityエディターへ接続する一回分の処理群を作成します。</summary>
        private static SceneWorkspaceOperations CreateOperations()
        {
            return new SceneWorkspaceOperations(new UnitySceneWorkspaceGateway());
        }
    }
}
