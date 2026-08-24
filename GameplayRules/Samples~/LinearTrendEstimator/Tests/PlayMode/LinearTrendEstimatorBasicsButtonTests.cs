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

namespace GameplayAnalysis.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、trend推定、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class LinearTrendEstimatorBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fb220000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private LinearTrendEstimatorBasicsController _sample;
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
            _host = new GameObject("Linear Trend Estimator Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<LinearTrendEstimatorBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(LinearTrendEstimatorBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_HasNoPartialEstimate()
        {
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(LinearTrendError.None));
            Assert.That(_sample.LastEstimate, Is.EqualTo(default(LinearTrendEstimate)));
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RisingFlatAndFallingButtons_ReturnExpectedLines()
        {
            Click(LinearTrendEstimatorBasicsController.RisingButtonElementName);
            AssertSuccess(10d, 10d, 50d, 1);
            Click(LinearTrendEstimatorBasicsController.FlatButtonElementName);
            AssertSuccess(0d, 20d, 20d, 2);
            Click(LinearTrendEstimatorBasicsController.FallingButtonElementName);
            AssertSuccess(-10d, 40d, 0d, 3);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoisyAndExtremeButtons_ReturnFitThenExplicitFailure()
        {
            Click(LinearTrendEstimatorBasicsController.NoisyButtonElementName);
            AssertSuccess(8d, 13d, 45d, 1);
            Click(LinearTrendEstimatorBasicsController.ExtremeButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(LinearTrendError.ResultOutOfRange));
            Assert.That(_sample.LastEstimate, Is.EqualTo(default(LinearTrendEstimate)));
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

        private void AssertSuccess(double slope, double intercept, double prediction, int actions)
        {
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastError, Is.EqualTo(LinearTrendError.None));
            Assert.That(_sample.LastEstimate.SlopePerSample, Is.EqualTo(slope).Within(1e-12d));
            Assert.That(_sample.LastEstimate.InterceptAtIndexZero, Is.EqualTo(intercept).Within(1e-12d));
            Assert.That(_sample.LastEstimate.PredictedNextSample, Is.EqualTo(prediction).Within(1e-12d));
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(actions));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(LinearTrendEstimatorBasicsController.CardElementName);
            var names = new[]
            {
                LinearTrendEstimatorBasicsController.TitleElementName,
                LinearTrendEstimatorBasicsController.DescriptionElementName,
                LinearTrendEstimatorBasicsController.ConfigurationElementName,
                LinearTrendEstimatorBasicsController.InputElementName,
                LinearTrendEstimatorBasicsController.StageElementName,
                LinearTrendEstimatorBasicsController.ResultElementName,
                LinearTrendEstimatorBasicsController.RisingButtonElementName,
                LinearTrendEstimatorBasicsController.FlatButtonElementName,
                LinearTrendEstimatorBasicsController.FallingButtonElementName,
                LinearTrendEstimatorBasicsController.NoisyButtonElementName,
                LinearTrendEstimatorBasicsController.ExtremeButtonElementName
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
                root.Q<Button>(LinearTrendEstimatorBasicsController.RisingButtonElementName),
                root.Q<Button>(LinearTrendEstimatorBasicsController.FlatButtonElementName),
                root.Q<Button>(LinearTrendEstimatorBasicsController.FallingButtonElementName),
                root.Q<Button>(LinearTrendEstimatorBasicsController.NoisyButtonElementName),
                root.Q<Button>(LinearTrendEstimatorBasicsController.ExtremeButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Linear Trend Estimator Test {width}x{height}" };
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
    }
}
