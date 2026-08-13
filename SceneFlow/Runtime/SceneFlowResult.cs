namespace SceneFlow
{
    /// <summary>Scene操作の成否と失敗理由を例外なしで返す。</summary>
    public readonly struct SceneFlowResult
    {
        private SceneFlowResult(bool isSuccess, SceneFlowError error, string message, SceneFlowRequest request)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
            Request = request;
        }

        /// <summary>要求どおりのScene状態を確認できた場合はtrue。</summary>
        public bool IsSuccess { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public SceneFlowError Error { get; }

        /// <summary>ログや画面表示に使える短い説明。</summary>
        public string Message { get; }

        /// <summary>結果に対応する要求。</summary>
        public SceneFlowRequest Request { get; }

        /// <summary>完了後のScene状態を確認済みの成功結果を作る。</summary>
        /// <param name="request">成功した要求。</param>
        /// <param name="message">ログや画面表示に使える説明。</param>
        /// <returns>成功を表す結果。</returns>
        internal static SceneFlowResult Success(SceneFlowRequest request, string message = null) =>
            new SceneFlowResult(true, SceneFlowError.None, message, request);

        /// <summary>指定した理由を持つ失敗結果を作る。</summary>
        /// <param name="request">失敗した要求。</param>
        /// <param name="error">失敗理由。</param>
        /// <param name="message">ログや画面表示に使える説明。</param>
        /// <returns>失敗を表す結果。</returns>
        internal static SceneFlowResult Failure(SceneFlowRequest request, SceneFlowError error, string message) =>
            new SceneFlowResult(false, error == SceneFlowError.None ? SceneFlowError.OperationFailed : error, message, request);
    }
}
