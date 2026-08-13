using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScreenTransition.Samples.Tests.PlayMode
{
    /// <summary>Import済みBasicsサンプルの操作ボタン名と、手動遷移後の再操作可否を検証する。</summary>
    public sealed class ScreenTransitionBasicsButtonTests
    {
        /// <summary>テスト対象のUIDocumentとControllerを所有するGameObject。</summary>
        private GameObject _host;

        /// <summary>名前付きボタンを検索するUIDocument。</summary>
        private UIDocument _document;

        /// <summary>UIDocumentへ実panelを割り当てる設定。</summary>
        private PanelSettings _panelSettings;

        /// <summary>サンプル画面と3個の操作ボタンを構築する。</summary>
        [UnitySetUp]
        public IEnumerator CreateSampleView()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _host = new GameObject("Screen Transition Basics Button Tests");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _host.AddComponent<ScreenTransitionController>();
            _host.AddComponent<ScreenTransitionBasicsController>();
            _host.SetActive(true);

            yield return null;

            Assert.That(FindButton(ScreenTransitionBasicsController.CoverButtonElementName), Is.Not.Null);
            Assert.That(FindButton(ScreenTransitionBasicsController.RevealButtonElementName), Is.Not.Null);
            Assert.That(FindButton(ScreenTransitionBasicsController.DemoButtonElementName), Is.Not.Null);
        }

        /// <summary>テスト対象のGameObjectとPanelSettingsを破棄する。</summary>
        [UnityTearDown]
        public IEnumerator DestroySampleView()
        {
            if (_host != null) Object.Destroy(_host);
            yield return null;

            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
            _host = null;
            _document = null;
            _panelSettings = null;
        }

        /// <summary>手動CoverとRevealの各完了後に、3個の操作ボタンを再び押せることを確かめる。</summary>
        [UnityTest]
        public IEnumerator ManualCoverAndReveal_CompletionReenablesAllButtons()
        {
            var coverButton = FindButton(ScreenTransitionBasicsController.CoverButtonElementName);
            var revealButton = FindButton(ScreenTransitionBasicsController.RevealButtonElementName);
            var demoButton = FindButton(ScreenTransitionBasicsController.DemoButtonElementName);

            AssertButtonsEnabled(coverButton, revealButton, demoButton);
            InvokeBoundClick(coverButton);
            AssertButtonsDisabled(coverButton, revealButton, demoButton);
            yield return WaitUntilButtonsEnabled(coverButton, revealButton, demoButton);

            InvokeBoundClick(revealButton);
            AssertButtonsDisabled(coverButton, revealButton, demoButton);
            yield return WaitUntilButtonsEnabled(coverButton, revealButton, demoButton);
        }

        /// <summary>安定した要素名でUIDocumentからボタンを取得する。</summary>
        /// <param name="elementName">サンプルが公開するボタンの要素名。</param>
        /// <returns>一致したボタン。存在しない場合はnull。</returns>
        private Button FindButton(string elementName) => _document.rootVisualElement.Q<Button>(elementName);

        /// <summary>Buttonが保持する実コールバックを呼び、画面上の操作と同じ入口を通す。</summary>
        /// <param name="button">実行する名前付きボタン。</param>
        private static void InvokeBoundClick(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "UI ToolkitのButtonコールバック入口を取得できません");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        /// <summary>遷移受付直後は全操作が無効であることを確かめる。</summary>
        /// <param name="coverButton">Coverボタン。</param>
        /// <param name="revealButton">Revealボタン。</param>
        /// <param name="demoButton">自動デモボタン。</param>
        private static void AssertButtonsDisabled(Button coverButton, Button revealButton, Button demoButton)
        {
            Assert.That(coverButton.enabledSelf, Is.False);
            Assert.That(revealButton.enabledSelf, Is.False);
            Assert.That(demoButton.enabledSelf, Is.False);
        }

        /// <summary>待機前または完了後は全操作が有効であることを確かめる。</summary>
        /// <param name="coverButton">Coverボタン。</param>
        /// <param name="revealButton">Revealボタン。</param>
        /// <param name="demoButton">自動デモボタン。</param>
        private static void AssertButtonsEnabled(Button coverButton, Button revealButton, Button demoButton)
        {
            Assert.That(coverButton.enabledSelf, Is.True);
            Assert.That(revealButton.enabledSelf, Is.True);
            Assert.That(demoButton.enabledSelf, Is.True);
        }

        /// <summary>3個の操作ボタンが再び有効になるまで実時間timeout付きで待つ。</summary>
        /// <param name="coverButton">Coverボタン。</param>
        /// <param name="revealButton">Revealボタン。</param>
        /// <param name="demoButton">自動デモボタン。</param>
        private static IEnumerator WaitUntilButtonsEnabled(Button coverButton, Button revealButton, Button demoButton)
        {
            var deadline = Time.realtimeSinceStartup + 3f;
            while (!coverButton.enabledSelf || !revealButton.enabledSelf || !demoButton.enabledSelf)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("手動遷移の完了後3秒以内に操作ボタンが再有効化されませんでした");
                yield return null;
            }

            AssertButtonsEnabled(coverButton, revealButton, demoButton);
        }
    }
}
