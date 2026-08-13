using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TimeControl.Samples.Tests.PlayMode
{
    /// <summary>Import済みBasics sampleの実Button callback、時間差、入れ子順序、解放範囲を検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class TimeControlBasicsButtonTests
    {
        /// <summary>公開sampleと同じ横長画面を再現する幅。</summary>
        private const int TargetWidth = 960;

        /// <summary>公開sampleと同じ横長画面を再現する高さ。</summary>
        private const int TargetHeight = 600;

        /// <summary>Buttonを折り返す狭い画面を再現する幅。</summary>
        private const int NarrowTargetWidth = 640;

        /// <summary>描画計算の小数誤差として許容するpixel数。</summary>
        private const float GeometryTolerance = 0.5f;

        /// <summary>Buttonとcard端の間へ残す最低余白。</summary>
        private const float MinimumCardInset = 20f;

        /// <summary>各test前に復元対象として保存するglobal時間倍率。</summary>
        private float _originalTimeScale;

        /// <summary>UIDocument、Controller、sample controllerを同じ寿命で所有するGameObject。</summary>
        private GameObject _host;

        /// <summary>安定した名前で実Buttonと表示Labelを取得するUIDocument。</summary>
        private UIDocument _document;

        /// <summary>実`Time.timeScale`を所有するtest対象Controller。</summary>
        private TimeControlController _controller;

        /// <summary>Button callbackと2種類の経過時間を所有するsample controller。</summary>
        private TimeControlBasicsController _sample;

        /// <summary>UIDocumentへ実panelを割り当てるtest用設定。</summary>
        private PanelSettings _panelSettings;

        /// <summary>画面寸法を固定して実panelを描画するtest用texture。</summary>
        private RenderTexture _targetTexture;

        /// <summary>global時間を1へ揃え、実panelとsample画面を作る。</summary>
        [UnitySetUp]
        public IEnumerator CreateSampleView()
        {
            _originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            _targetTexture = new RenderTexture(TargetWidth, TargetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "Time Control Basics Button Tests Target" };
            Assert.That(_targetTexture.Create(), Is.True, "PlayMode検証用RenderTextureを作れません。");

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.targetTexture = _targetTexture;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _host = new GameObject("Time Control Basics Button Tests");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _controller = _host.AddComponent<TimeControlController>();
            _sample = _host.AddComponent<TimeControlBasicsController>();
            _host.SetActive(true);

            var root = _document.rootVisualElement;
            root.style.width = TargetWidth;
            root.style.height = TargetHeight;

            yield return WaitUntil(
                () => _controller.IsControlling && FindElement<VisualElement>(TimeControlBasicsController.ReadyElementName) != null,
                3d,
                "Time Control Basicsの実panelとControllerが3秒以内に準備されませんでした。");
        }

        /// <summary>sample所有leaseとGameObjectを終了してからglobal時間を元へ戻す。</summary>
        [UnityTearDown]
        public IEnumerator DestroySampleView()
        {
            var releaseButton = FindElement<Button>(TimeControlBasicsController.ReleaseOwnedButtonElementName);
            if (releaseButton != null && releaseButton.enabledSelf) InvokeBoundClick(releaseButton);

            yield return null;

            if (_host != null) UnityEngine.Object.Destroy(_host);
            yield return null;

            Time.timeScale = _originalTimeScale;
            if (_panelSettings != null) UnityEngine.Object.DestroyImmediate(_panelSettings);
            if (_targetTexture != null)
            {
                _targetTexture.Release();
                UnityEngine.Object.DestroyImmediate(_targetTexture);
            }

            _host = null;
            _document = null;
            _controller = null;
            _sample = null;
            _panelSettings = null;
            _targetTexture = null;
        }

        /// <summary>視覚gateと利用者が必要とするButton、状態、段階、2本のlaneが欠けていないことを確かめる。</summary>
        [UnityTest]
        public IEnumerator ReadyView_ContainsAllStableControlsAndState()
        {
            Assert.That(FindElement<Button>(TimeControlBasicsController.PauseButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(TimeControlBasicsController.SlowButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(TimeControlBasicsController.FastButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(TimeControlBasicsController.NestedDemoButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(TimeControlBasicsController.ReleaseOwnedButtonElementName), Is.Not.Null);
            Assert.That(FindElement<VisualElement>(TimeControlBasicsController.EvidenceElementName), Is.Not.Null);
            Assert.That(FindElement<VisualElement>(TimeControlBasicsController.ScaledLaneMarkerElementName), Is.Not.Null);
            Assert.That(FindElement<VisualElement>(TimeControlBasicsController.UnscaledLaneMarkerElementName), Is.Not.Null);

            var status = FindElement<Label>(TimeControlBasicsController.StatusElementName);
            var stage = FindElement<Label>(TimeControlBasicsController.StageElementName);
            var result = FindElement<Label>(TimeControlBasicsController.ResultElementName);
            var scaled = FindElement<Label>(TimeControlBasicsController.ScaledCounterElementName);
            var unscaled = FindElement<Label>(TimeControlBasicsController.UnscaledCounterElementName);

            Assert.That(status, Is.Not.Null);
            Assert.That(status.text, Does.Contain("Baseline="));
            Assert.That(status.text, Does.Contain("Effective="));
            Assert.That(status.text, Does.Contain("Leases="));
            Assert.That(status.text, Does.Contain("Error=None"));
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.text, Is.EqualTo("Ready"));
            Assert.That(result, Is.Not.Null);
            Assert.That(result.text, Is.Not.Empty);
            Assert.That(scaled, Is.Not.Null);
            Assert.That(scaled.text, Is.Not.Empty);
            Assert.That(unscaled, Is.Not.Null);
            Assert.That(unscaled.text, Is.Not.Empty);
            yield break;
        }

        /// <summary>960x600の1行表示と640x600の折返し表示でButtonがcard内へ収まり、互いに重ならないことを確かめる。</summary>
        [UnityTest]
        public IEnumerator ReadyView_KeepsButtonsInsideCardWithoutOverlapAtReferenceAndNarrowWidths()
        {
            yield return WaitUntil(() => FindElement<VisualElement>(TimeControlBasicsController.ButtonRowElementName) is { } row && row.worldBound.width > 0f && FindElement<Button>(TimeControlBasicsController.ReleaseOwnedButtonElementName) is { } button && button.worldBound.width > 0f, 3d, "Time Control BasicsのButton配置が3秒以内に確定しませんでした。");

            var root = _document.rootVisualElement;
            var card = FindElement<VisualElement>(TimeControlBasicsController.CardElementName);
            var buttonRow = FindElement<VisualElement>(TimeControlBasicsController.ButtonRowElementName);
            var buttons = FindButtons();
            Assert.That(root.contentRect.width, Is.EqualTo(TargetWidth).Within(GeometryTolerance));
            Assert.That(root.contentRect.height, Is.EqualTo(TargetHeight).Within(GeometryTolerance));
            AssertButtonGeometry(card, buttonRow, buttons, true);

            root.style.width = NarrowTargetWidth;
            yield return WaitUntil(() => buttons[4].worldBound.yMin > buttons[0].worldBound.yMin + GeometryTolerance, 3d, "640x600で操作Buttonが読みやすい幅へ折り返されませんでした。");

            Assert.That(root.contentRect.width, Is.EqualTo(NarrowTargetWidth).Within(GeometryTolerance));
            Assert.That(root.contentRect.height, Is.EqualTo(TargetHeight).Within(GeometryTolerance));
            AssertButtonGeometry(card, buttonRow, buttons, false);
            for (var i = 1; i < 4; i++)
            {
                Assert.That(buttons[i].worldBound.yMin, Is.EqualTo(buttons[0].worldBound.yMin).Within(GeometryTolerance), $"{buttons[i].name}が640x600の先頭操作行から外れています。");
                Assert.That(buttons[i].worldBound.xMin, Is.GreaterThan(buttons[i - 1].worldBound.xMax), $"{buttons[i].name}の折返し前の表示順が操作順と一致しません。");
            }

            Assert.That(buttons[4].worldBound.yMin, Is.GreaterThan(buttons[0].worldBound.yMax), "Release Owned Buttonが640x600の折返し行へ配置されていません。");
        }

        /// <summary>実Buttonで2、0.25、0の優先順位を作り、pause中もUIからsample所有leaseを解放できることを確かめる。</summary>
        [UnityTest]
        public IEnumerator ManualButtons_PauseKeepsUnscaledLaneResponsive_AndReleaseOwnedPreservesForeignLease()
        {
            var fastButton = FindElement<Button>(TimeControlBasicsController.FastButtonElementName);
            var slowButton = FindElement<Button>(TimeControlBasicsController.SlowButtonElementName);
            var pauseButton = FindElement<Button>(TimeControlBasicsController.PauseButtonElementName);
            var releaseButton = FindElement<Button>(TimeControlBasicsController.ReleaseOwnedButtonElementName);

            InvokeBoundClick(fastButton);
            yield return WaitForTimeScale(2f, 2d, "Fast x2 Buttonのcallbackが実効倍率2を反映しませんでした。");
            Assert.That(_sample.StageText, Is.EqualTo("Manual: Fast x2"));

            InvokeBoundClick(slowButton);
            yield return WaitForTimeScale(0.25f, 2d, "Slow x0.25 ButtonがFast leaseより優先されませんでした。");
            Assert.That(_sample.StageText, Is.EqualTo("Manual: Slow x0.25"));

            InvokeBoundClick(pauseButton);
            yield return WaitForTimeScale(0f, 2d, "Pause x0 Buttonがほかのleaseより優先されませんでした。");
            Assert.That(_sample.StageText, Is.EqualTo("Manual: Pause x0"));
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(3));
            Assert.That(releaseButton.enabledSelf, Is.True, "pause中にRelease Owned Buttonを操作できません。");

            yield return null;
            var scaledBefore = _sample.ScaledElapsed;
            var unscaledBefore = _sample.UnscaledElapsed;
            yield return WaitForRealtime(0.25d);
            var scaledDifference = _sample.ScaledElapsed - scaledBefore;
            var unscaledDifference = _sample.UnscaledElapsed - unscaledBefore;

            Assert.That(scaledDifference, Is.LessThan(0.02d), "pause中にScaled Laneが実時間と同じように進みました。");
            Assert.That(unscaledDifference, Is.GreaterThan(0.1d), "pause中にUnscaled Laneが停止しました。");

            Assert.That(_controller.TryAcquire(0.5f, out var foreignLease, out var foreignError), Is.True, $"sample外のleaseを準備できません: {foreignError}");
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(4));
            InvokeBoundClick(releaseButton);
            yield return WaitForTimeScale(0.5f, 2d, "Release Owned Buttonがsample所有leaseだけを解放しませんでした。");
            Assert.That(_sample.StageText, Is.EqualTo("Manual: Release Owned"));
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(1));
            Assert.That(foreignLease.IsActive, Is.True, "Release Ownedがsample外のleaseまで無効化しました。");
            Assert.That(releaseButton.enabledSelf, Is.False);

            foreignLease.Dispose();
            yield return WaitForTimeScale(1f, 2d, "sample外のlease解放後に基準値へ戻りませんでした。");
            Assert.That(_controller.Status.ActiveLeaseCount, Is.Zero);
        }

        /// <summary>Nested Demoの各段階と実効倍率が2、0.25、0、0.25、2、基準値の順になることを確かめる。</summary>
        [UnityTest]
        public IEnumerator NestedDemo_UsesUnscaledDeadlines_AndReleasesInReverseOrder()
        {
            var nestedButton = FindElement<Button>(TimeControlBasicsController.NestedDemoButtonElementName);
            InvokeBoundClick(nestedButton);

            yield return WaitForStage(TimeControlBasicsController.NestedFastStageText, 2d);
            AssertTimeScale(2f);
            yield return WaitForStage(TimeControlBasicsController.NestedSlowStageText, 2d);
            AssertTimeScale(0.25f);
            yield return WaitForStage(TimeControlBasicsController.NestedPauseStageText, 2d);
            AssertTimeScale(0f);
            yield return WaitForStage(TimeControlBasicsController.NestedReleasePauseStageText, 2d);
            AssertTimeScale(0.25f);
            yield return WaitForStage(TimeControlBasicsController.NestedReleaseSlowStageText, 2d);
            AssertTimeScale(2f);
            yield return WaitForStage(TimeControlBasicsController.NestedReleaseFastStageText, 2d);
            AssertTimeScale(1f);

            Assert.That(_controller.Status.ActiveLeaseCount, Is.Zero);
            Assert.That(FindElement<Button>(TimeControlBasicsController.PauseButtonElementName).enabledSelf, Is.True);
        }

        /// <summary>安定した要素名を使ってUIDocumentから指定型を取得する。</summary>
        /// <typeparam name="T">Button、Label、VisualElementのいずれか。</typeparam>
        /// <param name="elementName">sample controllerが公開する安定した要素名。</param>
        /// <returns>一致した要素。見つからない場合はnull。</returns>
        private T FindElement<T>(string elementName) where T : VisualElement => _document?.rootVisualElement?.Q<T>(elementName);

        /// <summary>画面上の並び順で5つの操作Buttonを取得する。</summary>
        /// <returns>Pause、Slow、Fast、Nested Demo、Release Ownedの順のButton。</returns>
        private Button[] FindButtons() => new[] { FindElement<Button>(TimeControlBasicsController.PauseButtonElementName), FindElement<Button>(TimeControlBasicsController.SlowButtonElementName), FindElement<Button>(TimeControlBasicsController.FastButtonElementName), FindElement<Button>(TimeControlBasicsController.NestedDemoButtonElementName), FindElement<Button>(TimeControlBasicsController.ReleaseOwnedButtonElementName) };

        /// <summary>すべてのButtonが正の寸法を持ち、cardの安全余白内で重ならないことを確かめる。</summary>
        /// <param name="card">操作Buttonを含む背景card。</param>
        /// <param name="buttonRow">操作Buttonを表示順に含む行。</param>
        /// <param name="buttons">画面上の並び順で取得した操作Button。</param>
        /// <param name="requiresSingleRow">すべて同じ行に並ぶ必要がある場合はtrue。</param>
        private static void AssertButtonGeometry(VisualElement card, VisualElement buttonRow, Button[] buttons, bool requiresSingleRow)
        {
            Assert.That(card, Is.Not.Null);
            Assert.That(buttonRow, Is.Not.Null);
            for (var i = 0; i < buttons.Length; i++)
            {
                Assert.That(buttons[i], Is.Not.Null, $"操作Button[{i}]が見つかりません。");
                Assert.That(buttons[i].parent, Is.SameAs(buttonRow), $"{buttons[i].name}が安定した操作行の直下にありません。");
            }

            var safeBounds = new Rect(card.worldBound.xMin + MinimumCardInset, card.worldBound.yMin + MinimumCardInset, card.worldBound.width - (MinimumCardInset * 2f), card.worldBound.height - (MinimumCardInset * 2f));
            var firstY = buttons[0].worldBound.yMin;
            for (var i = 0; i < buttons.Length; i++)
            {
                var bounds = buttons[i].worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f), $"{buttons[i].name}の幅が確定していません。");
                Assert.That(bounds.height, Is.GreaterThan(0f), $"{buttons[i].name}の高さが確定していません。");
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safeBounds.xMin - GeometryTolerance), $"{buttons[i].name}がcard左端の安全余白を越えています。");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safeBounds.xMax + GeometryTolerance), $"{buttons[i].name}がcard右端の安全余白を越えています。");
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safeBounds.yMin - GeometryTolerance), $"{buttons[i].name}がcard上端の安全余白を越えています。");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safeBounds.yMax + GeometryTolerance), $"{buttons[i].name}がcard下端の安全余白を越えています。");
                if (requiresSingleRow) Assert.That(bounds.yMin, Is.EqualTo(firstY).Within(GeometryTolerance), $"{buttons[i].name}が960x600で操作行から外れています。");
                if (requiresSingleRow && i > 0) Assert.That(bounds.xMin, Is.GreaterThan(buttons[i - 1].worldBound.xMax), $"{buttons[i].name}の表示順が操作順と一致しません。");

                for (var otherIndex = i + 1; otherIndex < buttons.Length; otherIndex++) Assert.That(bounds.Overlaps(buttons[otherIndex].worldBound), Is.False, $"{buttons[i].name}と{buttons[otherIndex].name}が重なっています。");
            }
        }

        /// <summary>Buttonが保持する実callbackをUI ToolkitのClick入口から呼ぶ。</summary>
        /// <param name="button">有効状態を確認済みのsample Button。</param>
        private static void InvokeBoundClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True, $"{button.name} Buttonが無効です。");
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "UI ToolkitのButton callback入口を取得できません。");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        /// <summary>実時間deadlineまで指定条件をframeごとに確認する。</summary>
        /// <param name="condition">成功時にtrueとなる条件。</param>
        /// <param name="timeoutSeconds">timeScaleに依存しないtimeout秒数。</param>
        /// <param name="failureMessage">deadline超過時の失敗説明。</param>
        /// <returns>Play Modeのframeごとに進むcoroutine。</returns>
        private static IEnumerator WaitUntil(Func<bool> condition, double timeoutSeconds, string failureMessage)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(failureMessage);
                yield return null;
            }
        }

        /// <summary>指定した実時間だけtimeScaleに依存せず待つ。</summary>
        /// <param name="seconds">0以上の実時間秒数。</param>
        /// <returns>Play Modeのframeごとに進むcoroutine。</returns>
        private static IEnumerator WaitForRealtime(double seconds)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        }

        /// <summary>sampleの段階表示が期待文字列になるまで実時間で待つ。</summary>
        /// <param name="expectedStage">sampleが公開する安定した段階文字列。</param>
        /// <param name="timeoutSeconds">timeScaleに依存しないtimeout秒数。</param>
        /// <returns>Play Modeのframeごとに進むcoroutine。</returns>
        private IEnumerator WaitForStage(string expectedStage, double timeoutSeconds)
        {
            yield return WaitUntil(
                () => string.Equals(_sample.StageText, expectedStage, StringComparison.Ordinal),
                timeoutSeconds,
                $"Nested Demoが段階「{expectedStage}」へ進みませんでした。現在: {_sample.StageText}");
        }

        /// <summary>global時間倍率が期待値になるまで実時間で待つ。</summary>
        /// <param name="expected">期待する`Time.timeScale`。</param>
        /// <param name="timeoutSeconds">timeScaleに依存しないtimeout秒数。</param>
        /// <param name="failureMessage">deadline超過時の失敗説明。</param>
        /// <returns>Play Modeのframeごとに進むcoroutine。</returns>
        private static IEnumerator WaitForTimeScale(float expected, double timeoutSeconds, string failureMessage)
        {
            yield return WaitUntil(() => Mathf.Abs(Time.timeScale - expected) <= 0.0001f, timeoutSeconds, failureMessage);
        }

        /// <summary>global時間倍率が期待値と十分近いことを確かめる。</summary>
        /// <param name="expected">期待する`Time.timeScale`。</param>
        private static void AssertTimeScale(float expected)
        {
            Assert.That(Time.timeScale, Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
