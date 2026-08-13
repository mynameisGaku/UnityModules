namespace ScreenTransition
{
    /// <summary>画面遷移の成否と失敗理由を例外なしで返す。</summary>
    public readonly struct ScreenTransitionResult
    {
        private ScreenTransitionResult(bool isSuccess, ScreenTransitionError error, string message, ScreenTransitionRequest request)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message ?? string.Empty;
            Request = request;
        }

        /// <summary>要求どおりの不透明度へ到達した場合はtrue。</summary>
        public bool IsSuccess { get; }

        /// <summary>失敗理由。成功時はNone。</summary>
        public ScreenTransitionError Error { get; }

        /// <summary>ログや画面表示に使える短い説明。</summary>
        public string Message { get; }

        /// <summary>結果に対応する要求。</summary>
        public ScreenTransitionRequest Request { get; }

        /// <summary>要求どおりの不透明度へ到達した成功結果を作る。</summary>
        /// <param name="request">成功した要求。</param>
        /// <returns>成功を表す結果。</returns>
        internal static ScreenTransitionResult Success(ScreenTransitionRequest request) =>
            new ScreenTransitionResult(true, ScreenTransitionError.None, string.Empty, request);

        /// <summary>指定理由を持つ失敗結果を作る。</summary>
        /// <param name="request">失敗した要求。</param>
        /// <param name="error">失敗理由。</param>
        /// <param name="message">ログや画面表示に使える説明。</param>
        /// <returns>失敗を表す結果。</returns>
        internal static ScreenTransitionResult Failure(ScreenTransitionRequest request, ScreenTransitionError error, string message) =>
            new ScreenTransitionResult(false, error == ScreenTransitionError.None ? ScreenTransitionError.OperationFailed : error, message, request);
    }
}
