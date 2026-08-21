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

namespace GameplayProgression.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、tier評価、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class ThresholdTierTableBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fa210000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private ThresholdTierTableBasicsController _sample;
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
            _host = new GameObject("Threshold Tier Table Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<ThresholdTierTableBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(ThresholdTierTableBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_HasThreeConfiguredTiers()
        {
            Assert.That(_sample.TierCount, Is.EqualTo(3));
            Assert.That(_sample.HasEvaluation, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BelowBronzeAndMidButtons_ExposeInclusiveProgress()
        {
            Click(ThresholdTierTableBasicsController.BelowButtonElementName);
            AssertEvaluation(-10d, false, 0, true, 1, 0d);
            Click(ThresholdTierTableBasicsController.BronzeButtonElementName);
            AssertEvaluation(0d, true, 1, true, 2, 0d);
            Click(ThresholdTierTableBasicsController.MidButtonElementName);
            AssertEvaluation(50d, true, 1, true, 2, 0.5d);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SilverGoldAndGoldButtons_ExposeSegmentAndTerminalStates()
        {
            Click(ThresholdTierTableBasicsController.SilverGoldButtonElementName);
            AssertEvaluation(250d, true, 2, true, 3, 0.75d);
            Click(ThresholdTierTableBasicsController.GoldButtonElementName);
            AssertEvaluation(500d, true, 3, false, 0, 1d);
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

        private void AssertEvaluation(double query, bool hasCurrent, int currentId, bool hasNext, int nextId, double progress)
        {
            Assert.That(_sample.HasEvaluation, Is.True);
            var evaluation = _sample.LastEvaluation;
            Assert.That(evaluation.QueryValue, Is.EqualTo(query));
            Assert.That(evaluation.HasCurrentTier, Is.EqualTo(hasCurrent));
            if (hasCurrent) Assert.That(evaluation.CurrentTier.Id, Is.EqualTo(currentId));
            Assert.That(evaluation.HasNextTier, Is.EqualTo(hasNext));
            if (hasNext) Assert.That(evaluation.NextTier.Id, Is.EqualTo(nextId));
            Assert.That(evaluation.ProgressToNext, Is.EqualTo(progress).Within(1e-12d));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(ThresholdTierTableBasicsController.CardElementName);
            var names = new[]
            {
                ThresholdTierTableBasicsController.TitleElementName,
                ThresholdTierTableBasicsController.DescriptionElementName,
                ThresholdTierTableBasicsController.ConfigurationElementName,
                ThresholdTierTableBasicsController.InputElementName,
                ThresholdTierTableBasicsController.StageElementName,
                ThresholdTierTableBasicsController.ResultElementName,
                ThresholdTierTableBasicsController.BelowButtonElementName,
                ThresholdTierTableBasicsController.BronzeButtonElementName,
                ThresholdTierTableBasicsController.MidButtonElementName,
                ThresholdTierTableBasicsController.SilverGoldButtonElementName,
                ThresholdTierTableBasicsController.GoldButtonElementName
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
                root.Q<Button>(ThresholdTierTableBasicsController.BelowButtonElementName),
                root.Q<Button>(ThresholdTierTableBasicsController.BronzeButtonElementName),
                root.Q<Button>(ThresholdTierTableBasicsController.MidButtonElementName),
                root.Q<Button>(ThresholdTierTableBasicsController.SilverGoldButtonElementName),
                root.Q<Button>(ThresholdTierTableBasicsController.GoldButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Threshold Tier Table Test {width}x{height}" };
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
