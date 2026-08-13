namespace SaveSystem
{
    /// <summary>値を返さない保存操作の結果。</summary>
    public readonly struct SaveOperationResult
    {
        private SaveOperationResult(bool isSuccess, SaveError error, string message, SaveMetadata metadata)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message;
            Metadata = metadata;
        }

        /// <summary>操作が成功した場合は true。</summary>
        public bool IsSuccess { get; }

        /// <summary>失敗理由。成功時は <see cref="SaveError.None"/>。</summary>
        public SaveError Error { get; }

        /// <summary>ログや画面表示に使える短い説明。</summary>
        public string Message { get; }

        /// <summary>保存操作で確定した識別情報。該当しない操作では既定値。</summary>
        public SaveMetadata Metadata { get; }

        /// <summary>成功結果を作る。</summary>
        /// <param name="metadata">保存データの識別情報。</param>
        /// <param name="message">補足。省略可。</param>
        /// <returns>保存データの識別情報と補足を持つ成功結果。</returns>
        public static SaveOperationResult Success(SaveMetadata metadata = default, string message = null) =>
            new SaveOperationResult(true, SaveError.None, message ?? string.Empty, metadata);

        /// <summary>失敗結果を作る。</summary>
        /// <param name="error">失敗理由。None は指定できない。</param>
        /// <param name="message">失敗内容。</param>
        /// <returns>指定した失敗理由と内容を持つ失敗結果。</returns>
        public static SaveOperationResult Failure(SaveError error, string message) =>
            new SaveOperationResult(false, error == SaveError.None ? SaveError.StorageFailed : error, message ?? string.Empty, default);
    }
}
