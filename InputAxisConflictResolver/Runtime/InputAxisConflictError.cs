namespace InputAxisConflict
{
    /// <summary>Input Axis Conflict Resolverが要求を受理できなかった理由。</summary>
    public enum InputAxisConflictError
    {
        /// <summary>処理が成功した。</summary>
        None = 0,

        /// <summary>未定義の競合解決policyが指定された。</summary>
        InvalidPolicy = 1,

        /// <summary>入力tickが最後に受理したtickより前へ戻った。</summary>
        TickMovedBackward = 2
    }
}
