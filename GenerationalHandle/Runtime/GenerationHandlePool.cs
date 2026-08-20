using System;
using System.Collections.Generic;

namespace GenerationalHandles
{
    /// <summary>明示capacity内で最小の空きslotを決定論的に割り当て、古いgenerationを拒否するowner付きpool。</summary>
    public sealed class GenerationHandlePool
    {
        /// <summary>過大な配列確保を防ぐv1の最大slot数。</summary>
        public const int MaximumCapacity = 1_000_000;

        private readonly uint[] _generations;
        private readonly bool[] _active;
        private readonly SortedSet<int> _freeSlots = new SortedSet<int>();
        private int _nextUnusedSlot;

        /// <summary>このpoolが扱えるslot総数。</summary>
        public int Capacity { get; }

        /// <summary>現在有効なhandle数。</summary>
        public int ActiveCount { get; private set; }

        /// <summary>generation上限へ到達し、再利用しないslot数。</summary>
        public int RetiredCount { get; private set; }

        /// <summary>今後Acquireできるslot数。</summary>
        public int AvailableCount => Capacity - ActiveCount - RetiredCount;

        /// <summary>明示capacityを持つ空のpoolを作成する。</summary>
        /// <param name="capacity">1以上MaximumCapacity以下のslot数。</param>
        /// <exception cref="ArgumentOutOfRangeException">capacityが許容範囲外。</exception>
        public GenerationHandlePool(int capacity)
        {
            if (capacity < 1 || capacity > MaximumCapacity) throw new ArgumentOutOfRangeException(nameof(capacity), $"capacityは1以上{MaximumCapacity}以下にしてください。");

            Capacity = capacity;
            _generations = new uint[capacity];
            _active = new bool[capacity];
        }

        /// <summary>最小の空きslotへhandleを1つ割り当てる。</summary>
        /// <param name="handle">成功時の有効handle。失敗時はdefault。</param>
        /// <param name="error">成功時None、空きが無い場合CapacityReached。</param>
        /// <returns>割当できた場合true。</returns>
        public bool TryAcquire(out GenerationHandle handle, out GenerationHandleError error)
        {
            int slot;
            if (_freeSlots.Count > 0)
            {
                slot = _freeSlots.Min;
                _freeSlots.Remove(slot);
            }
            else if (_nextUnusedSlot < Capacity)
            {
                slot = _nextUnusedSlot;
                _nextUnusedSlot++;
                _generations[slot] = 1;
            }
            else
            {
                handle = default;
                error = GenerationHandleError.CapacityReached;
                return false;
            }

            _active[slot] = true;
            ActiveCount++;
            handle = new GenerationHandle(slot, _generations[slot]);
            error = GenerationHandleError.None;
            return true;
        }

        /// <summary>現在有効なhandleを解放し、次のgenerationへ進める。</summary>
        /// <param name="handle">このpoolが現在所有するhandle。</param>
        /// <returns>成功時None、構造不正ならInvalidHandle、古い世代ならStaleHandle。</returns>
        public GenerationHandleError Release(GenerationHandle handle)
        {
            if (!handle.IsValid || handle.Slot >= _nextUnusedSlot) return GenerationHandleError.InvalidHandle;
            if (!_active[handle.Slot] || _generations[handle.Slot] != handle.Generation) return GenerationHandleError.StaleHandle;

            _active[handle.Slot] = false;
            ActiveCount--;
            if (_generations[handle.Slot] == uint.MaxValue)
            {
                RetiredCount++;
            }
            else
            {
                _generations[handle.Slot]++;
                _freeSlots.Add(handle.Slot);
            }

            return GenerationHandleError.None;
        }

        /// <summary>handleがこのpoolで現在有効かを返す。</summary>
        /// <param name="handle">確認するhandle。</param>
        /// <returns>slotとgenerationが現在のactive entryに一致すればtrue。</returns>
        public bool IsActive(GenerationHandle handle)
        {
            return handle.IsValid
                && handle.Slot < _nextUnusedSlot
                && _active[handle.Slot]
                && _generations[handle.Slot] == handle.Generation;
        }

        /// <summary>generation上限の回帰検証だけに使う内部hook。</summary>
        internal GenerationHandle SetGenerationForTesting(GenerationHandle handle, uint generation)
        {
            if (!IsActive(handle) || generation == 0) throw new ArgumentOutOfRangeException(nameof(generation));
            _generations[handle.Slot] = generation;
            return new GenerationHandle(handle.Slot, generation);
        }
    }
}
