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

namespace InputDeadZones.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、補正結果、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputRadialDeadZoneBasicsButtonTests
    {
        private const string PanelSettingsGuid = "f9000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private InputRadialDeadZoneBasicsController _sample;
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
            _host = new GameObject("Input Radial Dead Zone Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputRadialDeadZoneBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(InputRadialDeadZoneBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_IsZeroAndHealthy()
        {
            AssertOutput(0d, 0d, 0d);
            Assert.That(_sample.LastError, Is.EqualTo(InputRadialDeadZoneError.None));
            Assert.That(_sample.NonFiniteRejected, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoldenButtons_ProduceContinuousRadialOutputs()
        {
            Click(InputRadialDeadZoneBasicsController.InnerButtonElementName);
            AssertOutput(0d, 0d, 0d);
            Click(InputRadialDeadZoneBasicsController.MidButtonElementName);
            AssertOutput(0.5d, 0d, 0.5d);
            Click(InputRadialDeadZoneBasicsController.OuterButtonElementName);
            AssertOutput(0d, 1d, 1d);
            Click(InputRadialDeadZoneBasicsController.OverRangeButtonElementName);
            AssertOutput(0.6d, 0.8d, 1d);
            Assert.That(_sample.LastHorizontal, Is.EqualTo(3d));
            Assert.That(_sample.LastVertical, Is.EqualTo(4d));
            Assert.That(_sample.LastError, Is.EqualTo(InputRadialDeadZoneError.None));
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonFiniteButton_PreservesLastSuccessfulOutput()
        {
            Click(InputRadialDeadZoneBasicsController.OverRangeButtonElementName);
            var beforeHorizontal = _sample.CurrentHorizontal;
            var beforeVertical = _sample.CurrentVertical;
            var beforeMagnitude = _sample.CurrentMagnitude;
            Click(InputRadialDeadZoneBasicsController.RejectNonFiniteButtonElementName);
            Assert.That(double.IsNaN(_sample.LastHorizontal), Is.True);
            Assert.That(_sample.CurrentHorizontal, Is.EqualTo(beforeHorizontal));
            Assert.That(_sample.CurrentVertical, Is.EqualTo(beforeVertical));
            Assert.That(_sample.CurrentMagnitude, Is.EqualTo(beforeMagnitude));
            Assert.That(_sample.LastError, Is.EqualTo(InputRadialDeadZoneError.NonFiniteInput));
            Assert.That(_sample.NonFiniteRejected, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Geometry_WideAndNarrowStayContained()
        {
            yield return AssertGeometry(960, 600, true);
            ReplaceTarget(640, 360);
            yield return WaitUntil(() => Math.Abs(ReadyRoot().worldBound.width - 640f) <= 1f && Math.Abs(ReadyRoot().worldBound.height - 360f) <= 1f, "640x360 panelへ切り替わりませんでした。");
            yield return null;
            yield return AssertGeometry(640, 360, false);
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(InputRadialDeadZoneBasicsController.CardElementName);
            var names = new[]
            {
                InputRadialDeadZoneBasicsController.TitleElementName,
                InputRadialDeadZoneBasicsController.DescriptionElementName,
                InputRadialDeadZoneBasicsController.ConfigurationElementName,
                InputRadialDeadZoneBasicsController.InputElementName,
                InputRadialDeadZoneBasicsController.StageElementName,
                InputRadialDeadZoneBasicsController.ResultElementName,
                InputRadialDeadZoneBasicsController.InnerButtonElementName,
                InputRadialDeadZoneBasicsController.MidButtonElementName,
                InputRadialDeadZoneBasicsController.OuterButtonElementName,
                InputRadialDeadZoneBasicsController.OverRangeButtonElementName,
                InputRadialDeadZoneBasicsController.RejectNonFiniteButtonElementName
            };
            var elements = names.Select(name => root.Q<VisualElement>(name)).ToArray();
            Assert.That(card, Is.Not.Null);
            Assert.That(elements.All(element => element != null), Is.True);
            var safe = new Rect(card.worldBound.xMin + 5f, card.worldBound.yMin + 5f, card.worldBound.width - 10f, card.worldBound.height - 10f);
            foreach (var element in elements)
            {
                var bounds = element.worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f), Describe(element, bounds, safe));
                Assert.That(bounds.height, Is.GreaterThan(0f), Describe(element, bounds, safe));
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 0.5f), Describe(element, bounds, safe));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + 0.5f), Describe(element, bounds, safe));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 0.5f), Describe(element, bounds, safe));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + 0.5f), Describe(element, bounds, safe));
            }

            for (var first = 0; first < elements.Length; first++)
            for (var second = first + 1; second < elements.Length; second++)
                Assert.That(elements[first].worldBound.Overlaps(elements[second].worldBound), Is.False, $"overlap: {elements[first].name} / {elements[second].name}");

            var buttons = new[]
            {
                root.Q<Button>(InputRadialDeadZoneBasicsController.InnerButtonElementName),
                root.Q<Button>(InputRadialDeadZoneBasicsController.MidButtonElementName),
                root.Q<Button>(InputRadialDeadZoneBasicsController.OuterButtonElementName),
                root.Q<Button>(InputRadialDeadZoneBasicsController.OverRangeButtonElementName),
                root.Q<Button>(InputRadialDeadZoneBasicsController.RejectNonFiniteButtonElementName)
            };
            if (wide)
            {
                Assert.That(buttons.All(button => Math.Abs(button.worldBound.yMin - buttons[0].worldBound.yMin) <= 0.5f), Is.True, $"{width}x{height}は5 Button 1列ではありません。");
            }
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
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledSelf, Is.True, name);
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Input Radial Dead Zone Test {width}x{height}" };
            target.Create();
            return target;
        }

        private static PanelSettings LoadShippedPanelSettings()
        {
#if UNITY_EDITOR
            var path = AssetDatabase.GUIDToAssetPath(PanelSettingsGuid);
            Assert.That(path, Is.Not.Empty, "配布PanelSettings GUIDを解決できません。");
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
#else
            Assert.Fail("このgeometry fixtureはUnity Editorで実行してください。");
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

        private static string Describe(VisualElement element, Rect bounds, Rect safe) => $"{element.name} text='{(element as TextElement)?.text}' bounds={bounds} safe={safe}";

        private void AssertOutput(double horizontal, double vertical, double magnitude)
        {
            Assert.That(_sample.CurrentHorizontal, Is.EqualTo(horizontal).Within(1e-12d));
            Assert.That(_sample.CurrentVertical, Is.EqualTo(vertical).Within(1e-12d));
            Assert.That(_sample.CurrentMagnitude, Is.EqualTo(magnitude).Within(1e-12d));
        }
    }
}
