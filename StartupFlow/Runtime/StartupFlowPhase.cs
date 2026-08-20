namespace StartupFlow
{
    /// <summary>StartupFlowServiceが現在処理している段階。</summary>
    public enum StartupFlowPhase
    {
        /// <summary>要求を処理していない。</summary>
        Idle = 0,
        /// <summary>step一覧を受理し、実行開始を通知している。</summary>
        Validating = 1,
        /// <summary>1件のstepを実行している。</summary>
        Running = 2,
        /// <summary>すべてのstepが成功した。</summary>
        Completed = 3,
        /// <summary>検証後の実行が失敗または中止で確定した。</summary>
        Failed = 4
    }
}
