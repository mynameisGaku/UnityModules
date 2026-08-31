namespace SceneWorkspace.Editor
{
    /// <summary>差分確認へ表示する一行分の変更と、変更前後の位置・状態を変更不能な値で保持します。</summary>
    public sealed class SceneWorkspaceChange
    {
        /// <summary>差分種別、シーンパス、変更前後の位置・状態から一つの変更を作成します。</summary>
        internal SceneWorkspaceChange(SceneWorkspaceChangeKind kind, string path, int beforeIndex, int afterIndex, bool beforeLoaded, bool afterLoaded, bool beforeActive, bool afterActive)
        {
            Kind = kind;
            Path = path ?? string.Empty;
            BeforeIndex = beforeIndex;
            AfterIndex = afterIndex;
            BeforeLoaded = beforeLoaded;
            AfterLoaded = afterLoaded;
            BeforeActive = beforeActive;
            AfterActive = afterActive;
        }

        /// <summary>開閉、読み込み、並べ替えなどの差分種別を返します。</summary>
        public SceneWorkspaceChangeKind Kind { get; }

        /// <summary>対象シーンのプロジェクト相対パスを返します。</summary>
        public string Path { get; }

        /// <summary>変更前の零始まり位置を返します。変更前に存在しない場合は負の値です。</summary>
        public int BeforeIndex { get; }

        /// <summary>変更後の零始まり位置を返します。変更後に存在しない場合は負の値です。</summary>
        public int AfterIndex { get; }

        /// <summary>変更前にシーンを読み込んでいたかを返します。</summary>
        public bool BeforeLoaded { get; }

        /// <summary>変更後にシーンを読み込むかを返します。</summary>
        public bool AfterLoaded { get; }

        /// <summary>変更前に使用中のシーンだったかを返します。</summary>
        public bool BeforeActive { get; }

        /// <summary>変更後に使用中のシーンとするかを返します。</summary>
        public bool AfterActive { get; }
    }
}
