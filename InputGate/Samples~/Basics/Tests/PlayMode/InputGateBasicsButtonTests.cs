using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InputGate.Samples.Tests.PlayMode
{
    /// <summary>Import済みBasics sampleの実Button、Action Map分離、wide/narrow geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputGateBasicsButtonTests
    {
        private const int WideWidth = 960;
        private const int WideHeight = 600;
        private const int NarrowWidth = 640;
        private const int NarrowHeight = 360;
        private const float GeometryTolerance = 0.5f;
        private const float CardInset = 6f;
        private const string ActionAssetGuid = "120df38b4b3a4e23b20a82f6d63da029";
        private const string PanelSettingsGuid = "66e1cb0745f0f8a4a9fa34589604c69d";

        private GameObject _host;
        private UIDocument _document;
        private PlayerInput _playerInput;
        private InputGateController _controller;
        private InputGateBasicsController _sample;
        private InputActionAsset _actions;
        private PanelSettings _panelSettings;
        private RenderTexture _targetTexture;
        private Gamepad _gamepad;

        /// <summary>shipped Action AssetとPanel Settingsをcloneし、実RenderTexture panelへsampleを構築する。</summary>
        [UnitySetUp]
        public IEnumerator CreateSampleView()
        {
            _actions = LoadAndCloneAsset<InputActionAsset>(ActionAssetGuid);
            _panelSettings = LoadAndCloneAsset<PanelSettings>(PanelSettingsGuid);
            Assert.That(_panelSettings.themeStyleSheet, Is.Not.Null, "shipped Panel SettingsのTheme Style Sheetがありません。");
            Assert.That(_panelSettings.scaleMode, Is.EqualTo(PanelScaleMode.ConstantPixelSize));
            _targetTexture = CreateTarget(WideWidth, WideHeight);
            _panelSettings.targetTexture = _targetTexture;

            _host = new GameObject("Input Gate Basics Button Tests");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _playerInput = _host.AddComponent<PlayerInput>();
            _playerInput.actions = _actions;
            _playerInput.defaultActionMap = "Gameplay";
            _playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            _controller = _host.AddComponent<InputGateController>();
            _sample = _host.AddComponent<InputGateBasicsController>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _host.SetActive(true);

            yield return WaitUntil(
                () => _controller.Status.IsReady && Find<VisualElement>(InputGateBasicsController.ReadyElementName) is { } ready && ready.worldBound.width > 0f,
                3d,
                "Input Gate Basicsの960x600実panelとControllerが3秒以内に準備されませんでした。");
        }

        /// <summary>sample所有lease、device、GameObject、clone asset、RenderTextureを確実に破棄する。</summary>
        [UnityTearDown]
        public IEnumerator DestroySampleView()
        {
            var releaseAll = Find<Button>(InputGateBasicsController.ReleaseAllButtonElementName);
            if (releaseAll != null && releaseAll.enabledSelf) InvokeBoundClick(releaseAll);
            yield return null;
            if (_host != null) UnityEngine.Object.Destroy(_host);
            yield return null;
            if (_gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
            if (_actions != null) UnityEngine.Object.DestroyImmediate(_actions);
            if (_panelSettings != null) UnityEngine.Object.DestroyImmediate(_panelSettings);
            ReleaseTarget();
        }

        /// <summary>実Button callbackで入れ子leaseを作り、Gameplayだけ停止してUI Actionを継続する。</summary>
        [UnityTest]
        public IEnumerator Buttons_BlockGameplayKeepUi_AndRestoreAfterLastRelease()
        {
            yield return Pulse(GamepadButton.South);
            yield return Pulse(GamepadButton.North);
            Assert.That(_sample.GameplayCount, Is.EqualTo(1));
            Assert.That(_sample.UiCount, Is.EqualTo(1));

            InvokeBoundClick(Find<Button>(InputGateBasicsController.NestedButtonElementName));
            Assert.That(_controller.Status.IsBlocking, Is.True);
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(2));
            Assert.That(_sample.OwnedLeaseCount, Is.EqualTo(2));

            yield return Pulse(GamepadButton.South);
            yield return Pulse(GamepadButton.North);
            Assert.That(_sample.GameplayCount, Is.EqualTo(1), "停止中にGameplay Pulseを受理しました。");
            Assert.That(_sample.UiCount, Is.EqualTo(2), "停止対象外UI Pulseが止まりました。");

            InvokeBoundClick(Find<Button>(InputGateBasicsController.ReleaseOneButtonElementName));
            Assert.That(_controller.Status.ActiveLeaseCount, Is.EqualTo(1));
            yield return Pulse(GamepadButton.South);
            Assert.That(_sample.GameplayCount, Is.EqualTo(1), "1件のleaseが残る間にGameplayが復元されました。");

            InvokeBoundClick(Find<Button>(InputGateBasicsController.ReleaseAllButtonElementName));
            Assert.That(_controller.Status.IsBlocking, Is.False);
            Assert.That(_controller.Status.ActiveLeaseCount, Is.Zero);
            yield return Pulse(GamepadButton.South);
            Assert.That(_sample.GameplayCount, Is.EqualTo(2), "最後のlease解放後にGameplayが復元されませんでした。");
        }

        /// <summary>wideでは5 Buttonを1行、narrowでは3+2行に保ち、全表示要素をcard内へ収める。</summary>
        [UnityTest]
        public IEnumerator ReadyView_UsesContainedWideAndNarrowGeometry()
        {
            yield return WaitForGeometry(WideWidth, WideHeight);
            var card = Find<VisualElement>(InputGateBasicsController.CardElementName);
            var buttons = FindButtons();
            AssertContained(card, buttons, true);
            AssertVisibleContent(card, buttons);

            ReplaceTarget(NarrowWidth, NarrowHeight);
            yield return WaitUntil(
                () => Find<Button>(InputGateBasicsController.ReleaseAllButtonElementName).worldBound.yMin >
                      Find<Button>(InputGateBasicsController.AcquireButtonElementName).worldBound.yMax,
                3d,
                "640x360でButtonが3+2行へ折り返されませんでした。");
            yield return WaitForGeometry(NarrowWidth, NarrowHeight);
            card = Find<VisualElement>(InputGateBasicsController.CardElementName);
            buttons = FindButtons();
            AssertContained(card, buttons, false);
            AssertVisibleContent(card, buttons);
            for (var i = 1; i < 3; i++)
            {
                Assert.That(buttons[i].worldBound.yMin, Is.EqualTo(buttons[0].worldBound.yMin).Within(GeometryTolerance));
            }

            Assert.That(buttons[3].worldBound.yMin, Is.GreaterThan(buttons[0].worldBound.yMax));
            Assert.That(buttons[4].worldBound.yMin, Is.EqualTo(buttons[3].worldBound.yMin).Within(GeometryTolerance));
        }

        /// <summary>公開した安定要素名が全て実panelへ存在し、初期状態を表示する。</summary>
        [UnityTest]
        public IEnumerator ReadyView_ContainsStableControlsAndStatus()
        {
            Assert.That(Find<Label>(InputGateBasicsController.StatusElementName).text, Does.Contain("Ready=True"));
            Assert.That(Find<Label>(InputGateBasicsController.StatusElementName).text, Does.Contain("Maps=1"));
            Assert.That(Find<Label>(InputGateBasicsController.GameplayCountElementName), Is.Not.Null);
            Assert.That(Find<Label>(InputGateBasicsController.UiCountElementName), Is.Not.Null);
            foreach (var button in FindButtons()) Assert.That(button, Is.Not.Null);
            yield break;
        }

        /// <summary>安定した要素名でUIDocumentから指定型を取得する。</summary>
        /// <typeparam name="T">Button、Label、VisualElementのいずれか。</typeparam>
        /// <param name="name">sample controllerが公開する要素名。</param>
        /// <returns>一致した要素。</returns>
        private T Find<T>(string name) where T : VisualElement => _document?.rootVisualElement?.Q<T>(name);

        /// <summary>画面の操作順で5つのButtonを返す。</summary>
        /// <returns>Acquire、Nested、Release One、Release All、Resetの順。</returns>
        private Button[] FindButtons() => new[]
        {
            Find<Button>(InputGateBasicsController.AcquireButtonElementName),
            Find<Button>(InputGateBasicsController.NestedButtonElementName),
            Find<Button>(InputGateBasicsController.ReleaseOneButtonElementName),
            Find<Button>(InputGateBasicsController.ReleaseAllButtonElementName),
            Find<Button>(InputGateBasicsController.ResetButtonElementName),
        };

        /// <summary>指定Gamepad Buttonの押下と解放を別frameへ送り、Action callbackを実行する。</summary>
        /// <param name="button">送るGamepad Button。</param>
        /// <returns>入力更新を待つcoroutine。</returns>
        private IEnumerator Pulse(GamepadButton button)
        {
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            yield return null;
        }

        /// <summary>Buttonが保持する実callbackをUI ToolkitのClick入口から呼ぶ。</summary>
        /// <param name="button">有効状態を確認済みのsample Button。</param>
        private static void InvokeBoundClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True, button.name + " Buttonが無効です。");
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }

        /// <summary>cardと全Buttonが正の寸法を持ち、安全領域内で互いに重ならないことを確かめる。</summary>
        /// <param name="card">表示を囲むcard。</param>
        /// <param name="buttons">画面操作順のButton。</param>
        /// <param name="singleRow">1行を要求する場合はtrue。</param>
        private static void AssertContained(VisualElement card, Button[] buttons, bool singleRow)
        {
            Assert.That(card, Is.Not.Null);
            var safe = new Rect(card.worldBound.xMin + CardInset, card.worldBound.yMin + CardInset,
                card.worldBound.width - CardInset * 2f, card.worldBound.height - CardInset * 2f);
            for (var i = 0; i < buttons.Length; i++)
            {
                var bounds = buttons[i].worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f), buttons[i].name);
                Assert.That(bounds.height, Is.GreaterThan(0f), buttons[i].name);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - GeometryTolerance), buttons[i].name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + GeometryTolerance), buttons[i].name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - GeometryTolerance), buttons[i].name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + GeometryTolerance), buttons[i].name);
                if (singleRow) Assert.That(bounds.yMin, Is.EqualTo(buttons[0].worldBound.yMin).Within(GeometryTolerance));
                for (var other = i + 1; other < buttons.Length; other++)
                {
                    Assert.That(bounds.Overlaps(buttons[other].worldBound), Is.False, buttons[i].name + " / " + buttons[other].name);
                }
            }
        }

        /// <summary>TitleからButtonまでの全表示要素がcard内に収まり、異なるsection同士で重ならないことを確かめる。</summary>
        /// <param name="card">表示を囲むcard。</param>
        /// <param name="buttons">画面操作順のButton。</param>
        private void AssertVisibleContent(VisualElement card, Button[] buttons)
        {
            var title = Find<Label>(InputGateBasicsController.TitleElementName);
            var description = Find<Label>(InputGateBasicsController.DescriptionElementName);
            var status = Find<Label>(InputGateBasicsController.StatusElementName);
            var stage = Find<Label>(InputGateBasicsController.StageElementName);
            var gameplay = Find<Label>(InputGateBasicsController.GameplayCountElementName);
            var ui = Find<Label>(InputGateBasicsController.UiCountElementName);
            var elements = new VisualElement[6 + buttons.Length];
            elements[0] = title;
            elements[1] = description;
            elements[2] = status;
            elements[3] = stage;
            elements[4] = gameplay;
            elements[5] = ui;
            Array.Copy(buttons, 0, elements, 6, buttons.Length);
            var safe = new Rect(card.worldBound.xMin + CardInset, card.worldBound.yMin + CardInset,
                card.worldBound.width - CardInset * 2f, card.worldBound.height - CardInset * 2f);

            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                Assert.That(element, Is.Not.Null);
                var bounds = element.worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f), element.name);
                Assert.That(bounds.height, Is.GreaterThan(0f), element.name);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - GeometryTolerance), element.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + GeometryTolerance), element.name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - GeometryTolerance), element.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + GeometryTolerance), element.name);
                for (var other = i + 1; other < elements.Length; other++)
                {
                    Assert.That(bounds.Overlaps(elements[other].worldBound), Is.False,
                        element.name + " / " + elements[other].name);
                }
            }

            Assert.That(title.worldBound.yMax, Is.LessThanOrEqualTo(description.worldBound.yMin + GeometryTolerance));
            Assert.That(description.worldBound.yMax, Is.LessThanOrEqualTo(status.worldBound.yMin + GeometryTolerance));
            Assert.That(status.worldBound.yMax, Is.LessThanOrEqualTo(stage.worldBound.yMin + GeometryTolerance));
            Assert.That(stage.worldBound.yMax, Is.LessThanOrEqualTo(gameplay.worldBound.yMin + GeometryTolerance));
            Assert.That(gameplay.worldBound.yMin, Is.EqualTo(ui.worldBound.yMin).Within(GeometryTolerance));
            Assert.That(gameplay.worldBound.yMax, Is.LessThanOrEqualTo(buttons[0].worldBound.yMin + GeometryTolerance));
            Assert.That(ui.worldBound.yMax, Is.LessThanOrEqualTo(buttons[0].worldBound.yMin + GeometryTolerance));
        }

        /// <summary>実panelのrootとcardが指定RenderTexture寸法へ解決されるまで待つ。</summary>
        /// <param name="width">期待する幅。</param>
        /// <param name="height">期待する高さ。</param>
        /// <returns>layout完了を待つcoroutine。</returns>
        private IEnumerator WaitForGeometry(int width, int height)
        {
            yield return WaitUntil(
                () => Math.Abs(_document.rootVisualElement.contentRect.width - width) <= GeometryTolerance &&
                      Math.Abs(_document.rootVisualElement.contentRect.height - height) <= GeometryTolerance &&
                      Find<VisualElement>(InputGateBasicsController.CardElementName).worldBound.width > 0f,
                3d,
                $"実panelが{width}x{height}へ解決されませんでした。");
        }

        /// <summary>Panel Settingsの描画先を新しい実寸RenderTextureへ交換する。</summary>
        /// <param name="width">新しい幅。</param>
        /// <param name="height">新しい高さ。</param>
        private void ReplaceTarget(int width, int height)
        {
            ReleaseTarget();
            _targetTexture = CreateTarget(width, height);
            _panelSettings.targetTexture = _targetTexture;
        }

        /// <summary>指定寸法の描画可能なRenderTextureを作る。</summary>
        /// <param name="width">幅。</param>
        /// <param name="height">高さ。</param>
        /// <returns>作成済みRenderTexture。</returns>
        private static RenderTexture CreateTarget(int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = $"Input Gate Basics {width}x{height}",
            };
            Assert.That(target.Create(), Is.True);
            return target;
        }

        /// <summary>現在のRenderTextureをPanel Settingsから外して破棄する。</summary>
        private void ReleaseTarget()
        {
            if (_panelSettings != null) _panelSettings.targetTexture = null;
            if (_targetTexture == null) return;
            _targetTexture.Release();
            UnityEngine.Object.DestroyImmediate(_targetTexture);
            _targetTexture = null;
        }

        /// <summary>GUIDでshipped assetを読み込み、test専用instanceを作る。</summary>
        /// <typeparam name="T">InputActionAssetまたはPanelSettings。</typeparam>
        /// <param name="guid">package import後も不変なmeta GUID。</param>
        /// <returns>shipped assetのclone。</returns>
        private static T LoadAndCloneAsset<T>(string guid) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(path, Is.Not.Empty, guid + "のasset pathを解決できません。");
            var source = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(source, Is.Not.Null, path);
            return UnityEngine.Object.Instantiate(source);
#else
            Assert.Fail("Input Gate sample geometry tests are Editor PlayMode tests.");
            return null;
#endif
        }

        /// <summary>実時間deadlineまで指定条件をframeごとに確認する。</summary>
        /// <param name="condition">成功時にtrueとなる条件。</param>
        /// <param name="seconds">timeScaleに依存しないtimeout秒数。</param>
        /// <param name="message">deadline超過時の説明。</param>
        /// <returns>PlayModeのframeごとに進むcoroutine。</returns>
        private static IEnumerator WaitUntil(Func<bool> condition, double seconds, string message)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(message);
                yield return null;
            }
        }
    }
}
