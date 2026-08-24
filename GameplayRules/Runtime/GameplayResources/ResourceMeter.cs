using System;

namespace GameplayResources
{
    /// <summary>immutable capacityと現在値を所有し、回復・部分消費・全量必須消費を明示結果で処理する純粋resource processor。</summary>
    public sealed class ResourceMeter
    {
        /// <summary>作成後に変化しない0より大きい有限capacity。</summary>
        public double Capacity { get; }

        /// <summary>現在の0以上capacity以下の値。</summary>
        public double Current { get; private set; }

        /// <summary>現在値をcapacityで0以上1以下へ正規化した値。</summary>
        public double Normalized => Current / Capacity;

        /// <summary>現在値が0か。</summary>
        public bool IsEmpty => Current == 0d;

        /// <summary>現在値がcapacityと同じか。</summary>
        public bool IsFull => Current == Capacity;

        private ResourceMeter(double capacity, double initialCurrent)
        {
            Capacity = capacity;
            Current = initialCurrent;
        }

        /// <summary>capacityと初期現在値を検証してmeterを作成する。</summary>
        /// <param name="capacity">0より大きい有限capacity。</param>
        /// <param name="initialCurrent">0以上capacity以下の有限初期値。</param>
        /// <param name="meter">成功時のmeter。失敗時はnull。</param>
        /// <param name="error">成功時None、失敗時は構成error。</param>
        /// <returns>作成できた場合true。</returns>
        public static bool TryCreate(double capacity, double initialCurrent, out ResourceMeter meter, out ResourceMeterError error)
        {
            if (!IsFinite(capacity) || capacity <= 0d)
            {
                meter = null;
                error = ResourceMeterError.InvalidCapacity;
                return false;
            }

            if (!TryValidateValue(initialCurrent, capacity, out error))
            {
                meter = null;
                return false;
            }

            meter = new ResourceMeter(capacity, initialCurrent);
            error = ResourceMeterError.None;
            return true;
        }

        /// <summary>非負amountをcapacityまで回復する。</summary>
        /// <param name="amount">回復を要求する有限の非負amount。</param>
        /// <returns>成功時は前後値、要求・実適用・未適用delta。失敗時はstateを変えず明示error。</returns>
        public ResourceChangeResult Restore(double amount)
        {
            if (!TryValidateAmount(amount, out var error)) return ResourceChangeResult.Failure(error);
            var previous = Current;
            var available = Capacity - previous;
            var applied = Math.Min(amount, available);
            Current = applied == available ? Capacity : previous + applied;
            return ResourceChangeResult.Success(previous, Current, Capacity, amount, applied, amount - applied, applied == amount);
        }

        /// <summary>非負amountを指定policyで消費する。</summary>
        /// <param name="amount">消費を要求する有限の非負amount。</param>
        /// <param name="policy">不足時に部分消費するか、stateを保つかを決めるpolicy。</param>
        /// <returns>成功時は前後値、負の要求・実適用・未適用delta。失敗時はstateを変えず明示error。</returns>
        public ResourceChangeResult Spend(double amount, ResourceSpendPolicy policy)
        {
            if (!TryValidateAmount(amount, out var error)) return ResourceChangeResult.Failure(error);
            if (policy != ResourceSpendPolicy.AllowPartial && policy != ResourceSpendPolicy.RequireFull) return ResourceChangeResult.Failure(ResourceMeterError.InvalidPolicy);

            var previous = Current;
            if (policy == ResourceSpendPolicy.RequireFull && amount > previous) return ResourceChangeResult.Success(previous, previous, Capacity, -amount, 0d, -amount, false);

            var appliedAmount = Math.Min(amount, previous);
            Current = appliedAmount == previous ? 0d : previous - appliedAmount;
            return ResourceChangeResult.Success(previous, Current, Capacity, -amount, -appliedAmount, -(amount - appliedAmount), appliedAmount == amount);
        }

        /// <summary>検証済みの明示値へ現在stateを再構築する。</summary>
        /// <param name="current">0以上capacity以下の有限値。</param>
        /// <param name="error">成功時None、失敗時は値error。</param>
        /// <returns>再構築できた場合true。失敗時は現在stateを変えない。</returns>
        public bool TryReset(double current, out ResourceMeterError error)
        {
            if (!TryValidateValue(current, Capacity, out error)) return false;
            Current = current;
            return true;
        }

        private static bool TryValidateValue(double value, double capacity, out ResourceMeterError error)
        {
            if (!IsFinite(value))
            {
                error = ResourceMeterError.NonFiniteValue;
                return false;
            }

            if (value < 0d || value > capacity)
            {
                error = ResourceMeterError.ValueOutOfRange;
                return false;
            }

            error = ResourceMeterError.None;
            return true;
        }

        private static bool TryValidateAmount(double amount, out ResourceMeterError error)
        {
            if (!IsFinite(amount))
            {
                error = ResourceMeterError.NonFiniteAmount;
                return false;
            }

            if (amount < 0d)
            {
                error = ResourceMeterError.NegativeAmount;
                return false;
            }

            error = ResourceMeterError.None;
            return true;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
