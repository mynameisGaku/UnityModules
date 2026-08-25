// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ObjectPool.Samples
{
    /// <summary>ownerとしてPrefabPoolを保持し、spawn、release、preload、clearと統計をGUIで試すsample。</summary>
    [AddComponentMenu("StudioGaku/Object Pool Basics Controller")]
    public sealed class ObjectPoolBasicsController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("poolへ登録するprefab。未設定なら起動時にCube primitiveを生成して使用します。")]
        private GameObject _prefab;

        private PrefabPool _pool;
        private readonly List<GameObject> _activeInstances = new List<GameObject>();
        private string _lastResult = "not started";

        /// <summary>所有するpool。Startが完了する前はnull。</summary>
        public PrefabPool Pool => _pool;

        /// <summary>最後に実行した操作結果。</summary>
        public string LastResult => _lastResult;

        /// <summary>GUIへ出す統計と操作結果の1行テキスト。</summary>
        public string StatusText
        {
            get
            {
                if (_pool == null) return "pool: not started";
                return $"{BuildStatsText()} | last: {_lastResult}";
            }
        }

        /// <summary>prefabを決めてpoolを生成する。GameObjectはまだ生成しない。</summary>
        private void Start()
        {
            var prefab = _prefab;
            if (prefab == null)
            {
                prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prefab.name = "ObjectPoolBasicsCube";
            }

            _pool = new PrefabPool(prefab, PrefabPoolSettings.Default);
            _lastResult = "ready";
        }

        /// <summary>ownerの終了時にidleを破棄する。取り出し中のinstanceは生存するため、必要なら先にReleaseOldestを呼ぶ。</summary>
        private void OnDestroy()
        {
            if (_pool != null)
            {
                _pool.Dispose();
                _pool = null;
            }
        }

        /// <summary>instanceを1つspawnし、返却順の追跡listへ追加する。</summary>
        public void SpawnOne()
        {
            if (!EnsureReady("SpawnOne")) return;
            var position = new Vector3(_activeInstances.Count % 8, 0.5f, 0f);
            if (_pool.TrySpawn(position, Quaternion.identity, transform, out var instance, out var error))
            {
                _activeInstances.Add(instance);
                _lastResult = "SpawnOne: success";
            }
            else
            {
                _lastResult = $"SpawnOne: {error}";
            }
        }

        /// <summary>最も古くspawnしたinstanceをidleへ返却する。</summary>
        public void ReleaseOldest()
        {
            if (!EnsureReady("ReleaseOldest")) return;
            while (_activeInstances.Count > 0)
            {
                var oldest = _activeInstances[0];
                _activeInstances.RemoveAt(0);
                if (oldest == null) continue;
                _lastResult = _pool.TryRelease(oldest, out var error)
                    ? $"ReleaseOldest: generation {oldest.GetComponent<PooledInstanceMarker>().Generation} released"
                    : $"ReleaseOldest: {error}";
                return;
            }

            _lastResult = "ReleaseOldest: no active instance";
        }

        /// <summary>idleを10個まで事前生成する。</summary>
        public void PreloadTen()
        {
            if (!EnsureReady("PreloadTen")) return;
            _lastResult = _pool.Preload(10, out var created, out var error)
                ? $"PreloadTen: created {created}"
                : $"PreloadTen: {error}";
        }

        /// <summary>idleを全て破壊する。</summary>
        public void ClearIdle()
        {
            if (!EnsureReady("ClearIdle")) return;
            _lastResult = $"ClearIdle: destroyed {_pool.ClearIdle()}";
        }

        private bool EnsureReady(string operation)
        {
            if (_pool != null) return true;
            _lastResult = $"{operation}: pool is not started";
            return false;
        }

        private string BuildStatsText()
        {
            var builder = new StringBuilder();
            builder.Append("Active=").Append(_pool.ActiveCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" Idle=").Append(_pool.IdleCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" Created=").Append(_pool.CreatedTotalCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" Reused=").Append(_pool.ReusedTotalCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" Released=").Append(_pool.ReleasedTotalCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" SpawnedTotal=").Append(_pool.SpawnedTotalCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" Disposed=").Append(_pool.IsDisposed);
            return builder.ToString();
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10f, 10f, 480f, 24f), "Object Pool Basics");

            if (GUI.Button(new Rect(10f, 40f, 140f, 30f), "Spawn One")) SpawnOne();
            if (GUI.Button(new Rect(10f, 76f, 140f, 30f), "Release Oldest")) ReleaseOldest();
            if (GUI.Button(new Rect(10f, 112f, 140f, 30f), "Preload x10")) PreloadTen();
            if (GUI.Button(new Rect(10f, 148f, 140f, 30f), "Clear Idle")) ClearIdle();

            GUI.Label(new Rect(160f, 40f, 700f, 24f), StatusText);
            GUI.Label(
                new Rect(160f, 64f, 700f, 60f),
                _pool == null
                    ? "pool will start with a Cube primitive when the prefab field is empty."
                    : $"prefab={_pool.Prefab.name}, maxActive={_pool.Settings.MaximumActiveCount}, maxIdle={_pool.Settings.MaximumIdleCount}, order={_pool.Settings.ReuseOrder}");
        }
    }
}
