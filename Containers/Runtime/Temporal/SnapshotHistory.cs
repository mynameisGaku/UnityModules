using System;

namespace Containers
{
    /// <summary>
    /// 状態のスナップショットを時刻付きでリングに溜め、任意の時刻を復元・補間できるようにするコンテナ。
    /// <para>
    /// 用途は 4 つとも実装が同じになる：ロールバックネットコードの巻き戻し、時間逆行ギミック、
    /// ゴーストリプレイ、そして<b>バグった瞬間の状態を後から取り出すデバッグ</b>。
    /// </para>
    /// <para>
    /// 容量は固定で、溢れたら最も古いものから捨てる。「直近 N 秒だけ」が欲しい形なので、
    /// 伸び続けるリストではなく <see cref="RingBuffer{T}"/> が正しい土台になる。
    /// </para>
    /// </summary>
    public sealed class SnapshotHistory<T>
    {
        private readonly struct Frame
        {
            public readonly double Time;
            public readonly T State;

            public Frame(double time, T state)
            {
                Time = time;
                State = state;
            }
        }

        private readonly RingBuffer<Frame> _frames;

        public SnapshotHistory(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _frames = new RingBuffer<Frame>(capacity);
        }

        public int Count => _frames.Count;
        public int Capacity => _frames.Capacity;
        public bool IsEmpty => _frames.IsEmpty;

        /// <summary>保持している最も古い時刻。空なら 0。</summary>
        public double OldestTime => _frames.IsEmpty ? 0d : _frames[0].Time;

        /// <summary>保持している最も新しい時刻。空なら 0。</summary>
        public double NewestTime => _frames.IsEmpty ? 0d : _frames[_frames.Count - 1].Time;

        /// <summary>
        /// スナップショットを記録する。時刻は単調増加でなければならない
        /// （そうでないと二分探索が成立しない）。
        /// </summary>
        public void Record(double time, in T state)
        {
            if (!_frames.IsEmpty && time < _frames[_frames.Count - 1].Time)
            {
                throw new ArgumentOutOfRangeException(nameof(time), "スナップショットの時刻は巻き戻せない。");
            }

            _frames.PushBack(new Frame(time, state));
        }

        /// <summary>最新のスナップショット。</summary>
        public bool TryGetLatest(out T state, out double time)
        {
            if (_frames.IsEmpty)
            {
                state = default;
                time = 0d;
                return false;
            }

            var frame = _frames[_frames.Count - 1];
            state = frame.State;
            time = frame.Time;
            return true;
        }

        /// <summary>
        /// <paramref name="time"/> 以前で最も新しいスナップショット。
        /// 補間せずそのまま採用する用途（決定論的な再シミュレーション）向け。
        /// </summary>
        public bool TryGetAt(double time, out T state)
        {
            var index = FindIndexAtOrBefore(time);
            if (index < 0)
            {
                state = default;
                return false;
            }

            state = _frames[index].State;
            return true;
        }

        /// <summary>
        /// <paramref name="time"/> を挟む 2 つのスナップショットと、その間の位置 <c>t</c> を返す。
        /// <para>
        /// 補間そのものは呼び出し側に任せる。位置は線形でよくても回転は球面線形が要る、というように
        /// 型ごとに正しい補間が違うため、ここで決め打ちにはしない。
        /// </para>
        /// </summary>
        public bool TryGetSurrounding(double time, out T before, out T after, out float t)
        {
            before = default;
            after = default;
            t = 0f;

            if (_frames.Count == 0) return false;

            if (_frames.Count == 1 || time <= _frames[0].Time)
            {
                before = after = _frames[0].State;
                return true;
            }

            if (time >= _frames[_frames.Count - 1].Time)
            {
                before = after = _frames[_frames.Count - 1].State;
                return true;
            }

            var index = FindIndexAtOrBefore(time);
            if (index < 0 || index + 1 >= _frames.Count) return false;

            var first = _frames[index];
            var second = _frames[index + 1];

            before = first.State;
            after = second.State;

            var span = second.Time - first.Time;
            t = span <= 0d ? 0f : (float)((time - first.Time) / span);
            return true;
        }

        /// <summary>
        /// <paramref name="time"/> より後のスナップショットを捨てる。
        /// ロールバック後に正しい入力で再シミュレーションし直す前段で使う。
        /// </summary>
        public int TruncateAfter(double time)
        {
            var removed = 0;
            while (_frames.Count > 0 && _frames[_frames.Count - 1].Time > time)
            {
                _frames.PopBack();
                removed++;
            }

            return removed;
        }

        public void Clear() => _frames.Clear();

        /// <summary><paramref name="time"/> 以下で最大の時刻を持つ要素の位置。無ければ -1。</summary>
        private int FindIndexAtOrBefore(double time)
        {
            if (_frames.Count == 0 || time < _frames[0].Time) return -1;

            var low = 0;
            var high = _frames.Count - 1;
            var result = -1;

            while (low <= high)
            {
                var middle = (low + high) / 2;

                if (_frames[middle].Time <= time)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return result;
        }
    }
}
