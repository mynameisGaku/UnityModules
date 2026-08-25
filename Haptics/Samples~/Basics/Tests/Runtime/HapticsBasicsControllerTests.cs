// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Haptics.Samples.Runtime.Tests
{
    /// <summary>NoOp環境でsample controllerの全操作が例外を出さず、表示文字列が常に得られることを確認する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class HapticsBasicsControllerTests
    {
        private GameObject _host;
        private HapticsBasicsController _sample;

        /// <summary>test所有GameObjectへcontrollerを追加し、Awakeでservice生成を済ませる。</summary>
        [SetUp]
        public void CreateSample()
        {
            _host = new GameObject("Haptics Basics Tests");
            _host.SetActive(false);
            _sample = _host.AddComponent<HapticsBasicsController>();
            _host.SetActive(true);
        }

        /// <summary>test所有objectを確実に破棄する。</summary>
        [TearDown]
        public void DestroySample()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
            _sample = null;
        }

        /// <summary>service生成直後からcapability表示がnullでない。</summary>
        [Test]
        public void StatusText_IsAvailableRightAfterCreation()
        {
            Assert.That(_sample.StatusText, Is.Not.Null);
            Assert.That(_sample.StatusText, Does.Contain("Capability="));
            Assert.That(_sample.LastResultText, Is.Not.Null);
        }

        /// <summary>NoOp環境のEditorで各intentと任意patternを呼んでも例外が出ない。</summary>
        [UnityTest]
        public IEnumerator EveryIntentAndCustomPattern_RunWithoutThrowingInNoOpEnvironment()
        {
            yield return null;

            foreach (HapticsIntent intent in Enum.GetValues(typeof(HapticsIntent)))
            {
                Assert.DoesNotThrow(() => _sample.PlayIntent(intent), $"{intent} threw.");
                Assert.That(_sample.LastResultText, Is.Not.Null);
            }

            Assert.DoesNotThrow(() => _sample.PlayCustomPattern());
            Assert.That(_sample.LastResultText, Is.Not.Null);
            yield break;
        }

        /// <summary>NoOp環境では再生は失敗理由付きで報告される。</summary>
        [Test]
        public void PlayIntent_ReportsSkipReasonInEditorEnvironment()
        {
            _sample.PlayIntent(HapticsIntent.SelectionTick);

            Assert.That(_sample.LastResultText, Is.Not.Null);
            Assert.That(_sample.LastResultText, Does.Contain("SelectionTick"));
            Assert.That(_sample.LastResultText, Does.Contain("Skipped"));
        }
    }
}
