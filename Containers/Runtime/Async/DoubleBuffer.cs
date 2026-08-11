using System;

namespace Containers.Async
{
    /// <summary>
    /// 書き込み用と読み取り用の 2 面を持ち、フレーム境界で入れ替えるコンテナ。
    /// <para>
    /// ワーカースレッドが書く面とメインスレッドが読む面を分けてしまえば、
    /// <b>参照している間はロックが要らない</b>。入れ替えの瞬間だけを守ればよい。
    /// 経路探索の結果、視界判定、群衆シミュレーション、プロファイラのサンプルなど、
    /// 「1 フレーム古くても構わないが毎フレーム読む」データに向く。
    /// </para>
    /// <para>
    /// 1 フレーム分の遅延が入ることは前提として受け入れる設計。
    /// 即時性が要るなら <see cref="MainThreadQueue"/> の方が正しい。
    /// </para>
    /// </summary>
    public sealed class DoubleBuffer<T> where T : class
    {
        private readonly object _swapGate = new object();

        private T _front;   // 読む側
        private T _back;    // 書く側

        /// <param name="factory">2 面それぞれを作る。同じインスタンスを返してはいけない。</param>
        public DoubleBuffer(Func<T> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _front = factory();
            _back = factory();

            if (ReferenceEquals(_front, _back))
            {
                throw new ArgumentException("factory は毎回別のインスタンスを返す必要がある。", nameof(factory));
            }
        }

        public DoubleBuffer(T front, T back)
        {
            _front = front ?? throw new ArgumentNullException(nameof(front));
            _back = back ?? throw new ArgumentNullException(nameof(back));

            if (ReferenceEquals(front, back)) throw new ArgumentException("2 面は別のインスタンスである必要がある。");
        }

        /// <summary>入れ替えた回数。</summary>
        public int SwapCount { get; private set; }

        /// <summary>読み取り側の面。<see cref="Swap"/> するまで内容は変わらない。</summary>
        public T Front
        {
            get
            {
                lock (_swapGate) return _front;
            }
        }

        /// <summary>書き込み側の面。読み手からは見えていない。</summary>
        public T Back
        {
            get
            {
                lock (_swapGate) return _back;
            }
        }

        /// <summary>
        /// 2 面を入れ替える。書き手が 1 サイクル分の書き込みを終えた時点で、
        /// 読み手が参照していないタイミング（通常はメインスレッドのフレーム境界）から呼ぶ。
        /// </summary>
        public void Swap()
        {
            lock (_swapGate)
            {
                (_front, _back) = (_back, _front);
                SwapCount++;
            }
        }

        /// <summary>
        /// 書き込み面を <paramref name="write"/> に渡し、終わったら入れ替える。
        /// 書いたあとに Swap を呼び忘れる事故を防ぐ。
        /// </summary>
        public void WriteAndSwap(Action<T> write)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));

            write(Back);
            Swap();
        }
    }
}
