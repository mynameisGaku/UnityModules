namespace DebugMenu
{
    /// <summary>保存対象で値を持つ行の既定値復元結果。部分失敗を成功表示しないために使う。</summary>
    public readonly struct DebugMenuResetResult
    {
        public DebugMenuResetResult(int totalCount, int succeededCount, int failedCount)
        {
            TotalCount = totalCount;
            SucceededCount = succeededCount;
            FailedCount = failedCount;
        }

        public int TotalCount { get; }
        public int SucceededCount { get; }
        public int FailedCount { get; }
        public bool IsSuccess => FailedCount == 0;
    }
}
