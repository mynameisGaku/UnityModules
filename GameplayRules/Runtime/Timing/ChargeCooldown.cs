namespace GameplayTiming
{
    /// <summary>明示simulation tickからcharge消費と逐次回復を決定論的に計算します。</summary>
    public static class ChargeCooldown
    {
        /// <summary>v1で保持できる最大charge数です。</summary>
        public const int MaximumChargeCount = 32;

        /// <summary>最大charge数と回復間隔からrulesを作成します。</summary>
        /// <param name="maximumCharges">1〜32の最大charge数です。</param>
        /// <param name="rechargeIntervalTicks">1 chargeの回復に必要な正のtick数です。</param>
        /// <param name="rules">成功時に作成したrulesです。</param>
        /// <param name="error">失敗理由です。</param>
        /// <returns>rulesを作成できた場合はtrueです。</returns>
        public static bool TryCreateRules(int maximumCharges, long rechargeIntervalTicks, out ChargeCooldownRules rules, out ChargeCooldownError error)
        {
            if (maximumCharges < 1 || maximumCharges > MaximumChargeCount) return Fail(out rules, out error, ChargeCooldownError.InvalidMaximumCharges);
            if (rechargeIntervalTicks <= 0) return Fail(out rules, out error, ChargeCooldownError.InvalidRechargeInterval);
            rules = new ChargeCooldownRules(maximumCharges, rechargeIntervalTicks);
            error = ChargeCooldownError.None;
            return true;
        }

        /// <summary>指定tickと初期charge数からcanonical stateを作成します。</summary>
        /// <param name="rules">適用する有効なrulesです。</param>
        /// <param name="currentTick">0以上の開始tickです。</param>
        /// <param name="initialCharges">0〜最大数の初期charge数です。</param>
        /// <param name="state">成功時に作成したstateです。</param>
        /// <param name="error">失敗理由です。</param>
        /// <returns>stateを作成できた場合はtrueです。</returns>
        public static bool TryCreateState(ChargeCooldownRules rules, long currentTick, int initialCharges, out ChargeCooldownState state, out ChargeCooldownError error)
        {
            if (!IsValid(rules)) return Fail(out state, out error, RulesError(rules));
            if (currentTick < 0) return Fail(out state, out error, ChargeCooldownError.InvalidTick);
            if (initialCharges < 0 || initialCharges > rules.MaximumCharges) return Fail(out state, out error, ChargeCooldownError.InvalidInitialCharges);
            var nextRechargeTick = 0L;
            if (initialCharges < rules.MaximumCharges && !TryAdd(currentTick, rules.RechargeIntervalTicks, out nextRechargeTick)) return Fail(out state, out error, ChargeCooldownError.TickOverflow);
            state = new ChargeCooldownState(initialCharges, currentTick, nextRechargeTick);
            error = ChargeCooldownError.None;
            return true;
        }

        /// <summary>保存済みfieldからrulesと整合するstateを復元します。</summary>
        /// <param name="rules">復元stateを検証するrulesです。</param>
        /// <param name="availableCharges">保存した利用可能charge数です。</param>
        /// <param name="lastEvaluatedTick">保存した最終評価tickです。</param>
        /// <param name="nextRechargeTick">保存した次回復tickです。</param>
        /// <param name="state">成功時に復元したstateです。</param>
        /// <param name="error">失敗理由です。</param>
        /// <returns>fieldがrulesと整合する場合はtrueです。</returns>
        public static bool TryRestoreState(ChargeCooldownRules rules, int availableCharges, long lastEvaluatedTick, long nextRechargeTick, out ChargeCooldownState state, out ChargeCooldownError error)
        {
            if (!IsValid(rules)) return Fail(out state, out error, RulesError(rules));
            var candidate = new ChargeCooldownState(availableCharges, lastEvaluatedTick, nextRechargeTick);
            if (!IsValid(candidate, rules)) return Fail(out state, out error, ChargeCooldownError.InvalidState);
            state = candidate;
            error = ChargeCooldownError.None;
            return true;
        }

        /// <summary>currentTickまでに成立した回復をまとめてstateへ反映します。</summary>
        /// <param name="state">操作前のstateです。</param>
        /// <param name="rules">適用するrulesです。</param>
        /// <param name="currentTick">state以上の評価tickです。</param>
        /// <param name="result">成功時の前後stateと回復数です。</param>
        /// <param name="error">失敗理由です。</param>
        /// <returns>回復を評価できた場合はtrueです。</returns>
        public static bool TryAdvance(ChargeCooldownState state, ChargeCooldownRules rules, long currentTick, out ChargeCooldownResult result, out ChargeCooldownError error)
        {
            if (!TryAdvanceCore(state, rules, currentTick, out var advanced, out var restored, out error))
            {
                result = default;
                return false;
            }

            result = new ChargeCooldownResult(state, advanced, restored, false);
            return true;
        }

        /// <summary>currentTickまで回復してから利用可能なchargeを1件だけ消費します。</summary>
        /// <param name="state">操作前のstateです。</param>
        /// <param name="rules">適用するrulesです。</param>
        /// <param name="currentTick">state以上の評価tickです。</param>
        /// <param name="result">成功時の前後state、回復数、消費成否です。</param>
        /// <param name="error">失敗理由です。</param>
        /// <returns>回復と消費可否を評価できた場合はtrueです。</returns>
        public static bool TrySpend(ChargeCooldownState state, ChargeCooldownRules rules, long currentTick, out ChargeCooldownResult result, out ChargeCooldownError error)
        {
            if (!TryAdvanceCore(state, rules, currentTick, out var advanced, out var restored, out error))
            {
                result = default;
                return false;
            }

            if (advanced.AvailableCharges == 0)
            {
                result = new ChargeCooldownResult(state, advanced, restored, false);
                return true;
            }

            var nextRechargeTick = advanced.NextRechargeTick;
            if (advanced.AvailableCharges == rules.MaximumCharges && !TryAdd(currentTick, rules.RechargeIntervalTicks, out nextRechargeTick))
            {
                result = default;
                error = ChargeCooldownError.TickOverflow;
                return false;
            }

            var spent = new ChargeCooldownState(advanced.AvailableCharges - 1, currentTick, nextRechargeTick);
            result = new ChargeCooldownResult(state, spent, restored, true);
            error = ChargeCooldownError.None;
            return true;
        }

        private static bool TryAdvanceCore(ChargeCooldownState state, ChargeCooldownRules rules, long currentTick, out ChargeCooldownState advanced, out int restored, out ChargeCooldownError error)
        {
            if (!IsValid(rules)) return Fail(out advanced, out restored, out error, RulesError(rules));
            if (currentTick < 0) return Fail(out advanced, out restored, out error, ChargeCooldownError.InvalidTick);
            if (!IsValid(state, rules)) return Fail(out advanced, out restored, out error, ChargeCooldownError.InvalidState);
            if (currentTick < state.LastEvaluatedTick) return Fail(out advanced, out restored, out error, ChargeCooldownError.TickMovedBackward);
            if (!state.IsRecharging || currentTick < state.NextRechargeTick)
            {
                advanced = new ChargeCooldownState(state.AvailableCharges, currentTick, state.NextRechargeTick);
                restored = 0;
                error = ChargeCooldownError.None;
                return true;
            }

            var due = 1L + ((currentTick - state.NextRechargeTick) / rules.RechargeIntervalTicks);
            var needed = rules.MaximumCharges - state.AvailableCharges;
            if (due >= needed)
            {
                advanced = new ChargeCooldownState(rules.MaximumCharges, currentTick, 0);
                restored = needed;
                error = ChargeCooldownError.None;
                return true;
            }

            if (!TryMultiply(due, rules.RechargeIntervalTicks, out var offset) || !TryAdd(state.NextRechargeTick, offset, out var next)) return Fail(out advanced, out restored, out error, ChargeCooldownError.TickOverflow);
            restored = (int)due;
            advanced = new ChargeCooldownState(state.AvailableCharges + restored, currentTick, next);
            error = ChargeCooldownError.None;
            return true;
        }

        private static bool IsValid(ChargeCooldownRules rules) => rules.MaximumCharges >= 1 && rules.MaximumCharges <= MaximumChargeCount && rules.RechargeIntervalTicks > 0;
        private static ChargeCooldownError RulesError(ChargeCooldownRules rules) => rules.MaximumCharges < 1 || rules.MaximumCharges > MaximumChargeCount ? ChargeCooldownError.InvalidMaximumCharges : ChargeCooldownError.InvalidRechargeInterval;
        private static bool IsValid(ChargeCooldownState state, ChargeCooldownRules rules)
        {
            if (state.AvailableCharges < 0 || state.AvailableCharges > rules.MaximumCharges || state.LastEvaluatedTick < 0 || state.NextRechargeTick < 0) return false;
            return state.AvailableCharges == rules.MaximumCharges ? state.NextRechargeTick == 0 : state.NextRechargeTick > state.LastEvaluatedTick;
        }

        private static bool TryAdd(long left, long right, out long value)
        {
            if (right > long.MaxValue - left) { value = default; return false; }
            value = left + right;
            return true;
        }

        private static bool TryMultiply(long left, long right, out long value)
        {
            if (left != 0 && right > long.MaxValue / left) { value = default; return false; }
            value = left * right;
            return true;
        }

        private static bool Fail<T>(out T value, out ChargeCooldownError error, ChargeCooldownError failure)
        {
            value = default;
            error = failure;
            return false;
        }

        private static bool Fail(out ChargeCooldownState state, out int restored, out ChargeCooldownError error, ChargeCooldownError failure)
        {
            state = default;
            restored = 0;
            error = failure;
            return false;
        }
    }
}
