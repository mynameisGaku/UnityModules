namespace InputPressing
{
    /// <summary>1回の押下sampleを処理した後の状態と、そのsampleで発生した分類結果。</summary>
    public readonly struct InputPressStatus
    {
        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick { get; }

        /// <summary>holdへ分類するまでに必要な押下継続tick数。</summary>
        public ulong HoldThresholdTicks { get; }

        /// <summary>処理後に入力が押下中か。</summary>
        public bool IsPressed { get; }

        /// <summary>処理後の押下がhold判定済みか。</summary>
        public bool IsHolding { get; }

        /// <summary>今回のsampleで押下edgeが始まったか。</summary>
        public bool PressStarted { get; }

        /// <summary>今回のsampleでhold閾値へ初めて到達したか。</summary>
        public bool HoldStarted { get; }

        /// <summary>今回のsampleで解放edgeが発生したか。</summary>
        public bool Released { get; }

        /// <summary>今回の解放がhold閾値未満のtapへ分類されたか。</summary>
        public bool Tapped { get; }

        /// <summary>今回の解放がhold完了へ分類されたか。</summary>
        public bool HoldCompleted { get; }

        /// <summary>現在または直前の押下edgeから今回のtickまでの差。</summary>
        public ulong PressDurationTicks { get; }

        internal InputPressStatus(ulong currentTick, ulong holdThresholdTicks, bool isPressed, bool isHolding, bool pressStarted, bool holdStarted, bool released, bool tapped, bool holdCompleted, ulong pressDurationTicks)
        {
            CurrentTick = currentTick;
            HoldThresholdTicks = holdThresholdTicks;
            IsPressed = isPressed;
            IsHolding = isHolding;
            PressStarted = pressStarted;
            HoldStarted = holdStarted;
            Released = released;
            Tapped = tapped;
            HoldCompleted = holdCompleted;
            PressDurationTicks = pressDurationTicks;
        }
    }
}
