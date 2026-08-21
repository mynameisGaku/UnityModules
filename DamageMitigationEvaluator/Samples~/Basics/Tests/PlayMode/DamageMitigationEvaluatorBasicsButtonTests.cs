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

namespace GameplayDamage.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、damage軽減明細、responsive geometryを検証します。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class DamageMitigationEvaluatorBasicsButtonTests
    {
        private const string PanelSettingsGuid = "b8e40000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private DamageMitigationEvaluatorBasicsController _sample;
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
            _host = new GameObject("Damage Mitigation Evaluator Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<DamageMitigationEvaluatorBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(DamageMitigationEvaluatorBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_HasNoPartialEvaluation()
        {
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(DamageMitigationError.None));
            Assert.That(_sample.LastEvaluation, Is.Null);
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlatRatioAndOrderedButtons_ReturnCompleteBreakdowns()
        {
            Click(DamageMitigationEvaluatorBasicsController.FlatButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.FinalDamage, Is.EqualTo(75d));
            AssertStep(0, 1, DamageMitigationKind.FlatReduction, 100d, 25d, 25d, 75d, false);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(1));

            Click(DamageMitigationEvaluatorBasicsController.RatioButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.FinalDamage, Is.EqualTo(75d));
            AssertStep(0, 1, DamageMitigationKind.RatioReduction, 100d, 25d, 25d, 75d, false);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(2));

            Click(DamageMitigationEvaluatorBasicsController.OrderedButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.FinalDamage, Is.EqualTo(60d));
            AssertStep(0, 1, DamageMitigationKind.FlatReduction, 100d, 20d, 20d, 80d, false);
            AssertStep(1, 2, DamageMitigationKind.RatioReduction, 80d, 20d, 20d, 60d, false);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(3));
            Assert.That(_sample.LastInputPreserved, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClampAndInvalidButtons_KeepExplicitBoundaries()
        {
            Click(DamageMitigationEvaluatorBasicsController.ClampButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.FinalDamage, Is.Zero);
            Assert.That(_sample.LastEvaluation.WasFullyMitigated, Is.True);
            AssertStep(0, 1, DamageMitigationKind.FlatReduction, 100d, 120d, 100d, 0d, true);

            Click(DamageMitigationEvaluatorBasicsController.InvalidButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(DamageMitigationError.DuplicateLayerId));
            Assert.That(_sample.LastEvaluation, Is.Null);
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

        private void AssertStep(int index, int layerId, DamageMitigationKind kind, double input, double requested, double applied, double output, bool clamped)
        {
            Assert.That(_sample.LastEvaluation.TryGetStep(index, out var step), Is.True);
            Assert.That(step.LayerId, Is.EqualTo(layerId));
            Assert.That(step.Kind, Is.EqualTo(kind));
            Assert.That(step.InputDamage, Is.EqualTo(input));
            Assert.That(step.RequestedReduction, Is.EqualTo(requested));
            Assert.That(step.AppliedReduction, Is.EqualTo(applied));
            Assert.That(step.OutputDamage, Is.EqualTo(output));
            Assert.That(step.WasClamped, Is.EqualTo(clamped));
            Assert.That(_sample.LastError, Is.EqualTo(DamageMitigationError.None));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(DamageMitigationEvaluatorBasicsController.CardElementName);
            var names = new[]
            {
                DamageMitigationEvaluatorBasicsController.TitleElementName,
                DamageMitigationEvaluatorBasicsController.DescriptionElementName,
                DamageMitigationEvaluatorBasicsController.ConfigurationElementName,
                DamageMitigationEvaluatorBasicsController.InputElementName,
                DamageMitigationEvaluatorBasicsController.StageElementName,
                DamageMitigationEvaluatorBasicsController.ResultElementName,
                DamageMitigationEvaluatorBasicsController.FlatButtonElementName,
                DamageMitigationEvaluatorBasicsController.RatioButtonElementName,
                DamageMitigationEvaluatorBasicsController.OrderedButtonElementName,
                DamageMitigationEvaluatorBasicsController.ClampButtonElementName,
                DamageMitigationEvaluatorBasicsController.InvalidButtonElementName
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
                root.Q<Button>(DamageMitigationEvaluatorBasicsController.FlatButtonElementName),
                root.Q<Button>(DamageMitigationEvaluatorBasicsController.RatioButtonElementName),
                root.Q<Button>(DamageMitigationEvaluatorBasicsController.OrderedButtonElementName),
                root.Q<Button>(DamageMitigationEvaluatorBasicsController.ClampButtonElementName),
                root.Q<Button>(DamageMitigationEvaluatorBasicsController.InvalidButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Damage Mitigation Evaluator Test {width}x{height}" };
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
