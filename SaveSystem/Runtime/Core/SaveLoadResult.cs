namespace SaveSystem
{
    /// <summary>型付きの読み込み結果。</summary>
    /// <typeparam name="T">読み込む値の型。</typeparam>
    public readonly struct SaveLoadResult<T>
    {
        private SaveLoadResult(bool isSuccess, T value, SaveError error, string message, SaveMetadata metadata)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            Message = message;
            Metadata = metadata;
        }

        /// <summary>読み込みに成功した場合は true。</summary>
        public bool IsSuccess { get; }

        /// <summary>読み込んだ値。失敗時は既定値。</summary>
        public T Value { get; }

        /// <summary>失敗理由。成功時は <see cref="SaveError.None"/>。</summary>
        public SaveError Error { get; }

        /// <summary>ログや画面表示に使える短い説明。</summary>
        public string Message { get; }

        /// <summary>読み込んだ保存データの識別情報。</summary>
        public SaveMetadata Metadata { get; }

        /// <summary>成功結果を作る。</summary>
        /// <param name="value">読み込んだ値。</param>
        /// <param name="metadata">保存データの識別情報。</param>
        /// <param name="message">補足。省略可。</param>
        /// <returns>読み込んだ値と識別情報を持つ成功結果。</returns>
        public static SaveLoadResult<T> Success(T value, SaveMetadata metadata, string message = null) =>
            new SaveLoadResult<T>(true, value, SaveError.None, message ?? string.Empty, metadata);

        /// <summary>失敗結果を作る。</summary>
        /// <param name="error">失敗理由。None は指定できない。</param>
        /// <param name="message">失敗内容。</param>
        /// <returns>指定した失敗理由と内容を持つ失敗結果。</returns>
        public static SaveLoadResult<T> Failure(SaveError error, string message) =>
            new SaveLoadResult<T>(false, default, error == SaveError.None ? SaveError.StorageFailed : error, message ?? string.Empty, default);
    }
}
