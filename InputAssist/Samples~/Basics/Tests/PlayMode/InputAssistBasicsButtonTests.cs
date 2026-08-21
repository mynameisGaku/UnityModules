using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InputAssist.Samples.PlayMode.Tests
{
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputAssistBasicsButtonTests
    {
        private const string PanelSettingsGuid = "e1a55000000000000000000000000017";

        private GameObject _host;
        private UIDocument _document;
        private InputAssistBasicsController _sample;
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
            _host = new GameObject("Input Assist Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputAssistBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => Card()?.worldBound.width > 0f, "The 960x600 Input Assist panel did not become ready.");
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
        public IEnumerator VectorButtons_RunTheConfiguredPipeline()
        {
            Click(InputAssistBasicsController.SoftRightButtonElementName);
            Assert.That(_sample.LastRawInput.x, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(_sample.LastFilteredInput.x, Is.GreaterThan(0f));
            Assert.That(_sample.LastDirection, Is.EqualTo(InputDirection.Right));

            Click(InputAssistBasicsController.DiagonalButtonElementName);
            Assert.That(_sample.LastDirection, Is.EqualTo(InputDirection.UpRight));

            Click(InputAssistBasicsController.NeutralButtonElementName);
            Assert.That(_sample.LastRawInput, Is.EqualTo(Vector2.zero));
            Assert.That(_sample.ActionCount, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GestureButtons_ProduceTapHoldAndRepeatEvents()
        {
            Click(InputAssistBasicsController.TapButtonElementName);
            Assert.That(_sample.LastButtonEvents.HasFlag(InputButtonEvent.Pressed), Is.True);
            Assert.That(_sample.LastButtonEvents.HasFlag(InputButtonEvent.Released), Is.True);
            Assert.That(_sample.LastButtonEvents.HasFlag(InputButtonEvent.TapCompleted), Is.True);
            Assert.That(_sample.LastTapCount, Is.EqualTo(1));

            Click(InputAssistBasicsController.HoldRepeatButtonElementName);
            Assert.That(_sample.LastButtonEvents.HasFlag(InputButtonEvent.HoldStarted), Is.True);
            Assert.That(_sample.LastButtonEvents.HasFlag(InputButtonEvent.Repeated), Is.True);
            Assert.That(_sample.LastRepeatCount, Is.GreaterThan(0));
            Assert.That(_sample.ActionCount, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetButton_ClearsBothProcessors()
        {
            Click(InputAssistBasicsController.DiagonalButtonElementName);
            Click(InputAssistBasicsController.TapButtonElementName);

            Click(InputAssistBasicsController.ResetButtonElementName);

            Assert.That(_sample.LastRawInput, Is.EqualTo(Vector2.zero));
            Assert.That(_sample.LastFilteredInput, Is.EqualTo(Vector2.zero));
            Assert.That(_sample.LastDirection, Is.EqualTo(InputDirection.Neutral));
            Assert.That(_sample.LastButtonEvents, Is.EqualTo(InputButtonEvent.None));
            Assert.That(_sample.LastTapCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Geometry_WideAndNarrowRemainContainedAndNonOverlapping()
        {
            yield return AssertGeometry(960, 600);
            ReplaceTarget(640, 360);
            yield return WaitUntil(() => Math.Abs(Root().worldBound.width - 640f) <= 1f && Math.Abs(Root().worldBound.height - 360f) <= 1f, "The panel did not switch to 640x360.");
            yield return null;
            yield return AssertGeometry(640, 360);
        }

        private IEnumerator AssertGeometry(int width, int height)
        {
            var root = Root();
            var card = Card();
            Assert.That(root.worldBound.width, Is.EqualTo(width).Within(1f));
            Assert.That(root.worldBound.height, Is.EqualTo(height).Within(1f));
            AssertContained(card.worldBound, root.worldBound, "card");

            var elements = new List<VisualElement>
            {
                root.Q<Label>(InputAssistBasicsController.TitleElementName),
                root.Q<Label>(InputAssistBasicsController.DescriptionElementName),
                root.Q<VisualElement>(InputAssistBasicsController.VectorStageElementName),
                root.Q<Label>(InputAssistBasicsController.VectorResultElementName),
                root.Q<Label>(InputAssistBasicsController.ButtonResultElementName)
            };
            foreach (var button in root.Query<Button>().ToList()) elements.Add(button);
            foreach (var element in elements)
            {
                Assert.That(element, Is.Not.Null);
                Assert.That(element.worldBound.width, Is.GreaterThan(0f), Describe(element));
                Assert.That(element.worldBound.height, Is.GreaterThan(0f), Describe(element));
                AssertContained(element.worldBound, card.worldBound, Describe(element));
            }

            var buttons = root.Query<Button>().ToList();
            for (var i = 0; i < buttons.Count; i++)
            {
                for (var j = i + 1; j < buttons.Count; j++)
                    Assert.That(OverlapsArea(buttons[i].worldBound, buttons[j].worldBound), Is.False, $"Buttons overlap: {Describe(buttons[i])} / {Describe(buttons[j])}");
            }
            yield return null;
        }

        private void Click(string name)
        {
            var button = Root().Q<Button>(name);
            Assert.That(button, Is.Not.Null);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private VisualElement Root()
        {
            return _document?.rootVisualElement;
        }

        private VisualElement Card()
        {
            return Root()?.Q<VisualElement>(InputAssistBasicsController.CardElementName);
        }

        private void ReplaceTarget(int width, int height)
        {
            var previous = _targetTexture;
            _targetTexture = CreateTarget(width, height);
            _panelSettings.targetTexture = _targetTexture;
            if (previous != null)
            {
                previous.Release();
                UnityEngine.Object.Destroy(previous);
            }
        }

        private static RenderTexture CreateTarget(int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            return target;
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failure)
        {
            for (var i = 0; i < 180; i++)
            {
                if (predicate()) yield break;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static void AssertContained(Rect inner, Rect outer, string name)
        {
            const float tolerance = 1f;
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance), name);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance), name);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance), name);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance), name);
        }

        private static bool OverlapsArea(Rect left, Rect right)
        {
            return Math.Min(left.xMax, right.xMax) - Math.Max(left.xMin, right.xMin) > 0.5f
                && Math.Min(left.yMax, right.yMax) - Math.Max(left.yMin, right.yMin) > 0.5f;
        }

        private static string Describe(VisualElement element)
        {
            var text = element is TextElement textElement ? textElement.text : string.Empty;
            return $"name={element.name}, text={text}, bounds={element.worldBound}";
        }

        private static PanelSettings LoadShippedPanelSettings()
        {
#if UNITY_EDITOR
            var path = AssetDatabase.GUIDToAssetPath(PanelSettingsGuid);
            Assert.That(path, Is.Not.Empty);
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
#else
            Assert.Fail("The imported sample geometry tests require the Unity Editor.");
            return null;
#endif
        }
    }
}
