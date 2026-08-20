using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InputMultiTapping.Samples.PlayMode.Tests
{
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputMultiTapClassifierBasicsButtonTests
    {
        private const string PanelSettingsGuid = "ef000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private InputMultiTapClassifierBasicsController _sample;
        private PanelSettings _panelSettings;
        private RenderTexture _targetTexture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var shipped = LoadShippedPanelSettings();
            Assert.That(shipped, Is.Not.Null);
            Assert.That(shipped.themeStyleSheet, Is.Not.Null);
            Assert.That(shipped.scaleMode, Is.EqualTo(PanelScaleMode.ConstantPixelSize));
            _panelSettings = UnityEngine.Object.Instantiate(shipped);
            _targetTexture = CreateTarget(960, 600);
            _panelSettings.targetTexture = _targetTexture;
            _host = new GameObject("Input Multi Tap Classifier Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputMultiTapClassifierBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(InputMultiTapClassifierBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            if (_targetTexture != null)
            {
                _targetTexture.Release();
                UnityEngine.Object.Destroy(_targetTexture);
            }
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialState_HasNoPendingBurstAtTick100()
        {
            Assert.That(_sample.CurrentTick, Is.EqualTo(100));
            Assert.That(_sample.PendingTapCount, Is.Zero);
            Assert.That(_sample.HasPendingTaps, Is.False);
            Assert.That(_sample.CompletedThisSample, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(InputMultiTapError.None));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoTapButtons_KeepDoubleTapPending()
        {
            Click(InputMultiTapClassifierBasicsController.FirstTapButtonElementName);
            Assert.That(_sample.PendingTapCount, Is.EqualTo(1));
            Assert.That(_sample.PendingDeadlineTick, Is.EqualTo(103));
            Click(InputMultiTapClassifierBasicsController.SecondTapButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(102));
            Assert.That(_sample.PendingTapCount, Is.EqualTo(2));
            Assert.That(_sample.PendingDeadlineTick, Is.EqualTo(105));
            Assert.That(_sample.TapAcceptedThisSample, Is.True);
            Assert.That(_sample.CompletedThisSample, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpireButton_CompletesPendingDoubleTap()
        {
            Click(InputMultiTapClassifierBasicsController.FirstTapButtonElementName);
            Click(InputMultiTapClassifierBasicsController.SecondTapButtonElementName);
            Click(InputMultiTapClassifierBasicsController.ExpireButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(106));
            Assert.That(_sample.PendingTapCount, Is.Zero);
            Assert.That(_sample.CompletedTapCount, Is.EqualTo(2));
            Assert.That(_sample.CompletedThisSample, Is.True);
            Assert.That(_sample.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.GapExpired));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveButtons_CompleteDoubleThenTripleTap()
        {
            Click(InputMultiTapClassifierBasicsController.FirstTapButtonElementName);
            Click(InputMultiTapClassifierBasicsController.SecondTapButtonElementName);
            Click(InputMultiTapClassifierBasicsController.ExpireButtonElementName);
            Click(InputMultiTapClassifierBasicsController.NewTapButtonElementName);
            Click(InputMultiTapClassifierBasicsController.CompleteTripleButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(109));
            Assert.That(_sample.PendingTapCount, Is.Zero);
            Assert.That(_sample.CompletedTapCount, Is.EqualTo(3));
            Assert.That(_sample.CompletedThisSample, Is.True);
            Assert.That(_sample.TapAcceptedThisSample, Is.True);
            Assert.That(_sample.CompletionReason, Is.EqualTo(InputMultiTapCompletionReason.MaximumReached));
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(5));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Geometry_WideAndNarrowStayContained()
        {
            yield return AssertGeometry(true);
            ReplaceTarget(640, 360);
            yield return WaitUntil(() => Math.Abs(ReadyRoot().worldBound.width - 640f) <= 1f && Math.Abs(ReadyRoot().worldBound.height - 360f) <= 1f, "640x360 panelへ切り替わりませんでした。");
            yield return null;
            yield return AssertGeometry(false);
        }

        private IEnumerator AssertGeometry(bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(InputMultiTapClassifierBasicsController.CardElementName);
            var names = new[]
            {
                InputMultiTapClassifierBasicsController.TitleElementName,
                InputMultiTapClassifierBasicsController.DescriptionElementName,
                InputMultiTapClassifierBasicsController.ConfigurationElementName,
                InputMultiTapClassifierBasicsController.InputElementName,
                InputMultiTapClassifierBasicsController.StageElementName,
                InputMultiTapClassifierBasicsController.ResultElementName,
                InputMultiTapClassifierBasicsController.FirstTapButtonElementName,
                InputMultiTapClassifierBasicsController.SecondTapButtonElementName,
                InputMultiTapClassifierBasicsController.ExpireButtonElementName,
                InputMultiTapClassifierBasicsController.NewTapButtonElementName,
                InputMultiTapClassifierBasicsController.CompleteTripleButtonElementName
            };
            var elements = names.Select(name => root.Q<VisualElement>(name)).ToArray();
            Assert.That(elements.All(element => element != null), Is.True);
            var safe = new Rect(card.worldBound.xMin + 5f, card.worldBound.yMin + 5f, card.worldBound.width - 10f, card.worldBound.height - 10f);
            foreach (var element in elements)
            {
                var bounds = element.worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f));
                Assert.That(bounds.height, Is.GreaterThan(0f));
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 0.5f));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + 0.5f));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 0.5f));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + 0.5f));
            }
            for (var first = 0; first < elements.Length; first++)
            for (var second = first + 1; second < elements.Length; second++)
                Assert.That(elements[first].worldBound.Overlaps(elements[second].worldBound), Is.False, $"overlap: {elements[first].name}/{elements[second].name}");
            var buttons = new[]
            {
                root.Q<Button>(InputMultiTapClassifierBasicsController.FirstTapButtonElementName),
                root.Q<Button>(InputMultiTapClassifierBasicsController.SecondTapButtonElementName),
                root.Q<Button>(InputMultiTapClassifierBasicsController.ExpireButtonElementName),
                root.Q<Button>(InputMultiTapClassifierBasicsController.NewTapButtonElementName),
                root.Q<Button>(InputMultiTapClassifierBasicsController.CompleteTripleButtonElementName)
            };
            if (wide) Assert.That(buttons.All(button => Math.Abs(button.worldBound.yMin - buttons[0].worldBound.yMin) <= 0.5f), Is.True);
            else
            {
                Assert.That(buttons.Take(3).All(button => Math.Abs(button.worldBound.yMin - buttons[0].worldBound.yMin) <= 0.5f), Is.True);
                Assert.That(buttons[3].worldBound.yMin, Is.GreaterThan(buttons[0].worldBound.yMax));
                Assert.That(Math.Abs(buttons[4].worldBound.yMin - buttons[3].worldBound.yMin), Is.LessThanOrEqualTo(0.5f));
            }
            yield return null;
        }

        private void Click(string name)
        {
            var button = ReadyRoot().Q<Button>(name);
            Assert.That(button, Is.Not.Null);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private VisualElement ReadyRoot() => _document?.rootVisualElement;

        private void ReplaceTarget(int width, int height)
        {
            var previous = _targetTexture;
            _targetTexture = CreateTarget(width, height);
            _panelSettings.targetTexture = _targetTexture;
            previous.Release();
            UnityEngine.Object.Destroy(previous);
        }

        private static RenderTexture CreateTarget(int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            return target;
        }

        private static PanelSettings LoadShippedPanelSettings()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(PanelSettingsGuid));
#else
            Assert.Fail("このfixtureはUnity Editorで実行してください。");
            return null;
#endif
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failure)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (!predicate())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(failure);
                yield return null;
            }
        }
    }
}
