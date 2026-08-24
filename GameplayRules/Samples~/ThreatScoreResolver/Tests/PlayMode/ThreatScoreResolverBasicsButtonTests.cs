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

namespace GameplayThreat.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、threat明細、responsive geometryを検証します。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class ThreatScoreResolverBasicsButtonTests
    {
        private const string PanelSettingsGuid = "b9e50000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private ThreatScoreResolverBasicsController _sample;
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
            _host = new GameObject("Threat Score Resolver Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<ThreatScoreResolverBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(ThreatScoreResolverBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_HasNoPartialResolution()
        {
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(ThreatScoreError.None));
            Assert.That(_sample.LastResolution, Is.Null);
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AddReduceAndOrderedButtons_ReturnCompleteBreakdowns()
        {
            Click(ThreatScoreResolverBasicsController.AddButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastResolution.LeaderTargetId, Is.EqualTo(1));
            AssertStep(0, 1, 10d, 15d, 15d, 25d, false);

            Click(ThreatScoreResolverBasicsController.ReduceButtonElementName);
            Assert.That(_sample.LastResolution.LeaderTargetId, Is.EqualTo(2));
            AssertStep(0, 1, 30d, -12d, -12d, 18d, false);

            Click(ThreatScoreResolverBasicsController.OrderedButtonElementName);
            Assert.That(_sample.LastResolution.LeaderTargetId, Is.EqualTo(2));
            Assert.That(_sample.LastResolution.LeaderScore, Is.EqualTo(45d));
            AssertStep(0, 1, 10d, 20d, 20d, 30d, false);
            AssertStep(1, 2, 5d, 40d, 40d, 45d, false);
            AssertStep(2, 1, 30d, -8d, -8d, 22d, false);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(3));
            Assert.That(_sample.LastInputPreserved, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClampAndInvalidButtons_KeepExplicitBoundaries()
        {
            Click(ThreatScoreResolverBasicsController.ClampButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            AssertStep(0, 1, 10d, -50d, -10d, 0d, true);
            Assert.That(_sample.LastResolution.LeaderTargetId, Is.EqualTo(1));

            Click(ThreatScoreResolverBasicsController.InvalidButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(ThreatScoreError.UnknownTargetId));
            Assert.That(_sample.LastResolution, Is.Null);
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(2));
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

        private void AssertStep(int index, int targetId, double input, double requested, double applied, double output, bool clamped)
        {
            Assert.That(_sample.LastResolution.TryGetStep(index, out var step), Is.True);
            Assert.That(step.TargetId, Is.EqualTo(targetId));
            Assert.That(step.InputScore, Is.EqualTo(input));
            Assert.That(step.RequestedDelta, Is.EqualTo(requested));
            Assert.That(step.AppliedDelta, Is.EqualTo(applied));
            Assert.That(step.OutputScore, Is.EqualTo(output));
            Assert.That(step.WasClamped, Is.EqualTo(clamped));
            Assert.That(_sample.LastError, Is.EqualTo(ThreatScoreError.None));
        }

        private IEnumerator AssertGeometry(bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(ThreatScoreResolverBasicsController.CardElementName);
            var names = new[]
            {
                ThreatScoreResolverBasicsController.TitleElementName,
                ThreatScoreResolverBasicsController.DescriptionElementName,
                ThreatScoreResolverBasicsController.ConfigurationElementName,
                ThreatScoreResolverBasicsController.InputElementName,
                ThreatScoreResolverBasicsController.StageElementName,
                ThreatScoreResolverBasicsController.ResultElementName,
                ThreatScoreResolverBasicsController.AddButtonElementName,
                ThreatScoreResolverBasicsController.ReduceButtonElementName,
                ThreatScoreResolverBasicsController.OrderedButtonElementName,
                ThreatScoreResolverBasicsController.ClampButtonElementName,
                ThreatScoreResolverBasicsController.InvalidButtonElementName
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
                root.Q<Button>(ThreatScoreResolverBasicsController.AddButtonElementName),
                root.Q<Button>(ThreatScoreResolverBasicsController.ReduceButtonElementName),
                root.Q<Button>(ThreatScoreResolverBasicsController.OrderedButtonElementName),
                root.Q<Button>(ThreatScoreResolverBasicsController.ClampButtonElementName),
                root.Q<Button>(ThreatScoreResolverBasicsController.InvalidButtonElementName)
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
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(button, Is.Not.Null, name);
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Threat Score Resolver Test {width}x{height}" };
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
