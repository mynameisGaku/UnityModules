using System;

namespace Containers
{
    /// <summary>
    /// 大量のタイマーをまとめて 1 箇所で更新するコンテナ。
    /// <para>
    /// 本質的な価値は「速いタイマー」ではなく <b>MonoBehaviour.Update とコルーチンを消せること</b>。
    /// クールダウン 1 個ごとに Update を持つ設計は、オブジェクト数に比例して呼び出しコストが増え、
    /// しかも <c>WaitForSeconds</c> は毎回ヒープを確保する。ここに集約すれば更新は 1 回で済み、
    /// ハンドル経由で個別にキャンセル・延長でき、ポーズも自前の <see cref="Tick"/> を呼ばないだけで実現できる。
    /// </para>
    /// </summary>
    public sealed class TimerCollection
    {
        private struct Timer
        {
            public float Remaining;
            public float Interval;
            public Action OnElapsed;
            public bool Repeating;
        }

        private readonly SlotMap<Timer> _timers = new SlotMap<Timer>();

        public int Count => _timers.Count;

        /// <summary>1 回だけ発火するタイマーを登録する。</summary>
        public SlotHandle After(float seconds, Action onElapsed)
        {
            if (onElapsed == null) throw new ArgumentNullException(nameof(onElapsed));
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));

            return _timers.Add(new Timer
            {
                Remaining = seconds,
                Interval = seconds,
                OnElapsed = onElapsed,
                Repeating = false
            });
        }

        /// <summary>一定間隔で繰り返すタイマーを登録する。停止するまで生き続ける。</summary>
        public SlotHandle Every(float seconds, Action onElapsed)
        {
            if (onElapsed == null) throw new ArgumentNullException(nameof(onElapsed));
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds), "繰り返し間隔は正の値が必要。");

            return _timers.Add(new Timer
            {
                Remaining = seconds,
                Interval = seconds,
                OnElapsed = onElapsed,
                Repeating = true
            });
        }

        /// <summary>タイマーを停止する。既に発火・停止済みのハンドルを渡しても安全。</summary>
        public bool Cancel(SlotHandle handle) => _timers.Remove(handle);

        /// <summary>まだ動いているかどうか。世代付きハンドルなので古いハンドルは正しく false になる。</summary>
        public bool IsActive(SlotHandle handle) => _timers.IsAlive(handle);

        /// <summary>残り時間。停止済みなら 0。</summary>
        public float RemainingTime(SlotHandle handle) =>
            _timers.TryGetValue(handle, out var timer) ? timer.Remaining : 0f;

        /// <summary>0（発火直前）から 1（登録直後）へ向かう進捗。UI のゲージ表示用。</summary>
        public float Progress(SlotHandle handle)
        {
            if (!_timers.TryGetValue(handle, out var timer) || timer.Interval <= 0f) return 0f;
            return timer.Remaining / timer.Interval;
        }

        /// <summary>残り時間を初期値に戻す。クールダウンの再発動に使う。</summary>
        public bool Restart(SlotHandle handle)
        {
            if (!_timers.IsAlive(handle)) return false;

            _timers[handle].Remaining = _timers[handle].Interval;
            return true;
        }

        /// <summary>残り時間を増減させる。短縮系のバフ・デバフ用。</summary>
        public bool Adjust(SlotHandle handle, float deltaSeconds)
        {
            if (!_timers.IsAlive(handle)) return false;

            _timers[handle].Remaining = Math.Max(0f, _timers[handle].Remaining + deltaSeconds);
            return true;
        }

        /// <summary>
        /// 全タイマーを進める。ポーズしたければ単に呼ばなければよい。
        /// スケール無視で進めたい場合は <c>Time.unscaledDeltaTime</c> を渡す。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _timers.Count == 0) return;

            // 作業用の配列はローカルに借りる。インスタンスのフィールドを使うと、
            // コールバックから Tick に再入したときに外側が走査中の中身を消してしまう。
            using var expired = TempList<SlotHandle>.Rent();

            var enumerator = _timers.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ref var timer = ref enumerator.CurrentValue;
                timer.Remaining -= deltaTime;
                if (timer.Remaining > 0f) continue;

                expired.List.Add(enumerator.CurrentHandle);
            }

            for (var i = 0; i < expired.List.Count; i++)
            {
                var handle = expired.List[i];

                // 直前のコールバックで止められたタイマーは呼ばない。
                // 発火する直前まで台帳に残しておくことで Cancel が間に合う。
                if (!_timers.TryGetValue(handle, out var timer)) continue;

                if (timer.Repeating)
                {
                    // 溜まった分を丸ごと捨てず間隔を足し込むので、長いフレームがあっても周期がずれない。
                    ref var live = ref _timers[handle];
                    live.Remaining += live.Interval;
                    if (live.Remaining <= 0f) live.Remaining = live.Interval;
                }
                else
                {
                    _timers.Remove(handle);
                }

                timer.OnElapsed?.Invoke();
            }
        }

        /// <summary>全てのタイマーを捨てる。</summary>
        public void Clear() => _timers.Clear();
    }
}
