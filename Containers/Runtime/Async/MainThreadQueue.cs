using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Containers.Async
{
    /// <summary>
    /// 別スレッドから積んだ処理を、メインスレッドの好きなタイミングで実行するキュー。
    /// <para>
    /// Unity の API はほぼ全てメインスレッド専用で、ワーカースレッドから <c>transform.position</c> に
    /// 触れた瞬間に例外になる。Job / Task / ネットワーク受信スレッドの結果を反映するには
    /// 必ずこの受け渡しが要るので、ほぼ全てのプロジェクトで一度は書くことになる。
    /// </para>
    /// <para>
    /// <see cref="Drain"/> を <c>Update</c> から呼ぶ。1 フレームあたりの実行件数に上限を掛けられるので、
    /// 大量に溜まっても 1 フレームで処理しきってスパイクを作ることがない。
    /// </para>
    /// </summary>
    public sealed class MainThreadQueue
    {
        private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();
        private int _mainThreadId;

        public MainThreadQueue() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>待機中の件数。</summary>
        public int Count => _pending.Count;

        /// <summary>今のスレッドがメインスレッドかどうか。</summary>
        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// メインスレッドの識別をやり直す。生成場所がメインスレッドでない場合に、
        /// メインスレッドから 1 回呼んでおく。
        /// </summary>
        public void BindToCurrentThread() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>どのスレッドからでも安全に積める。</summary>
        public void Enqueue(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _pending.Enqueue(action);
        }

        /// <summary>
        /// 既にメインスレッドなら即実行、そうでなければキューに積む。
        /// 呼び出し元がどちらのスレッドか分からない共通処理で使う。
        /// </summary>
        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (IsMainThread) action();
            else _pending.Enqueue(action);
        }

        /// <summary>
        /// 溜まった処理を実行する。<c>Update</c> や <c>LateUpdate</c> から呼ぶ。
        /// </summary>
        /// <param name="maxPerCall">
        /// 1 回で実行する上限。0 以下なら全件。大量に溜まりうるなら上限を掛けて
        /// フレーム落ちを避ける。
        /// </param>
        /// <returns>実行した件数。</returns>
        public int Drain(int maxPerCall = 0)
        {
            var executed = 0;

            while ((maxPerCall <= 0 || executed < maxPerCall) && _pending.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    // 1 件の失敗で残りが詰まらないようにする。
                    Debug.LogException(exception);
                }

                executed++;
            }

            return executed;
        }

        public void Clear()
        {
            while (_pending.TryDequeue(out _)) { }
        }
    }
}
