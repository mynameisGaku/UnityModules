using System;
using System.Collections.Generic;
using UnityEngine;

namespace Containers.Gameplay
{
    /// <summary>格子状インベントリに置かれた 1 個のアイテム。</summary>
    public readonly struct InventoryEntry<TItem>
    {
        public readonly TItem Item;

        /// <summary>占有領域の左下セル。</summary>
        public readonly Vector2Int Origin;

        public readonly Vector2Int Size;
        public readonly int StackCount;

        public InventoryEntry(TItem item, Vector2Int origin, Vector2Int size, int stackCount)
        {
            Item = item;
            Origin = origin;
            Size = size;
            StackCount = stackCount;
        }

        public RectInt Rect => new RectInt(Origin.x, Origin.y, Size.x, Size.y);
    }

    /// <summary>
    /// 格子状のインベントリ。アイテムが複数セルを占有し、スタックし、空きを自動で探す。
    /// <para>
    /// Diablo や Escape from Tarkov 型の「鞄に形で詰める」インベントリ。
    /// 一次元のスロット配列で足りるなら <see cref="Counter{T}"/> の方が適切で、
    /// こちらは<b>占有形状が意味を持つ</b>場合に使う。
    /// </para>
    /// <para>
    /// 占有情報はセルごとのハンドル配列で持つので、
    /// 「このセルに何があるか」も「このアイテムはどこにあるか」も O(1) で引ける。
    /// </para>
    /// </summary>
    public sealed class InventoryGrid<TItem>
    {
        private struct Placement
        {
            public TItem Item;
            public Vector2Int Origin;
            public Vector2Int Size;
            public int StackCount;
        }

        private readonly SlotMap<Placement> _placements = new SlotMap<Placement>();
        private readonly SlotHandle[] _occupancy;
        private readonly IEqualityComparer<TItem> _comparer;

        public InventoryGrid(int width, int height, IEqualityComparer<TItem> comparer = null)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            _occupancy = new SlotHandle[width * height];
            _comparer = comparer ?? EqualityComparer<TItem>.Default;
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>置かれているアイテムの数（スタック数ではない）。</summary>
        public int Count => _placements.Count;

        /// <summary>アイテムが増減・移動したときに発火する。</summary>
        public event Action Changed;

        /// <summary>指定セルにあるアイテム。無ければ false。</summary>
        public bool TryGetAt(Vector2Int cell, out InventoryEntry<TItem> entry)
        {
            entry = default;
            if (!InBounds(cell)) return false;

            var handle = _occupancy[cell.y * Width + cell.x];
            if (!_placements.TryGetValue(handle, out var placement)) return false;

            entry = new InventoryEntry<TItem>(placement.Item, placement.Origin, placement.Size, placement.StackCount);
            return true;
        }

        /// <summary><paramref name="size"/> のアイテムを <paramref name="origin"/> に置けるか。</summary>
        public bool CanPlaceAt(Vector2Int origin, Vector2Int size, SlotHandle ignoring = default)
        {
            if (size.x <= 0 || size.y <= 0) return false;
            if (origin.x < 0 || origin.y < 0) return false;
            if (origin.x + size.x > Width || origin.y + size.y > Height) return false;

            for (var y = 0; y < size.y; y++)
            for (var x = 0; x < size.x; x++)
            {
                var occupant = _occupancy[(origin.y + y) * Width + origin.x + x];
                if (!occupant.IsValid) continue;
                if (occupant == ignoring) continue;   // 移動時、自分自身との衝突は無視する
                if (_placements.IsAlive(occupant)) return false;
            }

            return true;
        }

        /// <summary>指定位置に置く。置けなければ <see cref="SlotHandle.None"/>。</summary>
        public SlotHandle PlaceAt(TItem item, Vector2Int origin, Vector2Int size, int stackCount = 1)
        {
            if (stackCount <= 0) throw new ArgumentOutOfRangeException(nameof(stackCount));
            if (!CanPlaceAt(origin, size)) return SlotHandle.None;

            var handle = _placements.Add(new Placement
            {
                Item = item,
                Origin = origin,
                Size = size,
                StackCount = stackCount
            });

            Occupy(origin, size, handle);
            Changed?.Invoke();
            return handle;
        }

