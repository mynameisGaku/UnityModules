using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 「時刻 t にこれを実行する」を時刻順に保持するキュー。
    /// <para>
    /// <see cref="TimerCollection"/> との違いは基準の取り方。あちらは各タイマーが自分の残り時間を持ち
    /// 毎フレーム全件を減算するが、こちらは<b>絶対時刻でヒープに積む</b>ので、
    /// 1 フレームの処理は「先頭が期限に達したか」を見るだけで済み、登録件数が増えてもコストが変わらない。
    /// </para>
    /// <para>
    /// さらに実行順が投入順ではなく時刻で完全に決まるため<b>決定論的</b>で、
    /// リプレイやネットワークのロックステップ同期と噛み合う。
    /// </para>
    /// </summary>
    public sealed class ScheduledEventQueue
    {
        /// <summary>
        /// 優先度は (時刻, 投入通番) の組。<see cref="ValueTuple"/> の既定比較は
        /// 第 1 要素から順に見るので、同時刻なら通番の小さい方 ＝ 先に積まれた方が先に出る。
        /// 時刻だけを優先度にすると、同時刻の順序がヒープの形に左右されて決定論が崩れる。
        /// </summary>
        private readonly PriorityQueue<Action, (double Time, long Sequence)> _queue =
            new PriorityQueue<Action, (double, long)>();

        /// <summary>
        /// 実行中に積まれたイベントの控え。
        /// <para>
        /// 実行中に直接ヒープへ入れると、それが根に来て「まだ期限の来ていないもの」ではなく
        /// 「今回の対象外のもの」で走査が止まり、<b>本来この回で実行すべき古いイベントが取り残される</b>。
        /// いったんここへ避け、実行が終わってからヒープへ移す。
        /// </para>
        /// </summary>
        private readonly List<(Action Callback, double Time, long Sequence)> _deferred =
            new List<(Action, double, long)>();

        private long _nextSequence;
        private bool _draining;

        /// <summary>このキューが認識している現在時刻。<see cref="Advance"/> で進む。</summary>
        public double CurrentTime { get; private set; }

        public int Count => _queue.Count;

        /// <summary>現在時刻から <paramref name="delaySeconds"/> 後に実行するよう登録する。</summary>
        public void ScheduleAfter(double delaySeconds, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            ScheduleAt(CurrentTime + Math.Max(0d, delaySeconds), callback);
        }

        /// <summary>絶対時刻を指定して登録する。過去の時刻を渡すと次の <see cref="Advance"/> で即実行される。</summary>
        public void ScheduleAt(double time, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            // 通番は積んだ順に確定させる。実行中でも先に採番しておくことで、
            // 控えから戻したあとも投入順が保たれる。
            var sequence = _nextSequence++;

            if (_draining) _deferred.Add((callback, time, sequence));
            else _queue.Enqueue(callback, (time, sequence));
        }

        /// <summary>
        /// 時刻を <paramref name="deltaTime"/> だけ進め、その間に期限を迎えたイベントを時刻順に実行する。
        /// 戻り値は実行した件数。
        /// </summary>
        public int Advance(double deltaTime)
        {
            if (deltaTime < 0d) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            CurrentTime += deltaTime;
            return DrainUntil(CurrentTime);
        }

        /// <summary>時刻を進めずに、既に期限を過ぎているものだけ実行する。</summary>
        public int Flush() => DrainUntil(CurrentTime);

        private int DrainUntil(double time)
        {
            // コールバックが再入してくるとヒープを二重に消費するので弾く。
            if (_draining) throw new InvalidOperationException("ScheduledEventQueue を実行中に再入することはできない。");

            _draining = true;
            var executed = 0;

            try
            {
                // 実行中に積まれたものは _deferred に逃げるので、ヒープの中身は
                // 開始時点の集合のまま。期限だけを見て素直に回せる。
                while (_queue.TryPeek(out _, out var due) && due.Time <= time)
                {
                    _queue.Dequeue()?.Invoke();
                    executed++;
                }
            }
            finally
            {
                _draining = false;
                FlushDeferred();
            }

            return executed;
        }

        /// <summary>実行中に積まれた控えをヒープへ移す。</summary>
        private void FlushDeferred()
        {
            if (_deferred.Count == 0) return;

            for (var i = 0; i < _deferred.Count; i++)
            {
                var entry = _deferred[i];
                _queue.Enqueue(entry.Callback, (entry.Time, entry.Sequence));
            }

            _deferred.Clear();
        }

        /// <summary>次のイベントの発火時刻。空なら false。</summary>
        /// <param name="time">次に発火する時刻。</param>
        public bool TryPeekNextTime(out double time)
        {
            if (!_queue.TryPeek(out _, out var due))
            {
                time = 0d;
                return false;
            }

            time = due.Time;
            return true;
        }

        public void Clear()
        {
            _queue.Clear();
            _deferred.Clear();
        }

        /// <summary>時刻を 0 に戻して中身を捨てる。シーンやマッチの開始時に呼ぶ。</summary>
        public void Reset()
        {
            _queue.Clear();
            _deferred.Clear();
            CurrentTime = 0d;
            _nextSequence = 0;
        }
    }
}
