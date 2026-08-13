using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Containers
{
    /// <summary>プールされるコンポーネントが任意で受け取れる、生成・返却の通知。</summary>
    public interface IPoolable
    {
        /// <summary>有効化と親付け替えが済み、呼び出し側に渡される直前に呼ばれる。</summary>
        void OnSpawned();

        /// <summary>無効化して返却される直前に呼ばれる。状態のリセットはここで行う。</summary>
        void OnDespawned();
    }

    /// <summary>
    /// プレハブのインスタンスをプールする。
    /// <para>
    /// Unity 標準の <c>UnityEngine.Pool.ObjectPool&lt;T&gt;</c> は<b>素の C# オブジェクト用</b>の
    /// 汎用プールで、プレハブも有効化も親子関係も Transform も知らない。結局どのプロジェクトも
    /// この層を自分で書くことになる。ここがその層 —— プレハブからの生成、アクティブの切り替え、
    /// ヒエラルキーの整理、<see cref="IPoolable"/> の通知までを引き受ける。
    /// </para>
    /// </summary>
    public sealed class GameObjectPool<T> : IDisposable where T : Component
    {
        /// <summary>
        /// 参照そのものが同じかだけを見る比較子。
        /// <para>
        /// <see cref="Object.Equals(object)"/> は破棄済みのオブジェクトを「null と等しい」扱いにするため、
        /// 素の <see cref="HashSet{T}"/> に入れると<b>別々に破棄された 2 つが同一視されうる</b>。
        /// 貸出台帳では実体の同一性だけが必要なので、参照で比較する。
        /// </para>
        /// </summary>
        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(T a, T b) => ReferenceEquals(a, b);

            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _idle;
        private readonly HashSet<T> _live = new HashSet<T>(ReferenceComparer.Instance);
        private readonly int _maxIdle;
        private readonly bool _worldPositionStays;

        private int _totalCreated;

        /// <summary>プレハブを指定してプールを作る。</summary>
        /// <param name="prefab">生成元のプレハブ。</param>
        /// <param name="prewarm">あらかじめ作っておくインスタンス数。</param>
        /// <param name="parent">未使用インスタンスを置いておく親。null 可。</param>
        /// <param name="maxIdle">
        /// 保持しておく待機インスタンスの上限。超えた返却は破棄する。
        /// 一度のピークがセッション中ずっとメモリを占有し続けるのを防ぐため。
        /// </param>
        /// <param name="worldPositionStays">親を付け替えるときにワールド座標を維持するか。</param>
        public GameObjectPool(T prefab, int prewarm = 0, Transform parent = null, int maxIdle = 256, bool worldPositionStays = false)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _maxIdle = Mathf.Max(0, maxIdle);
            _worldPositionStays = worldPositionStays;
            _idle = new Stack<T>(Mathf.Max(prewarm, 4));

            Prewarm(prewarm);
        }

        /// <summary>今貸し出しているインスタンス数。</summary>
        public int LiveCount => _live.Count;

        /// <summary>待機している再利用可能なインスタンス数。</summary>
        public int IdleCount => _idle.Count;

        /// <summary>これまでに生成した総数。増え続けるなら、どこかが返却していない。</summary>
        public int TotalCreated => _totalCreated;

        /// <summary>破棄済みか。</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>あらかじめ生成しておき、最初の取得でカクつかないようにする。</summary>
        /// <param name="count">生成する数。</param>
        public void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _idle.Push(instance);
            }
        }

        /// <summary>原点にインスタンスを取り出す。</summary>
        public T Get() => Get(Vector3.zero, Quaternion.identity, _parent);

        /// <summary>位置と回転を指定して取り出す。</summary>
        /// <param name="position">配置位置。</param>
        /// <param name="rotation">配置回転。</param>
        public T Get(Vector3 position, Quaternion rotation) => Get(position, rotation, _parent);

        /// <summary>位置・回転・親を指定して取り出す。</summary>
        /// <param name="position">配置位置。</param>
        /// <param name="rotation">配置回転。</param>
        /// <param name="parent">付け替える親。</param>
        public T Get(Vector3 position, Quaternion rotation, Transform parent)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(GameObjectPool<T>));

            T instance = null;

            // 待機中のインスタンスはシーンのアンロードで消えていることがあるので飛ばす。
            while (_idle.Count > 0 && instance == null) instance = _idle.Pop();

            if (instance == null) instance = CreateInstance();

            var transform = instance.transform;
            if (transform.parent != parent) transform.SetParent(parent, _worldPositionStays);
            transform.SetPositionAndRotation(position, rotation);

            instance.gameObject.SetActive(true);
            _live.Add(instance);

            if (instance is IPoolable poolable) poolable.OnSpawned();
            return instance;
        }

        /// <summary>
        /// インスタンスを返す。このプールが配ったものでない場合や二重返却は、
        /// プールを壊す代わりに無視する。
        /// </summary>
        /// <param name="instance">返すインスタンス。</param>
        public bool Release(T instance)
        {
            // Unity の == は破棄済みを null 扱いするので、参照同一性で判定する。
            // ここで弾いてしまうと、外部で Destroy されたインスタンスが _live に残り続け、
            // LiveCount が永久にずれてマネージド側の殻も解放されない。
            if (ReferenceEquals(instance, null)) return false;
            if (!_live.Remove(instance)) return false;

            // 破棄済みなら台帳から外すだけでよい。以降の操作は例外になる。
            if (instance == null) return true;

            if (instance is IPoolable poolable) poolable.OnDespawned();

            if (IsDisposed || _idle.Count >= _maxIdle)
            {
                Object.Destroy(instance.gameObject);
                return true;
            }

            instance.gameObject.SetActive(false);
            if (instance.transform.parent != _parent) instance.transform.SetParent(_parent, _worldPositionStays);
            _idle.Push(instance);
            return true;
        }

        /// <summary>貸し出し中を全て返す。ウェーブの切り替えやシーン終了時に使う。</summary>
        public void ReleaseAll()
        {
            if (_live.Count == 0) return;

            var snapshot = new T[_live.Count];
            _live.CopyTo(snapshot);
            foreach (var instance in snapshot) Release(instance);
        }

        /// <summary>待機中のインスタンスを破棄する。貸し出し中には触れない。</summary>
        public void TrimExcess()
        {
            while (_idle.Count > 0)
            {
                var instance = _idle.Pop();
                if (instance != null) Object.Destroy(instance.gameObject);
            }
        }

        /// <summary>全て返却・破棄し、以後の取得を禁止する。</summary>
        public void Dispose()
        {
            if (IsDisposed) return;

            IsDisposed = true;
            ReleaseAll();
            TrimExcess();
            _live.Clear();
        }

        private T CreateInstance()
        {
            var instance = Object.Instantiate(_prefab, _parent);
            instance.name = $"{_prefab.name} ({_totalCreated})";
            _totalCreated++;
            return instance;
        }
    }

    /// <summary>
    /// スコープを抜けたら自動的に返却されるプール取得。
    /// 生存期間がコードブロックと一致する生成に使う。
    /// </summary>
    public readonly struct PooledInstance<T> : IDisposable where T : Component
    {
        private readonly GameObjectPool<T> _pool;

        /// <summary>プールとインスタンスを結びつける。</summary>
        /// <param name="pool">返却先のプール。</param>
        /// <param name="instance">借りているインスタンス。</param>
        public PooledInstance(GameObjectPool<T> pool, T instance)
        {
            _pool = pool;
            Instance = instance;
        }

        /// <summary>借りているインスタンス。</summary>
        public T Instance { get; }

        /// <summary>インスタンスをプールに返す。</summary>
        public void Dispose() => _pool?.Release(Instance);

        /// <summary><see cref="Instance"/> への暗黙変換。</summary>
        public static implicit operator T(PooledInstance<T> pooled) => pooled.Instance;
    }
}