        /// <summary>
        /// 空いている場所を探して置く。左下から行優先で走査し、最初に収まった場所を使う。
        /// 見つからなければ <see cref="SlotHandle.None"/>。
        /// </summary>
        public SlotHandle TryAdd(TItem item, Vector2Int size, int stackCount = 1)
        {
            for (var y = 0; y <= Height - size.y; y++)
            for (var x = 0; x <= Width - size.x; x++)
            {
                var origin = new Vector2Int(x, y);
                if (!CanPlaceAt(origin, size)) continue;

                return PlaceAt(item, origin, size, stackCount);
            }

            return SlotHandle.None;
        }

        /// <summary>
        /// 同じアイテムの既存スタックに載せ、載り切らない分だけ新しい場所を探す。
        /// 戻り値は置けなかった残り個数。
        /// </summary>
        public int AddStacking(TItem item, Vector2Int size, int amount, int maxStack)
        {
            if (amount <= 0) return 0;
            if (maxStack <= 0) throw new ArgumentOutOfRangeException(nameof(maxStack));

            var remaining = amount;

            // まず既存スタックの空きを埋める。
            var enumerator = _placements.GetEnumerator();
            while (enumerator.MoveNext() && remaining > 0)
            {
                ref var placement = ref enumerator.CurrentValue;
                if (!_comparer.Equals(placement.Item, item)) continue;

                var room = maxStack - placement.StackCount;
                if (room <= 0) continue;

                var moved = Math.Min(room, remaining);
                placement.StackCount += moved;
                remaining -= moved;
            }

            // 残りは新しいスタックとして置く。
            while (remaining > 0)
            {
                var chunk = Math.Min(maxStack, remaining);
                if (!TryAdd(item, size, chunk).IsValid) break;

                remaining -= chunk;
            }

            if (remaining != amount) Changed?.Invoke();
            return remaining;
        }

        /// <summary>置いてあるアイテムを別の場所へ移す。移せなければ false（元の位置のまま）。</summary>
        public bool Move(SlotHandle handle, Vector2Int newOrigin)
        {
            if (!_placements.TryGetValue(handle, out var placement)) return false;
            if (!CanPlaceAt(newOrigin, placement.Size, handle)) return false;

            Vacate(placement.Origin, placement.Size);
            _placements[handle].Origin = newOrigin;
            Occupy(newOrigin, placement.Size, handle);

            Changed?.Invoke();
            return true;
        }

        public bool Remove(SlotHandle handle)
        {
            if (!_placements.TryGetValue(handle, out var placement)) return false;

            Vacate(placement.Origin, placement.Size);
            _placements.Remove(handle);
            Changed?.Invoke();
            return true;
        }

        /// <summary>スタック数を変更する。0 以下にすると取り除かれる。</summary>
        public bool SetStackCount(SlotHandle handle, int stackCount)
        {
            if (!_placements.IsAlive(handle)) return false;

            if (stackCount <= 0) return Remove(handle);

            _placements[handle].StackCount = stackCount;
            Changed?.Invoke();
            return true;
        }

        /// <summary>あるアイテムの総所持数。</summary>
        public int TotalCountOf(TItem item)
        {
            var total = 0;

            var enumerator = _placements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ref var placement = ref enumerator.CurrentValue;
                if (_comparer.Equals(placement.Item, item)) total += placement.StackCount;
            }

            return total;
        }

        /// <summary>置かれている全アイテムを列挙する。</summary>
        public void CopyEntries(FastList<InventoryEntry<TItem>> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            var enumerator = _placements.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ref var placement = ref enumerator.CurrentValue;
                results.Add(new InventoryEntry<TItem>(placement.Item, placement.Origin, placement.Size, placement.StackCount));
            }
        }

        public void Clear()
        {
            _placements.Clear();
            Array.Clear(_occupancy, 0, _occupancy.Length);
            Changed?.Invoke();
        }

        private bool InBounds(Vector2Int cell) =>
            (uint)cell.x < (uint)Width && (uint)cell.y < (uint)Height;

        private void Occupy(Vector2Int origin, Vector2Int size, SlotHandle handle)
        {
            for (var y = 0; y < size.y; y++)
            for (var x = 0; x < size.x; x++)
            {
                _occupancy[(origin.y + y) * Width + origin.x + x] = handle;
            }
        }

        private void Vacate(Vector2Int origin, Vector2Int size)
        {
            for (var y = 0; y < size.y; y++)
            for (var x = 0; x < size.x; x++)
            {
                _occupancy[(origin.y + y) * Width + origin.x + x] = SlotHandle.None;
            }
        }
    }
}
