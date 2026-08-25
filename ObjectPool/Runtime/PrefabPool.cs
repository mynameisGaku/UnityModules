// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ObjectPool
{
    /// <summary>
    /// 1つのprefabの生成、再利用、返却、整理を所有するpool。
    /// 寿命が明確なownerがnewして保持し、不要になった時点で<see cref="Dispose"/>する。
    /// 全public操作はUnity main threadから呼ぶこと。
    /// </summary>
    public sealed class PrefabPool : IDisposable
    {
        private static int _nextPoolId;

        private readonly GameObject _prefab;
        private readonly PrefabPoolSettings _settings;
        private readonly List<GameObject> _idle = new List<GameObject>();
        private readonly List<GameObject> _active = new List<GameObject>();
        private long _createdTotalCount;
        private long _reusedTotalCount;
        private long _releasedTotalCount;
        private bool _disposed;

        /// <summary>指定prefabと設定でpoolを作る。この時点でGameObjectは生成しない。</summary>
        /// <param name="prefab">複製元のprefab。nullは許可しない。</param>
        /// <param name="settings">設定。nullの場合は<see cref="PrefabPoolSettings.Default"/>を使う。</param>
        /// <exception cref="ArgumentNullException"><paramref name="prefab"/>がnull。</exception>
        /// <exception cref="ArgumentException"><paramref name="prefab"/>が既に<see cref="PooledInstanceMarker"/>をrootへ含む。markerはpoolが自動付与するため事前添付は不正です。</exception>
        public PrefabPool(GameObject prefab, PrefabPoolSettings settings = null)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (prefab.GetComponent<PooledInstanceMarker>() != null)
            {
                throw new ArgumentException("prefab must not contain PooledInstanceMarker; the pool attaches it automatically.", nameof(prefab));
            }
            _prefab = prefab;
            _settings = settings ?? PrefabPoolSettings.Default;
            PoolId = Interlocked.Increment(ref _nextPoolId);
        }

        /// <summary>他poolと照合するための一意id。<see cref="PooledInstanceMarker.PoolId"/>と一致する。</summary>
        public int PoolId { get; }

        /// <summary>複製元のprefab。constructorが成功していればnullにならない。</summary>
        public GameObject Prefab => _prefab;

        /// <summary>このpoolの変更不能な設定。</summary>
        public PrefabPoolSettings Settings => _settings;

        /// <summary>破棄済みの場合はtrue。破棄後のspawn、release、preload、trimは失敗する。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>現在取り出されているinstance数。</summary>
        public int ActiveCount => _active.Count;

        /// <summary>現在idleとして保持しているinstance数。外部破壊済みの死んだentryを除いた実数。</summary>
        public int IdleCount
        {
            get
            {
                RemoveDestroyedIdle();
                return _idle.Count;
            }
        }

        /// <summary>これまでに新規生成した累積instance数。preload分を含む。</summary>
        public long CreatedTotalCount => _createdTotalCount;

        /// <summary>これまでにidleから再利用した累積instance数。</summary>
        public long ReusedTotalCount => _reusedTotalCount;

        /// <summary>取り出したinstanceを返却できた累積回数。preloadやtrimによる破壊は含まない。</summary>
        public long ReleasedTotalCount => _releasedTotalCount;

        /// <summary>累積取出し数。<see cref="CreatedTotalCount"/>と<see cref="ReusedTotalCount"/>の合計。</summary>
        public long SpawnedTotalCount => _createdTotalCount + _reusedTotalCount;

        /// <summary>原点、identity、親なしでinstanceを取り出す。</summary>
        /// <param name="instance">成功時に有効化されたinstance。失敗時はnull。</param>
        /// <param name="error">失敗理由。成功時は<see cref="PoolError.None"/>。</param>
        /// <returns>取り出せた場合はtrue。</returns>
        public bool TrySpawn(out GameObject instance, out PoolError error)
        {
            return TrySpawn(Vector3.zero, Quaternion.identity, null, out instance, out error);
        }

        /// <summary>
        /// instanceを取り出す。idleがあれば<see cref="PrefabPoolSettings.ReuseOrder"/>に従って再利用し、
        /// 空なら上限内で新規生成する。上限判定は新規生成が必要な場合だけ行い、idle再利用は常に許可する。
        /// Dispose済みの場合は失敗する。
        /// </summary>
        /// <param name="position">適用するworld位置。</param>
        /// <param name="rotation">適用するworld回転。</param>
        /// <param name="parent">親Transform。nullなら親なし。</param>
        /// <param name="instance">成功時に有効化されたinstance。失敗時はnull。</param>
        /// <param name="error">失敗理由。成功時は<see cref="PoolError.None"/>。</param>
        /// <returns>取り出せた場合はtrue。</returns>
        public bool TrySpawn(Vector3 position, Quaternion rotation, Transform parent, out GameObject instance, out PoolError error)
        {
            instance = null;
            if (_disposed)
            {
                error = PoolError.PoolDisposed;
                return false;
            }

            RemoveDestroyedIdle();
            while (_idle.Count > 0)
            {
                var candidate = TakeFromIdle();
                if (candidate == null) continue;
                Activate(candidate, position, rotation, parent);
                _reusedTotalCount++;
                instance = candidate;
                error = PoolError.None;
                return true;
            }

            if (_settings.MaximumActiveCount > 0 && _active.Count >= _settings.MaximumActiveCount)
            {
                error = PoolError.ActiveLimitReached;
                return false;
            }

            var spawned = UnityEngine.Object.Instantiate(_prefab, position, rotation, parent);
            var marker = spawned.AddComponent<PooledInstanceMarker>();
            marker.Bind(PoolId);
            spawned.SetActive(true);
            _createdTotalCount++;
            _active.Add(spawned);
            instance = spawned;
            error = PoolError.None;
            return true;
        }

        /// <summary>
        /// 取り出したinstanceをidleへ返却する。
        /// C#参照としてのnullはNullInstance、Dispose後の呼出しはPoolDisposed、
        /// 外部破壊されたinstanceはInstanceExternallyDestroyed、管理外instanceはForeignInstance、
        /// 二重返却はAlreadyReleasedで失敗する。
        /// </summary>
        /// <param name="instance">返却するinstance。このpoolから取り出したものだけを受け付ける。</param>
        /// <param name="error">失敗理由。成功時は<see cref="PoolError.None"/>。</param>
        /// <returns>返却できた場合はtrue。</returns>
        public bool TryRelease(GameObject instance, out PoolError error)
        {
            if (ReferenceEquals(instance, null))
            {
                error = PoolError.NullInstance;
                return false;
            }

            if (_disposed)
            {
                error = PoolError.PoolDisposed;
                return false;
            }

            if (instance == null)
            {
                error = PoolError.InstanceExternallyDestroyed;
                return false;
            }

            var marker = instance.GetComponent<PooledInstanceMarker>();
            if (marker == null || marker.PoolId != PoolId)
            {
                error = PoolError.ForeignInstance;
                return false;
            }

            if (marker.IsReleased)
            {
                error = PoolError.AlreadyReleased;
                return false;
            }

            if (!_active.Remove(instance))
            {
                error = PoolError.ForeignInstance;
                return false;
            }

            instance.SetActive(false);
            marker.MarkReleased();
            _releasedTotalCount++;
            RemoveDestroyedIdle();
            if (_idle.Count >= _settings.MaximumIdleCount)
            {
                DestroyInstance(instance);
            }
            else
            {
                _idle.Add(instance);
            }

            error = PoolError.None;
            return true;
        }

        /// <summary>idleへinstanceを事前生成する。既存idleと合わせて<see cref="PrefabPoolSettings.MaximumIdleCount"/>までで打ち切る。</summary>
        /// <param name="count">作ろうとする数。負は失敗。</param>
        /// <param name="createdCount">実際に新規生成した数。失敗時は0。</param>
        /// <param name="error">失敗理由。成功時は<see cref="PoolError.None"/>。</param>
        /// <returns>呼出しが成立した場合はtrue。countが0や打ち切りでもtrue。</returns>
        public bool Preload(int count, out int createdCount, out PoolError error)
        {
            createdCount = 0;
            if (_disposed)
            {
                error = PoolError.PoolDisposed;
                return false;
            }

            if (count < 0)
            {
                error = PoolError.NegativePreloadCount;
                return false;
            }

            RemoveDestroyedIdle();
            var remaining = Mathf.Min(count, _settings.MaximumIdleCount - _idle.Count);
            while (remaining > 0)
            {
                var instance = UnityEngine.Object.Instantiate(_prefab);
                var marker = instance.AddComponent<PooledInstanceMarker>();
                marker.Bind(PoolId);
                marker.MarkReleased();
                instance.SetActive(false);
                _idle.Add(instance);
                _createdTotalCount++;
                createdCount++;
                remaining--;
            }

            error = PoolError.None;
            return true;
        }

        /// <summary><see cref="PrefabPoolSettings.InitialPreloadCount"/>に従いidleを事前生成する。自動生成は行わないため、ownerが明示して呼ぶ。</summary>
        /// <param name="createdCount">実際に新規生成した数。失敗時は0。</param>
        /// <param name="error">失敗理由。成功時は<see cref="PoolError.None"/>。</param>
        /// <returns>呼出しが成立した場合はtrue。</returns>
        public bool PreloadInitial(out int createdCount, out PoolError error)
        {
            return Preload(_settings.InitialPreloadCount, out createdCount, out error);
        }

        /// <summary>古くから保持しているidleから指定数を破壊する。実際に破壊できた数を返す。</summary>
        /// <param name="count">壊そうとする数。負は失敗。</param>
        /// <param name="error">失敗理由。成功時は<see cref="PoolError.None"/>。</param>
        /// <returns>実際に破壊した数。失敗時は0。</returns>
        public int TrimIdle(int count, out PoolError error)
        {
            if (_disposed)
            {
                error = PoolError.PoolDisposed;
                return 0;
            }

            if (count < 0)
            {
                error = PoolError.NegativeTrimCount;
                return 0;
            }

            RemoveDestroyedIdle();
            var removed = 0;
            while (removed < count && _idle.Count > 0)
            {
                var oldest = _idle[0];
                _idle.RemoveAt(0);
                if (oldest != null) DestroyInstance(oldest);
                removed++;
            }

            error = PoolError.None;
            return removed;
        }

        /// <summary>idleを全て破壊し、破壊した数を返す。取り出し中のinstanceには触れない。</summary>
        /// <returns>破壊したidle数。破棄済みpoolでは0。</returns>
        public int ClearIdle()
        {
            if (_disposed) return 0;
            RemoveDestroyedIdle();
            var removed = _idle.Count;
            for (var index = 0; index < _idle.Count; index++)
            {
                if (_idle[index] != null) DestroyInstance(_idle[index]);
            }

            _idle.Clear();
            return removed;
        }

        /// <summary>idleを全て破壊し、これ以上の操作を禁止する。取り出し中のinstanceは生存し、後から返却するとPoolDisposedになる。複数回呼んでも安全。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (var index = 0; index < _idle.Count; index++)
            {
                if (_idle[index] != null) DestroyInstance(_idle[index]);
            }

            _idle.Clear();
        }

        private GameObject TakeFromIdle()
        {
            int index;
            if (_settings.ReuseOrder == PoolReuseOrder.Lifo)
            {
                index = _idle.Count - 1;
            }
            else
            {
                index = 0;
            }

            var instance = _idle[index];
            _idle.RemoveAt(index);
            return instance;
        }

        private void Activate(GameObject instance, Vector3 position, Quaternion rotation, Transform parent)
        {
            var marker = instance.GetComponent<PooledInstanceMarker>();
            if (marker == null)
            {
                marker = instance.AddComponent<PooledInstanceMarker>();
                marker.Bind(PoolId);
            }

            marker.MarkReused();
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            _active.Add(instance);
        }

        private void RemoveDestroyedIdle()
        {
            for (var index = _idle.Count - 1; index >= 0; index--)
            {
                if (_idle[index] == null) _idle.RemoveAt(index);
            }
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
