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

namespace InputSmoothing.Samples.PlayMode.Tests
{
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputVectorSlewLimiterBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fa000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private InputVectorSlewLimiterBasicsController _sample;
        private PanelSettings _panelSettings;
        private RenderTexture _target;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var shipped = LoadPanel();
            Assert.That(shipped, Is.Not.Null);
            Assert.That(shipped.themeStyleSheet, Is.Not.Null);
            Assert.That(shipped.scaleMode, Is.EqualTo(PanelScaleMode.ConstantPixelSize));
            _panelSettings = UnityEngine.Object.Instantiate(shipped);
            _target = CreateTarget(960, 600);
            _panelSettings.targetTexture = _target;
            _host = new GameObject("Input Vector Slew Limiter Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputVectorSlewLimiterBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => Root()?.Q<VisualElement>(InputVectorSlewLimiterBasicsController.CardElementName)?.worldBound.width > 0f, "960x600 panelが準備されませんでした。");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); }
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialState_IsExplicitZero()
        {
            AssertCurrent(0d, 0d);
            Assert.That(_sample.LastResult.Succeeded, Is.True);
            Assert.That(_sample.LastResult.ReachedTarget, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoldenButtons_ApplyBoundedStepsAndReachNearbyTarget()
        {
            Click(InputVectorSlewLimiterBasicsController.OneStepButtonElementName);
            AssertCurrent(0.25d, 0d);
            Click(InputVectorSlewLimiterBasicsController.TwoStepsButtonElementName);
            AssertCurrent(0.5d, 0d);
            Click(InputVectorSlewLimiterBasicsController.DiagonalButtonElementName);
            AssertCurrent(0.15d, 0.2d);
            Click(InputVectorSlewLimiterBasicsController.ReachButtonElementName);
            AssertCurrent(0.1d, 0.1d);
            Assert.That(_sample.LastResult.ReachedTarget, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectButton_PreservesCurrentState()
        {
            Click(InputVectorSlewLimiterBasicsController.DiagonalButtonElementName);
            Click(InputVectorSlewLimiterBasicsController.RejectButtonElementName);
            AssertCurrent(0.15d, 0.2d);
            Assert.That(_sample.LastResult.Error, Is.EqualTo(InputVectorSlewLimiterError.InputOutOfRange));
            Assert.That(_sample.RejectionPreserved, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Geometry_WideAndNarrowStayContained()
        {
            yield return AssertGeometry(true);
            ReplaceTarget(640, 360);
            yield return WaitUntil(() => Math.Abs(Root().worldBound.width - 640f) <= 1f, "640x360 panelへ切り替わりませんでした。");
            yield return null;
            yield return AssertGeometry(false);
        }

        private IEnumerator AssertGeometry(bool wide)
        {
            var root = Root();
            var card = root.Q<VisualElement>(InputVectorSlewLimiterBasicsController.CardElementName);
            var names = new[] { InputVectorSlewLimiterBasicsController.TitleElementName, InputVectorSlewLimiterBasicsController.DescriptionElementName, InputVectorSlewLimiterBasicsController.ConfigurationElementName, InputVectorSlewLimiterBasicsController.InputElementName, InputVectorSlewLimiterBasicsController.StageElementName, InputVectorSlewLimiterBasicsController.ResultElementName, InputVectorSlewLimiterBasicsController.OneStepButtonElementName, InputVectorSlewLimiterBasicsController.TwoStepsButtonElementName, InputVectorSlewLimiterBasicsController.DiagonalButtonElementName, InputVectorSlewLimiterBasicsController.ReachButtonElementName, InputVectorSlewLimiterBasicsController.RejectButtonElementName };
            var elements = names.Select(name => root.Q<VisualElement>(name)).ToArray();
            Assert.That(card, Is.Not.Null);
            Assert.That(elements.All(value => value != null), Is.True);
            var safe = new Rect(card.worldBound.xMin + 5f, card.worldBound.yMin + 5f, card.worldBound.width - 10f, card.worldBound.height - 10f);
            foreach (var element in elements)
            {
                var bounds = element.worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f), element.name);
                Assert.That(bounds.height, Is.GreaterThan(0f), element.name);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 0.5f), element.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + 0.5f), element.name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 0.5f), element.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + 0.5f), element.name);
            }

            for (var first = 0; first < elements.Length; first++) for (var second = first + 1; second < elements.Length; second++) Assert.That(elements[first].worldBound.Overlaps(elements[second].worldBound), Is.False, $"{elements[first].name}/{elements[second].name}");
            var buttons = names.Skip(6).Select(name => root.Q<Button>(name)).ToArray();
            if (wide) Assert.That(buttons.All(value => Math.Abs(value.worldBound.yMin - buttons[0].worldBound.yMin) <= 0.5f), Is.True);
            else
            {
                Assert.That(buttons.Take(3).All(value => Math.Abs(value.worldBound.yMin - buttons[0].worldBound.yMin) <= 0.5f), Is.True);
                Assert.That(buttons[3].worldBound.yMin, Is.GreaterThan(buttons[0].worldBound.yMax));
                Assert.That(Math.Abs(buttons[4].worldBound.yMin - buttons[3].worldBound.yMin), Is.LessThanOrEqualTo(0.5f));
            }
            yield return null;
        }

        private void Click(string name)
        {
            var button = Root().Q<Button>(name);
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(button, Is.Not.Null);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private VisualElement Root() => _document?.rootVisualElement;
        private void AssertCurrent(double horizontal, double vertical) { Assert.That(_sample.CurrentHorizontal, Is.EqualTo(horizontal).Within(1e-12d)); Assert.That(_sample.CurrentVertical, Is.EqualTo(vertical).Within(1e-12d)); }
        private void ReplaceTarget(int width, int height) { var old = _target; _target = CreateTarget(width, height); _panelSettings.targetTexture = _target; old.Release(); UnityEngine.Object.Destroy(old); }
        private static RenderTexture CreateTarget(int width, int height) { var value = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32); value.Create(); return value; }
        private static PanelSettings LoadPanel()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(PanelSettingsGuid));
#else
            Assert.Fail("Editor専用geometry fixtureです。"); return null;
#endif
        }
        private static IEnumerator WaitUntil(Func<bool> predicate, string failure) { var deadline = Time.realtimeSinceStartupAsDouble + 5d; while (!predicate()) { if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(failure); yield return null; } }
    }
}
