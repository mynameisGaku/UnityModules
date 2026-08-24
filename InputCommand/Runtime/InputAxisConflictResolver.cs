namespace InputAxisConflict
{
    /// <summary>相反する2つの押下状態を明示tickとpolicyから解決するEngine非依存state machine。</summary>
    public sealed class InputAxisConflictResolver
    {
        private readonly InputAxisConflictPolicy _policy;
        private ulong _currentTick;
        private ulong _negativePressedTick;
        private ulong _positivePressedTick;
        private bool _negativePressed;
        private bool _positivePressed;
        private int _resolvedValue;

        /// <summary>競合時に使用する解決policy。</summary>
        public InputAxisConflictPolicy Policy => _policy;

        /// <summary>最後に受理したsimulation tick。</summary>
        public ulong CurrentTick => _currentTick;

        /// <summary>現在の解決値。</summary>
        public int ResolvedValue => _resolvedValue;

        private InputAxisConflictResolver(InputAxisConflictPolicy policy, ulong initialTick)
        {
            _policy = policy;
            _currentTick = initialTick;
            _negativePressedTick = initialTick;
            _positivePressedTick = initialTick;
        }

        /// <summary>定義済みpolicyと初期tickからneutral状態のresolverを作る。</summary>
        /// <param name="policy">競合時に使う定義済みpolicy。</param>
        /// <param name="initialTick">最初に受理済みとして扱うsimulation tick。</param>
        /// <param name="resolver">成功時に作成したresolver。</param>
        /// <param name="error">失敗理由。成功時はNone。</param>
        /// <returns>resolverを作成できた場合はtrue。</returns>
        public static bool TryCreate(InputAxisConflictPolicy policy, ulong initialTick, out InputAxisConflictResolver resolver, out InputAxisConflictError error)
        {
            if (!IsValidPolicy(policy))
            {
                resolver = null;
                error = InputAxisConflictError.InvalidPolicy;
                return false;
            }

            resolver = new InputAxisConflictResolver(policy, initialTick);
            error = InputAxisConflictError.None;
            return true;
        }

        /// <summary>指定tickのnegative・positive押下状態を処理して解決値を返す。</summary>
        /// <param name="tick">前回以上のsimulation tick。</param>
        /// <param name="negativePressed">negative側が押されている場合はtrue。</param>
        /// <param name="positivePressed">positive側が押されている場合はtrue。</param>
        /// <param name="status">受理後の入力edgeと解決結果。</param>
        /// <param name="error">失敗理由。成功時はNone。</param>
        /// <returns>sampleを受理できた場合はtrue。</returns>
        public bool TrySample(ulong tick, bool negativePressed, bool positivePressed, out InputAxisConflictStatus status, out InputAxisConflictError error)
        {
            if (tick < _currentTick)
            {
                status = Snapshot();
                error = InputAxisConflictError.TickMovedBackward;
                return false;
            }

            var negativePressedThisSample = negativePressed && !_negativePressed;
            var positivePressedThisSample = positivePressed && !_positivePressed;
            var negativeReleasedThisSample = !negativePressed && _negativePressed;
            var positiveReleasedThisSample = !positivePressed && _positivePressed;
            if (negativePressedThisSample) _negativePressedTick = tick;
            if (positivePressedThisSample) _positivePressedTick = tick;

            var previousValue = _resolvedValue;
            _currentTick = tick;
            _negativePressed = negativePressed;
            _positivePressed = positivePressed;
            _resolvedValue = Resolve();
            status = new InputAxisConflictStatus(_currentTick, _policy, _negativePressed, _positivePressed, negativePressedThisSample, positivePressedThisSample, negativeReleasedThisSample, positiveReleasedThisSample, _resolvedValue, previousValue != _resolvedValue);
            error = InputAxisConflictError.None;
            return true;
        }

        /// <summary>状態を進めず今回だけのedge・変更flagを持たない現在statusを返す。</summary>
        /// <returns>現在状態のimmutable snapshot。</returns>
        public InputAxisConflictStatus Snapshot() => new InputAxisConflictStatus(_currentTick, _policy, _negativePressed, _positivePressed, false, false, false, false, _resolvedValue, false);

        /// <summary>押下edge履歴を破棄し、指定tickのneutral状態へ初期化する。</summary>
        /// <param name="tick">reset後に受理済みとして扱うsimulation tick。</param>
        public void Reset(ulong tick)
        {
            _currentTick = tick;
            _negativePressedTick = tick;
            _positivePressedTick = tick;
            _negativePressed = false;
            _positivePressed = false;
            _resolvedValue = 0;
        }

        private static bool IsValidPolicy(InputAxisConflictPolicy policy) => policy == InputAxisConflictPolicy.Neutral || policy == InputAxisConflictPolicy.NegativeWins || policy == InputAxisConflictPolicy.PositiveWins || policy == InputAxisConflictPolicy.LastPressedWins;

        private int Resolve()
        {
            if (_negativePressed && !_positivePressed) return -1;
            if (!_negativePressed && _positivePressed) return 1;
            if (!_negativePressed) return 0;
            switch (_policy)
            {
                case InputAxisConflictPolicy.NegativeWins:
                    return -1;
                case InputAxisConflictPolicy.PositiveWins:
                    return 1;
                case InputAxisConflictPolicy.LastPressedWins:
                    if (_negativePressedTick > _positivePressedTick) return -1;
                    if (_positivePressedTick > _negativePressedTick) return 1;
                    return 0;
                default:
                    return 0;
            }
        }
    }
}
