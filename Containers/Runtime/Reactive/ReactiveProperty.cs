using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 値 1 個と、その変更通知を持つコンテナ。実質 1 要素のコレクション。
    /// <para>
    /// R3 や UniRx を丸ごと導入するほどではないが、HP やスコアの変化を UI に伝えたい ——
    /// という場面のための最小実装。<see cref="Subscribe"/> は解除用の
    /// <see cref="IDisposable"/> を返すので、<see cref="DisposableBag"/> にそのまま入れられる。
    /// </para>
    /// <para>
    /// 毎フレーム変わる値なら <see cref="VersionedValue{T}"/> の方が安い。
    /// こちらは通知の即時性が要る場合に使う。
    /// </para>
    /// </summary>
    public sealed class ReactiveProperty<T>
    {
        private readonly List<Action<T>> _observers = new List<Action<T>>();
        private readonly IEqualityComparer<T> _comparer;

        private T _value;

        public ReactiveProperty(T initialValue = default, IEqualityComparer<T> comparer = null)
        {
            _value = initialValue;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public int ObserverCount => _observers.Count;

        /// <summary>値。同じ値を書いても通知は飛ばない。</summary>
        public T Value
        {
            get => _value;
            set
            {
                if (_comparer.Equals(_value, value)) return;

                _value = value;
                Notify();
            }
        }

        /// <summary>等値判定を飛ばして必ず通知する。</summary>
        public void ForceSet(T value)
        {
            _value = value;
            Notify();
        }

        /// <summary>
        /// 変更を購読する。<paramref name="notifyImmediately"/> が true なら現在値で即座に 1 回呼ぶ ——
        /// UI の初期表示と更新を同じコードで書けるようにするため。
        /// </summary>
        public IDisposable Subscribe(Action<T> observer, bool notifyImmediately = true)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));

            _observers.Add(observer);
            if (notifyImmediately) observer(_value);

            return Disposable.Create(() => _observers.Remove(observer));
        }

        public void UnsubscribeAll() => _observers.Clear();

        /// <summary>
        /// 購読者に配る。
        /// <para>
        /// 添字で回しながら解除されるとリストが詰まり、同じ購読者を 2 回呼んだり
        /// 別の購読者を飛ばしたりする。配布前に写しを取り、呼ぶ直前に
        /// まだ購読中かを確認することで、どちらも起きないようにしている。
        /// </para>
        /// </summary>
        private void Notify()
        {
            if (_observers.Count == 0) return;

            using var snapshot = TempList<Action<T>>.Rent();
            for (var i = 0; i < _observers.Count; i++) snapshot.List.Add(_observers[i]);

            for (var i = 0; i < snapshot.List.Count; i++)
            {
                var observer = snapshot.List[i];

                // 同じ配布の中で解除された購読者には配らない。
                if (observer == null || !_observers.Contains(observer)) continue;

                observer(_value);
            }
        }

        public override string ToString() => _value?.ToString() ?? "null";

        public static implicit operator T(ReactiveProperty<T> property) => property._value;
    }

    /// <summary>
    /// 読み取りと購読だけを公開したいときに被せるビュー。
    /// 書き込みは持ち主だけ、という関係を型で表せる。
    /// </summary>
    public readonly struct ReadOnlyReactiveProperty<T>
    {
        private readonly ReactiveProperty<T> _source;

        public ReadOnlyReactiveProperty(ReactiveProperty<T> source) =>
            _source = source ?? throw new ArgumentNullException(nameof(source));

        public T Value => _source.Value;

        public IDisposable Subscribe(Action<T> observer, bool notifyImmediately = true) =>
            _source.Subscribe(observer, notifyImmediately);

        public static implicit operator T(ReadOnlyReactiveProperty<T> property) => property.Value;
    }
}
