namespace GameplayEffects
{
    /// <summary>時限stackの再適用前後と上限適用状況を保持する不変結果です。</summary>
    public readonly struct TimedStackResolution
    {
        /// <summary>解決に使用した状態、方針、および変化情報をまとめます。</summary>
        public TimedStackResolution(TimedStackState previousState, TimedStackState incomingState, TimedStackState resultState, TimedStackPolicy policy, bool stackClamped, bool durationClamped)
        {
            PreviousState = previousState;
            IncomingState = incomingState;
            ResultState = resultState;
            Policy = policy;
            StackClamped = stackClamped;
            DurationClamped = durationClamped;
        }

        /// <summary>再適用前の状態を取得します。</summary>
        public TimedStackState PreviousState { get; }

        /// <summary>追加された状態を取得します。</summary>
        public TimedStackState IncomingState { get; }

        /// <summary>再適用後の状態を取得します。</summary>
        public TimedStackState ResultState { get; }

        /// <summary>解決に使用した方針を取得します。</summary>
        public TimedStackPolicy Policy { get; }

        /// <summary>再適用前が非activeだったかを取得します。</summary>
        public bool WasInactive => PreviousState.IsInactive;

        /// <summary>stack数が変化したかを取得します。</summary>
        public bool StackCountChanged => PreviousState.StackCount != ResultState.StackCount;

        /// <summary>残りtick数が変化したかを取得します。</summary>
        public bool DurationChanged => PreviousState.RemainingTicks != ResultState.RemainingTicks;

        /// <summary>stack数が方針の上限に収められたかを取得します。</summary>
        public bool StackClamped { get; }

        /// <summary>残りtick数が方針の上限に収められたかを取得します。</summary>
        public bool DurationClamped { get; }
    }
}
