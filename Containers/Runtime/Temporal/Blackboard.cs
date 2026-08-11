using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// <see cref="Blackboard"/> のキー。型を鍵自体に埋め込むことで、読み書きの型が構文上一致することを保証する。
    /// </summary>
    public readonly struct BlackboardKey<T> : IEquatable<BlackboardKey<T>>
    {
        internal readonly int Id;

        internal BlackboardKey(int id) => Id = id;

        /// <summary>名前からキーを作る。同じ名前・同じ型なら常に同じキーになる。</summary>
        public static BlackboardKey<T> Named(string name) =>
            new BlackboardKey<T>(StringId.Get(name).Value);

        public string Name => StringIdRegistry.TextOf(Id);

        public bool Equals(BlackboardKey<T> other) => Id == other.Id;
        public override bool Equals(object obj) => obj is BlackboardKey<T> other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => $"{Name}:{typeof(T).Name}";
    }

    /// <summary>
    /// 型の違う値を 1 箇所に置ける共有メモ。AI の判断材料、会話フラグ、クエスト進行、チュートリアル状態など。
    /// <para>
    /// 素直に書くと <c>Dictionary&lt;string, object&gt;</c> になるが、それだと int や float を入れるたびに
    /// <b>ボクシングでゴミが出る</b>し、取り出すときのキャストが実行時まで検証されない。
    /// ここでは<b>型ごとに別の辞書を持つ</b>ことでどちらも回避している
    /// （<c>Store&lt;T&gt;</c> の静的フィールドが型引数ごとに独立するのを利用）。
    /// </para>
    /// <code>
    /// static readonly BlackboardKey&lt;float&gt; Aggression = BlackboardKey&lt;float&gt;.Named("aggression");
    /// board.Set(Aggression, 0.8f);
    /// var value = board.Get(Aggression);   // キャスト不要・ボクシング無し
    /// </code>
    /// </summary>
    public sealed class Blackboard
    {
        /// <summary>型ごとの実体。<c>_stores</c> はクリアと診断のためだけに全ストアを覚えておく。</summary>
        private sealed class Store<T>
        {
            public readonly Dictionary<int, T> Values = new Dictionary<int, T>();
        }

        private readonly Dictionary<Type, object> _stores = new Dictionary<Type, object>();

        /// <summary>値が書き換わったときに通知する。キー名を渡す。</summary>
        public event Action<string> ValueChanged;

        /// <summary>格納している型の数。件数ではない。</summary>
        public int TypeCount => _stores.Count;

        public void Set<T>(BlackboardKey<T> key, T value)
        {
            GetStore<T>().Values[key.Id] = value;
            ValueChanged?.Invoke(key.Name);
        }

        public void Set<T>(string name, T value) => Set(BlackboardKey<T>.Named(name), value);

        /// <summary>値を読む。未設定なら <paramref name="fallback"/>。</summary>
        public T Get<T>(BlackboardKey<T> key, T fallback = default) =>
            GetStore<T>().Values.TryGetValue(key.Id, out var value) ? value : fallback;

        public T Get<T>(string name, T fallback = default) => Get(BlackboardKey<T>.Named(name), fallback);

        public bool TryGet<T>(BlackboardKey<T> key, out T value) =>
            GetStore<T>().Values.TryGetValue(key.Id, out value);

        public bool Has<T>(BlackboardKey<T> key) => GetStore<T>().Values.ContainsKey(key.Id);

        public bool Remove<T>(BlackboardKey<T> key)
        {
            if (!GetStore<T>().Values.Remove(key.Id)) return false;

            ValueChanged?.Invoke(key.Name);
            return true;
        }

        /// <summary>未設定なら <paramref name="factory"/> で作って登録し、その値を返す。</summary>
        public T GetOrCreate<T>(BlackboardKey<T> key, Func<T> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var values = GetStore<T>().Values;
            if (values.TryGetValue(key.Id, out var existing)) return existing;

            var created = factory();
            values[key.Id] = created;
            ValueChanged?.Invoke(key.Name);
            return created;
        }

        /// <summary>ある型の中身だけ捨てる。</summary>
        public void ClearType<T>()
        {
            if (_stores.TryGetValue(typeof(T), out var store)) ((Store<T>)store).Values.Clear();
        }

        /// <summary>全て捨てる。</summary>
        public void Clear() => _stores.Clear();

        private Store<T> GetStore<T>()
        {
            if (_stores.TryGetValue(typeof(T), out var existing)) return (Store<T>)existing;

            var store = new Store<T>();
            _stores[typeof(T)] = store;
            return store;
        }
    }
}
