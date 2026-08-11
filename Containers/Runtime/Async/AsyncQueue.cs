using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Containers.Async
{
    /// <summary>
    /// <c>await</c> できる生産者・消費者キュー。空のときに取り出そうとすると、
    /// 誰かが積むまで待機する。
    /// <para>
    /// Unity 6 の <see cref="Awaitable"/> を返すので、<c>Task</c> と違って待機のたびに
    /// ヒープを確保しない。ロード処理のパイプライン、入力ストリーム、受信パケットの処理など、
    /// 「来たら処理する」ループが素直に書ける。
    /// </para>
    /// <code>
    /// while (!token.IsCancellationRequested)
    /// {
    ///     var packet = await _queue.DequeueAsync(token);
    ///     Apply(packet);
    /// }
    /// </code>
    /// <para>
    /// 積む側はどのスレッドからでも安全。取り出す側はメインスレッドを想定している
    /// （<see cref="Awaitable"/> の再開がメインスレッドで行われるため）。
    /// </para>
    /// </summary>
    public sealed class AsyncQueue<T> : IDisposable
    {
        /// <summary>
        /// 待機中の消費者 1 人分。キャンセル時に待機列から確実に外せるよう、
        /// 連結リストのノードと登録解除ハンドルを一緒に持つ。
        /// </summary>
        private sealed class Waiter
        {
            public AwaitableCompletionSource<T> Source;
            public CancellationTokenRegistration Registration;
            public LinkedListNode<Waiter> Node;

            /// <summary>結果を受け取ったかキャンセルされたか。二重完了を防ぐ。</summary>
            public bool Settled;
        }

        private readonly Queue<T> _items = new Queue<T>();
        private readonly LinkedList<Waiter> _waiters = new LinkedList<Waiter>();
        private readonly object _gate = new object();

        private bool _completed;

        /// <summary>待機中の要素数。待っている消費者の数ではない。</summary>
        public int Count
        {
            get
            {
                lock (_gate) return _items.Count;
            }
        }

        /// <summary>要素を待っている消費者の数。</summary>
        public int WaiterCount
        {
            get
            {
                lock (_gate) return _waiters.Count;
            }
        }

        /// <summary><see cref="Complete"/> 済みかどうか。</summary>
        public bool IsCompleted
        {
            get
            {
                lock (_gate) return _completed;
            }
        }

        /// <summary>
        /// 要素を積む。待っている消費者がいれば、キューを経由せず直接そちらに渡す。
        /// どのスレッドからでも呼べる。
        /// </summary>
        /// <param name="item">積む要素。</param>
        public void Enqueue(T item)
        {
            Waiter waiter = null;

            lock (_gate)
            {
                if (_completed) throw new InvalidOperationException("完了済みの AsyncQueue には積めない。");

                // キャンセル済みの待機者は既に列から外れているが、
                // 念のため未確定のものが見つかるまで進める。
                while (_waiters.Count > 0)
                {
                    var candidate = _waiters.First.Value;
                    _waiters.RemoveFirst();
                    candidate.Node = null;

                    if (candidate.Settled) continue;

                    candidate.Settled = true;
                    waiter = candidate;
                    break;
                }

                // 渡す相手がいなければ溜める。ここで積み忘れると要素が消える。
                if (waiter == null) _items.Enqueue(item);
            }

            if (waiter == null) return;

            // ロックの外で再開させる。継続処理が Enqueue を呼び返しても詰まらないようにするため。
            waiter.Registration.Dispose();
            waiter.Source.SetResult(item);
        }

        /// <summary>要素があれば即座に取り出す。待たない。</summary>
        /// <param name="item">取り出した要素。</param>
        public bool TryDequeue(out T item)
        {
            lock (_gate)
            {
                if (_items.Count > 0)
                {
                    item = _items.Dequeue();
                    return true;
                }
            }

            item = default;
            return false;
        }

        /// <summary>
        /// 要素を 1 つ取り出す。無ければ積まれるまで待つ。
        /// <paramref name="cancellationToken"/> が発火すると <see cref="OperationCanceledException"/> になる。
        /// </summary>
        /// <param name="cancellationToken">待機を打ち切るためのトークン。</param>
        public Awaitable<T> DequeueAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Waiter waiter;

            lock (_gate)
            {
                if (_items.Count > 0)
                {
                    var ready = new AwaitableCompletionSource<T>();
                    ready.SetResult(_items.Dequeue());
                    return ready.Awaitable;
                }

                if (_completed) throw new InvalidOperationException("完了済みで、これ以上要素は来ない。");

                waiter = new Waiter { Source = new AwaitableCompletionSource<T>() };
                waiter.Node = _waiters.AddLast(waiter);
            }

            if (cancellationToken.CanBeCanceled)
            {
                // 登録ハンドルは待機者に持たせ、解決時に必ず捨てる。
                // 捨てずに放置すると、トークンのコールバック列が待機のたびに伸びる。
                var registration = cancellationToken.Register(state => CancelWaiter((Waiter)state), waiter);

                bool settledDuringRegistration;
                lock (_gate)
                {
                    // Register はトークンが既に発火していればその場でコールバックを呼ぶ。
                    // その場合 CancelWaiter はまだハンドルを知らないので、捨てるのはこちらの仕事。
                    settledDuringRegistration = waiter.Settled;
                    if (!settledDuringRegistration) waiter.Registration = registration;
                }

                if (settledDuringRegistration) registration.Dispose();
            }

            return waiter.Source.Awaitable;
        }

        /// <summary>
        /// 待機者をキャンセルし、<b>待機列からも外す</b>。
        /// 外さないと、次に積まれた要素がこの死んだ待機者に渡され、
        /// 二重完了で例外になったうえ要素が消える。
        /// </summary>
        private void CancelWaiter(Waiter waiter)
        {
            lock (_gate)
            {
                if (waiter.Settled) return;

                waiter.Settled = true;
                if (waiter.Node != null)
                {
                    _waiters.Remove(waiter.Node);
                    waiter.Node = null;
                }
            }

            waiter.Registration.Dispose();
            waiter.Source.SetCanceled();
        }

        /// <summary>
        /// これ以上積まれないことを宣言し、待機中の消費者を全て解除する。
        /// 消費側のループを終わらせるために使う。
        /// </summary>
        public void Complete()
        {
            Waiter[] pending;

            lock (_gate)
            {
                if (_completed) return;

                _completed = true;

                pending = new Waiter[_waiters.Count];
                _waiters.CopyTo(pending, 0);
                _waiters.Clear();

                foreach (var waiter in pending)
                {
                    waiter.Node = null;
                    waiter.Settled = true;
                }
            }

            foreach (var waiter in pending)
            {
                waiter.Registration.Dispose();
                waiter.Source.SetCanceled();
            }
        }

        /// <summary>溜まっている要素を捨てる。待機中の消費者には影響しない。</summary>
        public void Clear()
        {
            lock (_gate) _items.Clear();
        }

        /// <summary><see cref="Complete"/> と同じ。</summary>
        public void Dispose() => Complete();
    }
}
