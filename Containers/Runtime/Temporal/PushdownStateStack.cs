using System;

namespace Containers
{
    /// <summary>
    /// <see cref="PushdownStateStack{TState}"/> が積む状態。全メソッド任意実装で構わない。
    /// </summary>
    public interface IStackState
    {
        /// <summary>この状態が積まれたとき。</summary>
        void OnEnter();

        /// <summary>この状態が取り除かれたとき。</summary>
        void OnExit();

        /// <summary>上に別の状態が積まれ、覆い隠されたとき。</summary>
        void OnCovered();

        /// <summary>上の状態が外れ、再び最前面に戻ったとき。</summary>
        void OnRevealed();

        /// <summary>最前面のときだけ毎フレーム呼ばれる。</summary>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// 状態をスタックで持つ状態機械。
    /// <para>
    /// 遷移表を書く普通の FSM と違い、<b>「元に戻る」を実装しなくてよい</b>のが利点。
    /// プレイ中 → ポーズ → 設定 → キー設定 と潜って戻るとき、各状態は自分がどこから来たかを
    /// 知る必要がなく、<see cref="Pop"/> するだけで正しい場所に戻る。
    /// UI の画面遷移、ゲームモード、AI の割り込み行動（戦闘中に回避を挟んで戻る）に向く。
    /// </para>
    /// </summary>
    public sealed class PushdownStateStack<TState> where TState : class, IStackState
    {
        private readonly FastList<TState> _stack = new FastList<TState>();

        /// <summary>状態が出入りしたときに通知する。デバッグ表示用。</summary>
        public event Action<TState> Pushed;
        public event Action<TState> Popped;

        public int Count => _stack.Count;
        public bool IsEmpty => _stack.Count == 0;

        /// <summary>最前面の状態。空なら null。</summary>
        public TState Current => _stack.Count == 0 ? null : _stack[_stack.Count - 1];

        /// <summary>下から <paramref name="depth"/> 番目ではなく、上から数えた状態。0 が最前面。</summary>
        public TState Peek(int depth = 0)
        {
            var index = _stack.Count - 1 - depth;
            return (uint)index < (uint)_stack.Count ? _stack[index] : null;
        }

        /// <summary>状態を積む。今の最前面には <see cref="IStackState.OnCovered"/> が飛ぶ。</summary>
        public void Push(TState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            Current?.OnCovered();
            _stack.Add(state);
            state.OnEnter();
            Pushed?.Invoke(state);
        }

        /// <summary>最前面を取り除く。下の状態には <see cref="IStackState.OnRevealed"/> が飛ぶ。</summary>
        public TState Pop()
        {
            if (_stack.Count == 0) return null;

            var state = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);

            state.OnExit();
            Popped?.Invoke(state);
            Current?.OnRevealed();
            return state;
        }

        /// <summary>
        /// 最前面を差し替える。<see cref="Pop"/> と <see cref="Push"/> の違いは、
        /// 下の状態に <c>OnRevealed</c> / <c>OnCovered</c> が無駄に飛ばないこと。
        /// </summary>
        public void Replace(TState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (_stack.Count > 0)
            {
                var previous = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                previous.OnExit();
                Popped?.Invoke(previous);
            }

            _stack.Add(state);
            state.OnEnter();
            Pushed?.Invoke(state);
        }

        /// <summary><paramref name="state"/> が最前面になるまで Pop する。「メインメニューまで戻る」用。</summary>
        public void PopTo(TState state)
        {
            while (_stack.Count > 0 && !ReferenceEquals(Current, state)) Pop();
        }

        /// <summary>1 つ残して全部 Pop する。</summary>
        public void PopToRoot()
        {
            while (_stack.Count > 1) Pop();
        }

        public void Clear()
        {
            while (_stack.Count > 0) Pop();
        }

        public bool Contains(TState state) => _stack.Contains(state);

        /// <summary>最前面の状態だけを進める。覆われた状態は止まる。</summary>
        public void Tick(float deltaTime) => Current?.Tick(deltaTime);

        /// <summary>
        /// 覆われた状態も含めて全部進める。ポーズ中も背景の演出を動かしたい場合など。
        /// 下から順に呼ぶ。
        /// </summary>
        public void TickAll(float deltaTime)
        {
            for (var i = 0; i < _stack.Count; i++) _stack[i].Tick(deltaTime);
        }
    }
}
