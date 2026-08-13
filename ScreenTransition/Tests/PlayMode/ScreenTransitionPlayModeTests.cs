using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScreenTransition.Tests.PlayMode
{
    /// <summary>実際のUIDocumentとPanelSettingsで、描画範囲、非スケール時間、直列化、終了処理を確かめる。</summary>
    public sealed class ScreenTransitionPlayModeTests
    {
        private const int TargetWidth = 320;
        private const int TargetHeight = 180;
        private const float GeometryTolerance = 0.1f;

        private GameObject _host;
        private UIDocument _document;
        private ScreenTransitionController _controller;
        private PanelSettings _panelSettings;
        private RenderTexture _targetTexture;

        /// <summary>固定寸法のRenderTextureへ描画する実panelとControllerを作る。</summary>
        [UnitySetUp]
        public IEnumerator CreateRealPanel()
        {
            Time.timeScale = 1f;

            _targetTexture = new RenderTexture(TargetWidth, TargetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "Screen Transition PlayMode Target",
            };
            Assert.That(_targetTexture.Create(), Is.True, "PlayMode検証用RenderTextureを作れません");

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "Screen Transition PlayMode Panel Settings";
            _panelSettings.targetTexture = _targetTexture;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.sortingOrder = 2000f;

            _host = new GameObject("Screen Transition PlayMode Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _controller = _host.AddComponent<ScreenTransitionController>();
            _host.SetActive(true);

            var root = _document.rootVisualElement;
            root.style.width = TargetWidth;
            root.style.height = TargetHeight;

            for (var i = 0; i < 4; i++) yield return null;

            Assert.That(_document.rootVisualElement, Is.Not.Null);
            Assert.That(_document.rootVisualElement.panel, Is.Not.Null, "UIDocumentが実panelへ接続されていません");
            Assert.That(FindOverlay(), Is.Not.Null, "Controllerのオーバーレイが実panelへ追加されていません");
        }

        /// <summary>停止時間を戻し、Controller、PanelSettings、RenderTextureを解放する。</summary>
        [UnityTearDown]
        public IEnumerator DestroyRealPanel()
        {
            Time.timeScale = 1f;

            if (_host != null) UnityEngine.Object.Destroy(_host);
            yield return null;

            if (_panelSettings != null) UnityEngine.Object.DestroyImmediate(_panelSettings);
            if (_targetTexture != null)
            {
                _targetTexture.Release();
                UnityEngine.Object.DestroyImmediate(_targetTexture);
            }

            _host = null;
            _document = null;
            _controller = null;
            _panelSettings = null;
            _targetTexture = null;
        }

        /// <summary>実panel全体を覆い、Reveal後にsurfaceを非表示へ戻すことを確かめる。</summary>
        [UnityTest]
        public IEnumerator CoverAndReveal_MatchRealPanelGeometryAndTerminalOpacity()
        {
            var root = _document.rootVisualElement;
            var overlay = FindOverlay();
            Assert.That(root.contentRect.width, Is.EqualTo(TargetWidth).Within(GeometryTolerance));
            Assert.That(root.contentRect.height, Is.EqualTo(TargetHeight).Within(GeometryTolerance));
            Assert.That(overlay.pickingMode, Is.EqualTo(PickingMode.Ignore));

            var result = default(ScreenTransitionResult);
            var color = new Color(0.12f, 0.48f, 0.86f, 0.75f);
            yield return WaitForResult(_controller.CoverAsync(color, 0.04f), value => result = value);
            yield return null;
            yield return null;

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(_controller.Status.Opacity, Is.EqualTo(color.a).Within(0.000001f));
            Assert.That(overlay.worldBound.xMin, Is.EqualTo(root.worldBound.xMin).Within(GeometryTolerance));
            Assert.That(overlay.worldBound.yMin, Is.EqualTo(root.worldBound.yMin).Within(GeometryTolerance));
            Assert.That(overlay.worldBound.width, Is.EqualTo(root.worldBound.width).Within(GeometryTolerance));
            Assert.That(overlay.worldBound.height, Is.EqualTo(root.worldBound.height).Within(GeometryTolerance));
            Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(overlay.resolvedStyle.backgroundColor.a, Is.EqualTo(color.a).Within(0.001f));

            yield return WaitForResult(_controller.RevealAsync(color, 0.04f), value => result = value);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(_controller.Status.Opacity, Is.EqualTo(0f).Within(0.000001f));
            Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(overlay.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>timeScaleが0でも非スケール時間でCoverが完了することを確かめる。</summary>
        [UnityTest]
        public IEnumerator Cover_CompletesWhileTimeScaleIsZero()
        {
            Time.timeScale = 0f;
            var result = default(ScreenTransitionResult);

            yield return WaitForResult(_controller.CoverAsync(Color.black, 0.06f), value => result = value);

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(_controller.Status.Opacity, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
        }

        /// <summary>0秒要求を同期的に確定し、処理中と完了通知中の再入をBusyで拒否することを確かめる。</summary>
        [UnityTest]
        public IEnumerator ZeroDurationAndReentry_ReturnDeterministicResults()
        {
            var result = default(ScreenTransitionResult);
            yield return WaitForResult(_controller.CoverAsync(Color.black, 0f), value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(_controller.Status.Opacity, Is.EqualTo(1f).Within(0.000001f));

            var active = _controller.RevealAsync(Color.black, 0.08f);
            var busy = default(ScreenTransitionResult);
            yield return WaitForResult(_controller.CoverAsync(Color.black, 0.08f), value => busy = value);
            Assert.That(busy.IsSuccess, Is.False);
            Assert.That(busy.Error, Is.EqualTo(ScreenTransitionError.Busy));
            yield return WaitForResult(active, value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);

            var callbackEntered = false;
            var callbackOperation = default(Awaitable<ScreenTransitionResult>);
            Action<ScreenTransitionResult> callback = _ =>
            {
                callbackEntered = true;
                callbackOperation = _controller.RevealAsync(Color.black, 0f);
            };
            _controller.Finished += callback;
            yield return WaitForResult(_controller.CoverAsync(Color.black, 0f), value => result = value);
            _controller.Finished -= callback;

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(callbackEntered, Is.True, "Finished通知が呼ばれていません");
            yield return WaitForResult(callbackOperation, value => busy = value);
            Assert.That(busy.IsSuccess, Is.False);
            Assert.That(busy.Error, Is.EqualTo(ScreenTransitionError.Busy));
        }

        /// <summary>Controller無効化で待機を終了し、オーバーレイをpanelと入力対象から外すことを確かめる。</summary>
        [UnityTest]
        public IEnumerator Disable_CompletesActiveRequestAndRemovesSurface()
        {
            var active = _controller.CoverAsync(Color.black, 2f);
            yield return null;

            _controller.enabled = false;

            var result = default(ScreenTransitionResult);
            yield return WaitForResult(active, value => result = value);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.ApplicationExiting));
            Assert.That(_controller.IsBusy, Is.False);
            Assert.That(FindOverlay(), Is.Null, "無効化後もオーバーレイがpanelに残っています");
        }

        /// <summary>不透明Coverの完了後も、複数frameにわたり表示状態と最前面配置を維持する。</summary>
        [UnityTest]
        public IEnumerator OpaqueCover_MaintainsResolvedSurfaceState()
        {
            var expectedColor = new Color(0.82f, 0.08f, 0.63f, 1f);
            var result = default(ScreenTransitionResult);
            yield return WaitForResult(_controller.CoverAsync(expectedColor, 0f), value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);

            for (var i = 0; i < 3; i++) yield return null;

            var overlay = FindOverlay();
            var actualColor = overlay.resolvedStyle.backgroundColor;
            Assert.That(_controller.Status.Phase, Is.EqualTo(ScreenTransitionPhase.Idle));
            Assert.That(_controller.Status.Opacity, Is.EqualTo(1f));
            Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(actualColor.r, Is.EqualTo(expectedColor.r).Within(0.001f));
            Assert.That(actualColor.g, Is.EqualTo(expectedColor.g).Within(0.001f));
            Assert.That(actualColor.b, Is.EqualTo(expectedColor.b).Within(0.001f));
            Assert.That(actualColor.a, Is.EqualTo(expectedColor.a).Within(0.001f));
            Assert.That(_document.rootVisualElement.ElementAt(_document.rootVisualElement.childCount - 1), Is.SameAs(overlay));
        }

        /// <summary>Cover完了後に兄弟要素が追加されても、次の描画までにオーバーレイをrootの最前面へ戻す。</summary>
        [UnityTest]
        public IEnumerator CoveredSurface_LaterSiblingAdded_ReturnsOverlayToFront()
        {
            var result = default(ScreenTransitionResult);
            yield return WaitForResult(_controller.CoverAsync(Color.black, 0f), value => result = value);
            Assert.That(result.IsSuccess, Is.True, result.Message);

            var root = _document.rootVisualElement;
            var overlay = FindOverlay();
            var laterSibling = new VisualElement { name = "later-sibling" };
            root.Add(laterSibling);
            Assert.That(root.ElementAt(root.childCount - 1), Is.SameAs(laterSibling));

            yield return null;
            yield return null;

            Assert.That(root.ElementAt(root.childCount - 1), Is.SameAs(overlay));
        }

        /// <summary>遷移中にUIDocumentだけを無効化すると、SurfaceUnavailableで待機を完了して表示要素を外す。</summary>
        [UnityTest]
        public IEnumerator Cover_DocumentDisabledDuringTransition_ReturnsSurfaceUnavailable()
        {
            var operation = _controller.CoverAsync(Color.black, 2f);
            yield return null;

            _document.enabled = false;

            var result = default(ScreenTransitionResult);
            yield return WaitForResult(operation, value => result = value);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.SurfaceUnavailable));
            Assert.That(_controller.IsBusy, Is.False);
            Assert.That(_controller.Status.Opacity, Is.Zero);
            Assert.That(FindOverlay(), Is.Null);
        }

        /// <summary>遷移中にPanelSettingsを外すと、SurfaceUnavailableで待機を完了して表示要素を外す。</summary>
        [UnityTest]
        public IEnumerator Cover_PanelSettingsRemovedDuringTransition_ReturnsSurfaceUnavailable()
        {
            var operation = _controller.CoverAsync(Color.black, 2f);
            yield return null;

            _document.panelSettings = null;

            var result = default(ScreenTransitionResult);
            yield return WaitForResult(operation, value => result = value);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(ScreenTransitionError.SurfaceUnavailable));
            Assert.That(_controller.IsBusy, Is.False);
            Assert.That(_controller.Status.Opacity, Is.Zero);
            Assert.That(FindOverlay(), Is.Null);
        }

        /// <summary>Controllerが所有する内部名のオーバーレイを実documentから探す。</summary>
        /// <returns>接続中のオーバーレイ。未作成または除去済みならnull。</returns>
        private VisualElement FindOverlay() => _document?.rootVisualElement?.Q<VisualElement>("screen-transition-overlay");

        /// <summary>Awaitableの完了を実時間timeout付きで待つ。</summary>
        /// <param name="operation">完了を待つ画面遷移。</param>
        /// <param name="receiveResult">完了結果の受取先。</param>
        private static IEnumerator WaitForResult(Awaitable<ScreenTransitionResult> operation, Action<ScreenTransitionResult> receiveResult)
        {
            var awaiter = operation.GetAwaiter();
            var deadline = Time.realtimeSinceStartup + 3f;
            while (!awaiter.IsCompleted)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("画面遷移が実時間3秒以内に完了しませんでした");
                yield return null;
            }

            receiveResult(awaiter.GetResult());
        }

    }
}
