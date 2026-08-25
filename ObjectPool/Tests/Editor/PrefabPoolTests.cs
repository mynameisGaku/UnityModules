// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ObjectPool.Editor.Tests
{
    /// <summary>spawn、release、preload、trim、disposeの契約と統計整合を実際のGameObjectで確認する。</summary>
    internal sealed class PrefabPoolTests
    {
        private readonly List<GameObject> _createdPrefabs = new List<GameObject>();

        [TearDown]
        public void DestroyTestObjects()
        {
            foreach (var marker in Object.FindObjectsByType<PooledInstanceMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (marker != null) Object.DestroyImmediate(marker.gameObject);
            }

            foreach (var prefab in _createdPrefabs)
            {
                if (prefab != null) Object.DestroyImmediate(prefab);
            }

            _createdPrefabs.Clear();
        }

        [Test]
        public void SpawnReleaseSpawn_ReusesSameInstanceUnderLifo_AndCountsReused()
        {
            var pool = CreatePool(new PrefabPoolSettings(0, 8, 0, PoolReuseOrder.Lifo));

            Assert.That(pool.TrySpawn(out var first, out var spawnError), Is.True, spawnError.ToString());
            var firstMarker = first.GetComponent<PooledInstanceMarker>();
            Assert.That(firstMarker.Generation, Is.EqualTo(0));
            Assert.That(firstMarker.IsReleased, Is.False);
            Assert.That(firstMarker.PoolId, Is.EqualTo(pool.PoolId));

            Assert.That(pool.TryRelease(first, out var releaseError), Is.True, releaseError.ToString());
            Assert.That(first.activeSelf, Is.False);

            Assert.That(pool.TrySpawn(out var second, out _), Is.True);
            Assert.That(second, Is.SameAs(first), "Lifoでは最後に返却したinstanceを再取得する。");
            Assert.That(second.activeSelf, Is.True);
            Assert.That(pool.ReusedTotalCount, Is.EqualTo(1));
            Assert.That(pool.CreatedTotalCount, Is.EqualTo(1));
            Assert.That(second.GetComponent<PooledInstanceMarker>().Generation, Is.EqualTo(1));
            Assert.That(second.GetComponent<PooledInstanceMarker>().IsReleased, Is.False);
        }

        [Test]
        public void FifoOrder_TakesOldestReleasedInstanceFirst()
        {
            var pool = CreatePool(new PrefabPoolSettings(0, 8, 0, PoolReuseOrder.Fifo));

            Assert.That(pool.TrySpawn(out var first, out _), Is.True);
            Assert.That(pool.TrySpawn(out var second, out _), Is.True);
            Assert.That(pool.TryRelease(first, out _), Is.True);
            Assert.That(pool.TryRelease(second, out _), Is.True);

            Assert.That(pool.TrySpawn(out var respawnFirst, out _), Is.True);
            Assert.That(respawnFirst, Is.SameAs(first), "Fifoでは最も古く返却したinstanceを先に取得する。");
            Assert.That(pool.TrySpawn(out var respawnSecond, out _), Is.True);
            Assert.That(respawnSecond, Is.SameAs(second));
            Assert.That(pool.ReusedTotalCount, Is.EqualTo(2));
        }

        [Test]
        public void ActiveLimit_BlocksThirdSpawn_ButAllowsReuseAfterRelease()
        {
            var pool = CreatePool(new PrefabPoolSettings(2, 8, 0, PoolReuseOrder.Lifo));

            Assert.That(pool.TrySpawn(out var first, out _), Is.True);
            Assert.That(pool.TrySpawn(out var second, out _), Is.True);
            Assert.That(pool.TrySpawn(out var third, out var error), Is.False);
            Assert.That(third, Is.Null);
            Assert.That(error, Is.EqualTo(PoolError.ActiveLimitReached));

            Assert.That(pool.TryRelease(first, out _), Is.True);
            Assert.That(pool.TrySpawn(out var reused, out _), Is.True, "idle再利用は上限判定を受けない。");
            Assert.That(reused, Is.SameAs(first));
        }

        [Test]
        public void Release_AfterExternalDestroy_ReturnsInstanceExternallyDestroyed()
        {
            var pool = CreatePool(null);

            Assert.That(pool.TrySpawn(out var instance, out _), Is.True);
            Object.DestroyImmediate(instance);

            Assert.That(pool.TryRelease(instance, out var error), Is.False);
            Assert.That(error, Is.EqualTo(PoolError.InstanceExternallyDestroyed));
        }

        [Test]
        public void Release_KeepsInstanceWhenDestroyedEntriesFreeUpIdleCapacity()
        {
            var pool = CreatePool(new PrefabPoolSettings(0, 2, 0, PoolReuseOrder.Lifo));

            Assert.That(pool.TrySpawn(out var first, out _), Is.True);
            Assert.That(pool.TrySpawn(out var second, out _), Is.True);
            Assert.That(pool.TrySpawn(out var third, out _), Is.True);
            Assert.That(pool.TryRelease(first, out _), Is.True);
            Assert.That(pool.TryRelease(second, out _), Is.True);
            Object.DestroyImmediate(first);

            Assert.That(pool.TryRelease(third, out var error), Is.True, error.ToString());
            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(third == null, Is.False, "idle上限の判定が破壊済みentryを数え、生存instanceを破壊しました。");
        }

        [Test]
        public void Constructor_PrefabAlreadyContainingMarker_ThrowsArgumentException()
        {
            var prefab = CreatePrefab("MarkedPrefab");
            prefab.AddComponent<PooledInstanceMarker>();

            Assert.Throws<System.ArgumentException>(() => new PrefabPool(prefab, null));
        }

        [Test]
        public void Release_ForeignOrUnmarkedInstance_ReturnsForeignInstance()
        {
            var prefab = CreatePrefab("ForeignPrefab");
            var owner = new PrefabPool(prefab, null);
            var other = new PrefabPool(prefab, null);
            Assert.That(owner.PoolId, Is.Not.EqualTo(other.PoolId), "pool idは一意に発番する。");

            Assert.That(owner.TrySpawn(out var instance, out _), Is.True);
            Assert.That(other.TryRelease(instance, out var foreignError), Is.False);
            Assert.That(foreignError, Is.EqualTo(PoolError.ForeignInstance));

            var unmarked = CreatePrefab("Unmarked");
            Assert.That(owner.TryRelease(unmarked, out var unmarkedError), Is.False);
            Assert.That(unmarkedError, Is.EqualTo(PoolError.ForeignInstance));
        }

        [Test]
        public void Release_NullAndDoubleRelease_ReportDistinctErrors()
        {
            var pool = CreatePool(null);

            Assert.That(pool.TryRelease(null, out var nullError), Is.False);
            Assert.That(nullError, Is.EqualTo(PoolError.NullInstance));

            Assert.That(pool.TrySpawn(out var instance, out _), Is.True);
            Assert.That(pool.TryRelease(instance, out _), Is.True);
            Assert.That(pool.TryRelease(instance, out var againError), Is.False);
            Assert.That(againError, Is.EqualTo(PoolError.AlreadyReleased));
        }

        [Test]
        public void Preload_NegativeCountFails_AndPositiveCountClampsToMaximumIdle()
        {
            var pool = CreatePool(new PrefabPoolSettings(0, 3, 0, PoolReuseOrder.Lifo));

            Assert.That(pool.Preload(-1, out var failedCreated, out var negativeError), Is.False);
            Assert.That(negativeError, Is.EqualTo(PoolError.NegativePreloadCount));
            Assert.That(failedCreated, Is.EqualTo(0));

            Assert.That(pool.Preload(10, out var created, out var error), Is.True, error.ToString());
            Assert.That(created, Is.EqualTo(3), "生成数はMaximumIdleCountで打ち切られる。");
            Assert.That(pool.IdleCount, Is.EqualTo(3));
            Assert.That(pool.CreatedTotalCount, Is.EqualTo(3));

            Assert.That(pool.Preload(10, out var extraCreated, out _), Is.True);
            Assert.That(extraCreated, Is.EqualTo(0), "上限済みなら追加生成しない。");
        }

        [Test]
        public void Preload_RespectsExistingIdleCapacity()
        {
            var pool = CreatePool(new PrefabPoolSettings(0, 3, 0, PoolReuseOrder.Lifo));

            Assert.That(pool.TrySpawn(out var instance, out _), Is.True);
            Assert.That(pool.TryRelease(instance, out _), Is.True);

            Assert.That(pool.Preload(5, out var created, out _), Is.True);
            Assert.That(created, Is.EqualTo(2), "既存idle分を含めてMaximumIdleCountまでとする。");
            Assert.That(pool.IdleCount, Is.EqualTo(3));
        }

        [Test]
        public void PreloadInitial_UsesSettingsInitialPreloadCount()
        {
            var pool = CreatePool(new PrefabPoolSettings(0, 8, 5, PoolReuseOrder.Lifo));

            Assert.That(pool.IdleCount, Is.EqualTo(0), "constructorは自動生成しない。");

            Assert.That(pool.PreloadInitial(out var created, out var error), Is.True, error.ToString());
            Assert.That(created, Is.EqualTo(5));
            Assert.That(pool.IdleCount, Is.EqualTo(5));
        }

        [Test]
        public void TrimIdle_RemovesOldestFirst_AndReportsActualRemovedCount()
        {
            var pool = CreatePool(new PrefabPoolSettings(0, 8, 0, PoolReuseOrder.Fifo));

            pool.Preload(4, out _, out _);
            Assert.That(pool.TrimIdle(2, out var error), Is.EqualTo(2));
            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(pool.CreatedTotalCount, Is.EqualTo(4), "trimは生成累積を変更しない。");

            Assert.That(pool.TrimIdle(99, out _), Is.EqualTo(2));
            Assert.That(pool.IdleCount, Is.EqualTo(0));

            Assert.That(pool.TrimIdle(-1, out var negativeError), Is.EqualTo(0));
            Assert.That(negativeError, Is.EqualTo(PoolError.NegativeTrimCount));
        }

        [Test]
        public void ClearIdle_DestroysEveryIdleInstance_AndReturnsDestroyedCount()
        {
            var pool = CreatePool(null);

            Assert.That(pool.TrySpawn(out var active, out _), Is.True);
            pool.Preload(5, out _, out _);

            Assert.That(pool.ClearIdle(), Is.EqualTo(5));
            Assert.That(pool.IdleCount, Is.EqualTo(0));
            Assert.That(pool.ActiveCount, Is.EqualTo(1), "取り出し中のinstanceには触れない。");
            Assert.That(active.activeInHierarchy, Is.True);
        }

        [Test]
        public void Dispose_DestroysIdleOnly_AndBlocksFurtherOperations()
        {
            var pool = CreatePool(null);

            Assert.That(pool.TrySpawn(out var active, out _), Is.True);
            pool.Preload(3, out _, out _);

            pool.Dispose();
            pool.Dispose();

            Assert.That(pool.IsDisposed, Is.True);
            Assert.That(pool.IdleCount, Is.EqualTo(0));
            Assert.That(active == null, Is.False, "取り出し中のinstanceはDispose後も生存する。");

            Assert.That(pool.TrySpawn(out var spawned, out var spawnError), Is.False);
            Assert.That(spawned, Is.Null);
            Assert.That(spawnError, Is.EqualTo(PoolError.PoolDisposed));

            Assert.That(pool.TryRelease(active, out var releaseError), Is.False);
            Assert.That(releaseError, Is.EqualTo(PoolError.PoolDisposed));

            Assert.That(pool.Preload(1, out var created, out var preloadError), Is.False);
            Assert.That(preloadError, Is.EqualTo(PoolError.PoolDisposed));
            Assert.That(created, Is.EqualTo(0));

            Assert.That(pool.TrimIdle(1, out var trimError), Is.EqualTo(0));
            Assert.That(trimError, Is.EqualTo(PoolError.PoolDisposed));
        }

        [Test]
        public void Spawn_AppliesPositionRotationAndParent()
        {
            var pool = CreatePool(null);
            var parent = CreatePrefab("Parent");

            var position = new Vector3(3f, 4f, 5f);
            var rotation = Quaternion.Euler(15f, 30f, 45f);
            Assert.That(pool.TrySpawn(position, rotation, parent.transform, out var instance, out _), Is.True);

            Assert.That(instance.transform.parent, Is.EqualTo(parent.transform));
            Assert.That(instance.transform.position, Is.EqualTo(position).Within(0.0001f));
            Assert.That(Quaternion.Angle(instance.transform.rotation, rotation), Is.EqualTo(0f).Within(0.01f));
            Assert.That(instance.activeSelf, Is.True);
        }

        [Test]
        public void Statistics_TrackLifecycleConsistently()
        {
            var pool = CreatePool(null);

            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.IdleCount, Is.EqualTo(0));
            Assert.That(pool.SpawnedTotalCount, Is.EqualTo(0));
            Assert.That(pool.ReleasedTotalCount, Is.EqualTo(0));

            Assert.That(pool.TrySpawn(out var first, out _), Is.True);
            Assert.That(pool.TrySpawn(out var second, out _), Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(2));
            Assert.That(pool.CreatedTotalCount, Is.EqualTo(2));
            Assert.That(pool.SpawnedTotalCount, Is.EqualTo(pool.CreatedTotalCount + pool.ReusedTotalCount));

            Assert.That(pool.TryRelease(first, out _), Is.True);
            Assert.That(pool.ReleasedTotalCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.IdleCount, Is.EqualTo(1));

            Assert.That(pool.TrySpawn(out var reused, out _), Is.True);
            Assert.That(reused, Is.SameAs(first));
            Assert.That(pool.ReusedTotalCount, Is.EqualTo(1));
            Assert.That(pool.CreatedTotalCount, Is.EqualTo(2));
            Assert.That(pool.SpawnedTotalCount, Is.EqualTo(3));
            Assert.That(pool.Prefab, Is.Not.Null);
            Assert.That(pool.Settings, Is.EqualTo(PrefabPoolSettings.Default));
        }

        private PrefabPool CreatePool(PrefabPoolSettings settings)
        {
            return new PrefabPool(CreatePrefab($"Prefab_{_createdPrefabs.Count}"), settings);
        }

        private GameObject CreatePrefab(string name)
        {
            var prefab = new GameObject(name);
            _createdPrefabs.Add(prefab);
            return prefab;
        }
    }
}
