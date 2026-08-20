namespace DeterministicRandom
{
    /// <summary>乱数streamを復元または範囲指定できなかった理由。</summary>
    public enum DeterministicRandomError
    {
        /// <summary>失敗していない。</summary>
        None = 0,
        /// <summary>algorithm versionまたは256-bit状態が不正。</summary>
        InvalidState = 1,
        /// <summary>上端が0、または整数範囲の下端が上端以上。</summary>
        InvalidRange = 2
    }
}
