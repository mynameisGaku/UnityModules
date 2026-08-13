using UnityEngine;

namespace Containers
{
    /// <summary>
    /// オブジェクトごとの <see cref="Component.GetComponent{T}"/> の結果を覚えておく。
    /// <para>
    /// <c>GetComponent</c> は呼ぶたびにオブジェクトのコンポーネント一覧を舐める。1 回なら問題ないが
    /// ループの中では無駄になる。覚えること自体は簡単で、<b>安全に</b>覚えることがこの型の価値 ——
    /// 素直に書いた <c>Dictionary&lt;GameObject, T&gt;</c> は、キーが破棄された分だけ漏れる。
    /// ここでは漏れない <see cref="UnityObjectMap{TKey,TValue}"/> を土台にしている。
    /// </para>
    /// </summary>
    public static class ComponentCache<T> where T : Component
    {
        private static readonly UnityObjectMap<GameObject, T> Cache = new UnityObjectMap<GameObject, T>();

        /// <summary>覚えているエントリ数。まだ掃除していない分を含む。</summary>
        public static int Count => Cache.Count;

        /// <summary>
        /// <paramref name="target"/> の持つコンポーネント。1 度引いたら覚えておく。
        /// 持っていない場合は null を返し、<b>その結果は覚えない</b> ——
        /// あとから <c>AddComponent</c> される可能性があるため。
        /// </summary>
        /// <param name="target">探す対象。</param>
        public static T Get(GameObject target)
        {
            if (target == null) return null;

            if (Cache.TryGetValue(target, out var cached) && cached != null) return cached;

            var component = target.GetComponent<T>();
            if (component != null) Cache.Set(target, component);
            return component;
        }

        /// <summary>コンポーネントから引く版。</summary>
        /// <param name="target">探す対象。</param>
        public static T Get(Component target) => target == null ? null : Get(target.gameObject);

        /// <summary>コンポーネントを引く。持っていなければ false。</summary>
        /// <param name="target">探す対象。</param>
        /// <param name="component">見つかったコンポーネント。</param>
        public static bool TryGet(GameObject target, out T component)
        {
            component = Get(target);
            return component != null;
        }

        /// <summary>1 件だけ忘れる。実行時にコンポーネントを付け外ししたときに呼ぶ。</summary>
        /// <param name="target">忘れる対象。</param>
        public static void Invalidate(GameObject target)
        {
            if (target != null) Cache.Remove(target);
        }

        /// <summary>全て忘れる。</summary>
        public static void Clear() => Cache.Clear();

        /// <summary>GameObject が破棄されたエントリを落とす。落とした数を返す。</summary>
        public static int Compact() => Cache.Compact();
    }

    /// <summary>呼び出し側が拡張メソッドとして書けるようにする糖衣。</summary>
    public static class ComponentCacheExtensions
    {
        /// <summary><see cref="GameObject.GetComponent{T}"/> のキャッシュ版。</summary>
        /// <param name="target">探す対象。</param>
        public static T GetComponentCached<T>(this GameObject target) where T : Component =>
            ComponentCache<T>.Get(target);

        /// <summary><see cref="Component.GetComponent{T}"/> のキャッシュ版。</summary>
        /// <param name="target">探す対象。</param>
        public static T GetComponentCached<T>(this Component target) where T : Component =>
            ComponentCache<T>.Get(target);
    }
}
