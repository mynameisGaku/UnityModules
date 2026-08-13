using System;
using System.Collections.Generic;

namespace Containers
{
    /// <summary>
    /// 「型ごとに 1 個」を保持するコンテナ。<c>Get&lt;T&gt;()</c> で引く。
    /// <para>
    /// サービスロケータ、型別キャッシュ、システム登録簿など、キーが実質「型そのもの」になる場面向け。
    /// <see cref="Blackboard"/> が「同じ型の値を名前で複数持つ」のに対し、こちらは「型 1 つにつき値 1 つ」。
    /// </para>
    /// <para>
    /// <c>Dictionary&lt;Type, object&gt;</c> と違い <b>値型を入れてもボクシングしない</b>。
    /// ジェネリック型の静的フィールドが型引数ごとに独立する性質を使い、型ごとに専用の箱を持たせている。
    /// </para>
    /// </summary>
    public sealed class TypeMap
    {
        private sealed class Box<T>
        {
            public T Value;
            public bool HasValue;
        }

        private readonly Dictionary<Type, object> _boxes = new Dictionary<Type, object>();

        public int Count => _boxes.Count;

        /// <summary>登録されている型の一覧。</summary>
        public Dictionary<Type, object>.KeyCollection Types => _boxes.Keys;

        public void Set<T>(T value)
        {
            var box = GetBox<T>();
            box.Value = value;
            box.HasValue = true;
        }

        public T Get<T>(T fallback = default)
        {
            var box = GetBox<T>();
            return box.HasValue ? box.Value : fallback;
        }

        public bool TryGet<T>(out T value)
        {
            var box = GetBox<T>();
            value = box.Value;
            return box.HasValue;
        }

        public bool Has<T>() => _boxes.TryGetValue(typeof(T), out var box) && ((Box<T>)box).HasValue;

        /// <summary>未登録なら <paramref name="factory"/> で作って登録する。サービスの遅延生成に使う。</summary>
        public T GetOrCreate<T>(Func<T> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var box = GetBox<T>();
            if (box.HasValue) return box.Value;

            box.Value = factory();
            box.HasValue = true;
            return box.Value;
        }

        public bool Remove<T>()
        {
            if (!_boxes.TryGetValue(typeof(T), out var boxObject)) return false;

            var box = (Box<T>)boxObject;
            if (!box.HasValue) return false;

            box.Value = default;
            box.HasValue = false;
            return true;
        }

        public void Clear() => _boxes.Clear();

        private Box<T> GetBox<T>()
        {
            if (_boxes.TryGetValue(typeof(T), out var existing)) return (Box<T>)existing;

            var box = new Box<T>();
            _boxes[typeof(T)] = box;
            return box;
        }
    }
}
