// SPDX-License-Identifier: MIT

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ObjectPool.Samples.Runtime.Tests
{
    /// <summary>sample controllerの各操作をPlayModeで直接呼び、統計とstatus文字列を確認する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class ObjectPoolBasicsControllerTests
    {
        private GameObject _host;
        private ObjectPoolBasicsController _controller;

        /// <summary>test専用hostへcontrollerを取り付け、Startによるpool初期化を待つ。</summary>
        [UnitySetUp]
        public IEnumerator CreateController()
        {
            _host = new GameObject("Object Pool Basics Tests");
            _controller = _host.AddComponent<ObjectPoolBasicsController>();
            yield return null;

            Assert.That(_controller.Pool, Is.Not.Null, "Startでpoolが初期化されていません。");
            Assert.That(_controller.Pool.Prefab.name, Does.Contain("Cube").Or.Contain("ObjectPoolBasicsCube"), "prefab未設定時はCube primitiveへfallbackする。");
        }

        /// <summary>test専用hostと生成物を掃除する。</summary>
        [UnityTearDown]
        public IEnumerator DestroyController()
        {
            if (_host != null) Object.Destroy(_host);
            foreach (var marker in Object.FindObjectsByType<PooledInstanceMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (marker != null) Object.Destroy(marker.gameObject);
            }

            _host = null;
            _controller = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnOne_IncreasesActiveAndSpawnedTotals()
        {
            _controller.SpawnOne();

            Assert.That(_controller.LastResult, Is.EqualTo("SpawnOne: success"));
            Assert.That(_controller.Pool.ActiveCount, Is.EqualTo(1));
            Assert.That(_controller.Pool.SpawnedTotalCount, Is.EqualTo(1));
            Assert.That(_controller.StatusText, Does.StartWith("Active=1"));
            yield break;
        }

        [UnityTest]
        public IEnumerator ReleaseOldest_ReleasesFirstSpawnedInstance_AndReuseCountsIt()
        {
            _controller.SpawnOne();
            _controller.SpawnOne();
            Assert.That(_controller.Pool.ActiveCount, Is.EqualTo(2));

            _controller.ReleaseOldest();
            Assert.That(_controller.LastResult, Does.StartWith("ReleaseOldest: generation"));
            Assert.That(_controller.Pool.ReleasedTotalCount, Is.EqualTo(1));
            Assert.That(_controller.Pool.IdleCount, Is.EqualTo(1));

            _controller.SpawnOne();
            Assert.That(_controller.Pool.ReusedTotalCount, Is.EqualTo(1), "返却済みinstanceが再利用される。");
            Assert.That(_controller.Pool.CreatedTotalCount, Is.EqualTo(2), "再利用時に新規生成しない。");
            yield break;
        }

        [UnityTest]
        public IEnumerator PreloadTen_CreatesIdleWithoutSpawning()
        {
            _controller.PreloadTen();

            Assert.That(_controller.LastResult, Is.EqualTo("PreloadTen: created 10"));
            Assert.That(_controller.Pool.IdleCount, Is.EqualTo(10));
            Assert.That(_controller.Pool.CreatedTotalCount, Is.EqualTo(10));
            Assert.That(_controller.Pool.SpawnedTotalCount, Is.EqualTo(0), "preloadはspawnとして数えない。");

            _controller.SpawnOne();
            Assert.That(_controller.Pool.ReusedTotalCount, Is.EqualTo(1));
            Assert.That(_controller.Pool.CreatedTotalCount, Is.EqualTo(10), "idle在庫がある間は新規生成しない。");
            yield break;
        }

        [UnityTest]
        public IEnumerator ClearIdle_DestroysAllIdleInstances()
        {
            _controller.PreloadTen();
            _controller.ClearIdle();

            Assert.That(_controller.LastResult, Is.EqualTo("ClearIdle: destroyed 10"));
            Assert.That(_controller.Pool.IdleCount, Is.EqualTo(0));

            _controller.ReleaseOldest();
            Assert.That(_controller.LastResult, Is.EqualTo("ReleaseOldest: no active instance"));
            yield break;
        }

        [UnityTest]
        public IEnumerator Operations_BeforeStart_KeepStatusHonest()
        {
            var earlyHost = new GameObject("Object Pool Basics Early");
            var early = earlyHost.AddComponent<ObjectPoolBasicsController>();

            try
            {
                Assert.That(early.StatusText, Is.EqualTo("pool: not started"));
                early.SpawnOne();
                Assert.That(early.LastResult, Is.EqualTo("SpawnOne: pool is not started"));
                yield return null;
            }
            finally
            {
                Object.Destroy(earlyHost);
            }
        }
    }
}
