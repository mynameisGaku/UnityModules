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

namespace GameplayStats.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、modifier合成結果、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class StatModifierStackBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fa100000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private StatModifierStackBasicsController _sample;
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
            _host = new GameObject("Stat Modifier Stack Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<StatModifierStackBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(StatModifierStackBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_IsFiniteAndEmpty()
        {
            AssertState(100d, 0d, 0d, 1d, 0);
            Assert.That(_sample.LastResult.Succeeded, Is.True);
            Assert.That(_sample.LastResult.Changed, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScenarioButtons_ProduceGoldenStackStates()
        {
            RunGoldenSequence();
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectDuplicate_PreservesLastSuccessfulState()
        {
            RunGoldenSequence();
            var before = _sample.CurrentValue;
            Click(StatModifierStackBasicsController.RejectDuplicateButtonElementName);
            Assert.That(_sample.CurrentValue, Is.EqualTo(before));
            Assert.That(_sample.LastResult.Succeeded, Is.False);
            Assert.That(_sample.LastResult.Error, Is.EqualTo(StatModifierError.DuplicateModifierId));
            Assert.That(_sample.LastResult.AffectedModifierId, Is.EqualTo(10));
            Assert.That(_sample.RejectionPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(5));
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

        private void RunGoldenSequence()
        {
            Click(StatModifierStackBasicsController.AddFlatButtonElementName);
            AssertState(115d, 15d, 0d, 1d, 1);
            AssertEvaluation(100d, 115d, 10);
            Click(StatModifierStackBasicsController.AddPercentButtonElementName);
            AssertState(138d, 15d, 0.2d, 1d, 2);
            AssertEvaluation(115d, 138d, 20);
            Click(StatModifierStackBasicsController.AddFactorButtonElementName);
            AssertState(207d, 15d, 0.2d, 1.5d, 3);
            AssertEvaluation(138d, 207d, 30);
            Click(StatModifierStackBasicsController.UpdateFactorButtonElementName);
            AssertState(69d, 15d, 0.2d, 0.5d, 3);
            AssertEvaluation(207d, 69d, 30);
        }

        private void AssertState(double current, double flat, double additivePercent, double factor, int count)
        {
            Assert.That(_sample.BaseValue, Is.EqualTo(100d));
            Assert.That(_sample.CurrentValue, Is.EqualTo(current).Within(1e-12d));
            Assert.That(_sample.FlatTotal, Is.EqualTo(flat).Within(1e-12d));
            Assert.That(_sample.AdditivePercentTotal, Is.EqualTo(additivePercent).Within(1e-12d));
            Assert.That(_sample.MultiplicativeFactor, Is.EqualTo(factor).Within(1e-12d));
            Assert.That(_sample.ModifierCount, Is.EqualTo(count));
        }

        private void AssertEvaluation(double previous, double current, long id)
        {
            Assert.That(_sample.LastResult.Succeeded, Is.True);
            Assert.That(_sample.LastResult.PreviousValue, Is.EqualTo(previous).Within(1e-12d));
            Assert.That(_sample.LastResult.CurrentValue, Is.EqualTo(current).Within(1e-12d));
            Assert.That(_sample.LastResult.AffectedModifierId, Is.EqualTo(id));
            Assert.That(_sample.LastResult.Error, Is.EqualTo(StatModifierError.None));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(StatModifierStackBasicsController.CardElementName);
            var names = new[]
            {
                StatModifierStackBasicsController.TitleElementName,
                StatModifierStackBasicsController.DescriptionElementName,
                StatModifierStackBasicsController.ConfigurationElementName,
                StatModifierStackBasicsController.InputElementName,
                StatModifierStackBasicsController.StageElementName,
                StatModifierStackBasicsController.ResultElementName,
                StatModifierStackBasicsController.AddFlatButtonElementName,
                StatModifierStackBasicsController.AddPercentButtonElementName,
                StatModifierStackBasicsController.AddFactorButtonElementName,
                StatModifierStackBasicsController.UpdateFactorButtonElementName,
                StatModifierStackBasicsController.RejectDuplicateButtonElementName
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
                root.Q<Button>(StatModifierStackBasicsController.AddFlatButtonElementName),
                root.Q<Button>(StatModifierStackBasicsController.AddPercentButtonElementName),
                root.Q<Button>(StatModifierStackBasicsController.AddFactorButtonElementName),
                root.Q<Button>(StatModifierStackBasicsController.UpdateFactorButtonElementName),
                root.Q<Button>(StatModifierStackBasicsController.RejectDuplicateButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Stat Modifier Stack Test {width}x{height}" };
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
