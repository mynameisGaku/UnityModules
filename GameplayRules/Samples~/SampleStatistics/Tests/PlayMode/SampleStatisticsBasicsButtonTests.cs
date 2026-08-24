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
    /// <summary>import済みBasicsの実Button、要約統計、responsive geometryを検証します。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class SampleStatisticsBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fd240000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private SampleStatisticsBasicsController _sample;
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
            _host = new GameObject("Sample Statistics Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<SampleStatisticsBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(SampleStatisticsBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_HasNoPartialStatistics()
        {
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(SampleStatisticsError.None));
            Assert.That(_sample.LastResult, Is.EqualTo(default(SampleStatisticsResult)));
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BalancedConstantAndSpreadButtons_ReturnExpectedStatistics()
        {
            Click(SampleStatisticsBasicsController.BalancedButtonElementName);
            AssertSuccess(4, 1d, 4d, 2.5d, 3d, 1.25d, 1);
            Click(SampleStatisticsBasicsController.ConstantButtonElementName);
            AssertSuccess(4, 7d, 7d, 7d, 0d, 0d, 2);
            Click(SampleStatisticsBasicsController.SpreadButtonElementName);
            AssertSuccess(3, -10d, 10d, 0d, 20d, 200d / 3d, 3);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SubrangeAndExtremeButtons_ReturnSelectionThenExplicitFailure()
        {
            Click(SampleStatisticsBasicsController.SubrangeButtonElementName);
            AssertSuccess(3, 2d, 6d, 4d, 4d, 8d / 3d, 1);
            Click(SampleStatisticsBasicsController.ExtremeButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(SampleStatisticsError.ResultOutOfRange));
            Assert.That(_sample.LastResult, Is.EqualTo(default(SampleStatisticsResult)));
            Assert.That(_sample.LastInputPreserved, Is.True);
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

        private void AssertSuccess(int count, double minimum, double maximum, double mean, double range, double variance, int actions)
        {
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastError, Is.EqualTo(SampleStatisticsError.None));
            Assert.That(_sample.LastResult.SampleCount, Is.EqualTo(count));
            Assert.That(_sample.LastResult.Minimum, Is.EqualTo(minimum));
            Assert.That(_sample.LastResult.Maximum, Is.EqualTo(maximum));
            Assert.That(_sample.LastResult.Mean, Is.EqualTo(mean).Within(1e-12d));
            Assert.That(_sample.LastResult.Range, Is.EqualTo(range).Within(1e-12d));
            Assert.That(_sample.LastResult.PopulationVariance, Is.EqualTo(variance).Within(1e-12d));
            Assert.That(_sample.LastResult.PopulationStandardDeviation, Is.EqualTo(Math.Sqrt(variance)).Within(1e-12d));
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(actions));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(SampleStatisticsBasicsController.CardElementName);
            var names = new[]
            {
                SampleStatisticsBasicsController.TitleElementName,
                SampleStatisticsBasicsController.DescriptionElementName,
                SampleStatisticsBasicsController.ConfigurationElementName,
                SampleStatisticsBasicsController.InputElementName,
                SampleStatisticsBasicsController.StageElementName,
                SampleStatisticsBasicsController.ResultElementName,
                SampleStatisticsBasicsController.BalancedButtonElementName,
                SampleStatisticsBasicsController.ConstantButtonElementName,
                SampleStatisticsBasicsController.SpreadButtonElementName,
                SampleStatisticsBasicsController.SubrangeButtonElementName,
                SampleStatisticsBasicsController.ExtremeButtonElementName
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
                root.Q<Button>(SampleStatisticsBasicsController.BalancedButtonElementName),
                root.Q<Button>(SampleStatisticsBasicsController.ConstantButtonElementName),
                root.Q<Button>(SampleStatisticsBasicsController.SpreadButtonElementName),
                root.Q<Button>(SampleStatisticsBasicsController.SubrangeButtonElementName),
                root.Q<Button>(SampleStatisticsBasicsController.ExtremeButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Sample Statistics Test {width}x{height}" };
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
