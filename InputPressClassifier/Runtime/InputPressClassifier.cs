namespace InputPressing
{
    /// <summary>明示tickと押下状態からtapとholdを分類するEngine非依存state machine。</summary>
    public sealed class InputPressClassifier
    {
        private readonly ulong _holdThresholdTicks;
        private ulong _currentTick;
        private ulong _pressStartedTick;
        private bool _isPressed;
        private bool _isHolding;

        /// <summary>holdへ分類するまでに必要な押下継続tick数。</summary>
        public ulong HoldThresholdTicks => _holdThresholdTicks;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _currentTick;

        /// <summary>最後に受理したsampleで入力が押下中か。</summary>
        public bool IsPressed => _isPressed;

        /// <summary>現在の押下がhold判定済みか。</summary>
        public bool IsHolding => _isHolding;

        private InputPressClassifier(ulong holdThresholdTicks, ulong initialTick)
        {
            _holdThresholdTicks = holdThresholdTicks;
            _currentTick = initialTick;
            _pressStartedTick = initialTick;
        }

        /// <summary>正のhold閾値と初期tickからreleased状態のclassifierを作る。</summary>
        public static bool TryCreate(ulong holdThresholdTicks, ulong initialTick, out InputPressClassifier classifier, out InputPressError error)
        {
            if (holdThresholdTicks == 0)
            {
                classifier = null;
                error = InputPressError.InvalidHoldThreshold;
                return false;
            }

            classifier = new InputPressClassifier(holdThresholdTicks, initialTick);
            error = InputPressError.None;
            return true;
        }

        /// <summary>指定tickの押下状態を処理し、edgeとtap・hold分類を返す。</summary>
        public bool TrySample(ulong tick, bool isPressed, out InputPressStatus status, out InputPressError error)
        {
            if (tick < _currentTick)
            {
                status = Snapshot();
                error = InputPressError.TickMovedBackward;
                return false;
            }

            _currentTick = tick;
            var pressStarted = false;
            var holdStarted = false;
            var released = false;
            var tapped = false;
            var holdCompleted = false;
            var durationTicks = 0UL;

            if (isPressed)
            {
                if (!_isPressed)
                {
                    _isPressed = true;
                    _isHolding = false;
                    _pressStartedTick = tick;
                    pressStarted = true;
                }
                else
                {
                    durationTicks = tick - _pressStartedTick;
                    if (!_isHolding && durationTicks >= _holdThresholdTicks)
                    {
                        _isHolding = true;
                        holdStarted = true;
                    }
                }
            }
            else if (_isPressed)
            {
                durationTicks = tick - _pressStartedTick;
                released = true;
                if (_isHolding)
                {
                    holdCompleted = true;
                }
                else if (durationTicks >= _holdThresholdTicks)
                {
                    holdStarted = true;
                    holdCompleted = true;
                }
                else
                {
                    tapped = true;
                }

                _isPressed = false;
                _isHolding = false;
            }

            status = CreateStatus(pressStarted, holdStarted, released, tapped, holdCompleted, durationTicks);
            error = InputPressError.None;
            return true;
        }

        /// <summary>状態を進めず今回だけのedge・分類flagを持たない現在statusを返す。</summary>
        public InputPressStatus Snapshot()
        {
            var durationTicks = _isPressed ? _currentTick - _pressStartedTick : 0;
            return CreateStatus(false, false, false, false, false, durationTicks);
        }

        /// <summary>押下履歴を破棄し、指定tickのreleased状態へ初期化する。</summary>
        public void Reset(ulong tick)
        {
            _currentTick = tick;
            _pressStartedTick = tick;
            _isPressed = false;
            _isHolding = false;
        }

        private InputPressStatus CreateStatus(bool pressStarted, bool holdStarted, bool released, bool tapped, bool holdCompleted, ulong durationTicks) => new InputPressStatus(_currentTick, _holdThresholdTicks, _isPressed, _isHolding, pressStarted, holdStarted, released, tapped, holdCompleted, durationTicks);
    }
}
