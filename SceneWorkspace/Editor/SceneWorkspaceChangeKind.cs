namespace SceneWorkspace.Editor
{
    /// <summary>現在構成と切り替え後の構成の間にある、一つの確定した差分を表します。</summary>
    public enum SceneWorkspaceChangeKind
    {
        /// <summary>シーンの順番と状態を変更しません。</summary>
        Keep,

        /// <summary>切り替え後の構成に新しいシーンを開きます。</summary>
        Open,

        /// <summary>切り替え後の構成に含まれないシーンを閉じます。</summary>
        Close,

        /// <summary>開いているシーンを読み込みます。</summary>
        Load,

        /// <summary>開いているシーンの読み込みを解除します。</summary>
        Unload,

        /// <summary>開いているシーンの順番を変えます。</summary>
        Reorder,

        /// <summary>シーンを使用中のシーンへ設定します。</summary>
        SetActive,

        /// <summary>シーンの使用中状態を解除します。</summary>
        ClearActive
    }
}
