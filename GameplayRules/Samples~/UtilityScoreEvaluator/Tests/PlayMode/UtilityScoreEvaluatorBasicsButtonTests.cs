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

namespace GameplayDecision.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、utility寄与明細、responsive geometryを検証します。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class UtilityScoreEvaluatorBasicsButtonTests
    {
        private const string PanelSettingsGuid = "a0270000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private UtilityScoreEvaluatorBasicsController _sample;
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
            _host = new GameObject("Utility Score Evaluator Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<UtilityScoreEvaluatorBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(UtilityScoreEvaluatorBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
            Assert.That(_sample.LastError, Is.EqualTo(UtilityScoreError.None));
            Assert.That(_sample.LastEvaluation, Is.Null);
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HighestAndWeightedButtons_SelectExpectedCandidate()
        {
            Click(UtilityScoreEvaluatorBasicsController.HighestButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.SelectedCandidateIdentifier, Is.EqualTo(2));
            AssertCandidate(0, 1, 0.3d);
            AssertCandidate(1, 2, 0.9d);
            AssertCandidate(2, 3, 0.6d);

            Click(UtilityScoreEvaluatorBasicsController.WeightedButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.SelectedCandidateIdentifier, Is.EqualTo(1));
            Assert.That(_sample.LastEvaluation.SelectedScore, Is.EqualTo(0.75d));
            AssertCandidate(0, 1, 0.75d);
            AssertCandidate(1, 2, 0.6d);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(2));
            Assert.That(_sample.LastInputPreserved, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TieLinesAndInvalidButtons_KeepExplicitBoundaries()
        {
            Click(UtilityScoreEvaluatorBasicsController.TieButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.SelectedCandidateIdentifier, Is.EqualTo(20));
            Assert.That(_sample.LastEvaluation.SelectedInputIndex, Is.Zero);

            Click(UtilityScoreEvaluatorBasicsController.LinesButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastEvaluation.SelectedCandidateIdentifier, Is.EqualTo(7));
            Assert.That(_sample.LastEvaluation.TryGetCandidateLine(0, out var candidate), Is.True);
            AssertFactor(candidate, 0, 30, 0.8d);
            AssertFactor(candidate, 1, 10, 1d);
            AssertFactor(candidate, 2, 20, 1d);

            Click(UtilityScoreEvaluatorBasicsController.InvalidButtonElementName);
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(UtilityScoreError.DuplicateCandidateIdentifier));
            Assert.That(_sample.LastEvaluation, Is.Null);
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(3));
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

        private void AssertCandidate(int index, int identifier, double score)
        {
            Assert.That(_sample.LastEvaluation.TryGetCandidateLine(index, out var line), Is.True);
            Assert.That(line.CandidateIdentifier, Is.EqualTo(identifier));
            Assert.That(line.Score, Is.EqualTo(score).Within(1e-12d));
            Assert.That(_sample.LastError, Is.EqualTo(UtilityScoreError.None));
        }

        private static void AssertFactor(UtilityScoreCandidateLine candidate, int index, int identifier, double contribution)
        {
            Assert.That(candidate.TryGetFactorLine(index, out var line), Is.True);
            Assert.That(line.FactorIdentifier, Is.EqualTo(identifier));
            Assert.That(line.WeightedUtility, Is.EqualTo(contribution).Within(1e-12d));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(UtilityScoreEvaluatorBasicsController.CardElementName);
            var names = new[]
            {
                UtilityScoreEvaluatorBasicsController.TitleElementName,
                UtilityScoreEvaluatorBasicsController.DescriptionElementName,
                UtilityScoreEvaluatorBasicsController.ConfigurationElementName,
                UtilityScoreEvaluatorBasicsController.InputElementName,
                UtilityScoreEvaluatorBasicsController.StageElementName,
                UtilityScoreEvaluatorBasicsController.ResultElementName,
                UtilityScoreEvaluatorBasicsController.HighestButtonElementName,
                UtilityScoreEvaluatorBasicsController.WeightedButtonElementName,
                UtilityScoreEvaluatorBasicsController.TieButtonElementName,
                UtilityScoreEvaluatorBasicsController.LinesButtonElementName,
                UtilityScoreEvaluatorBasicsController.InvalidButtonElementName
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
                root.Q<Button>(UtilityScoreEvaluatorBasicsController.HighestButtonElementName),
                root.Q<Button>(UtilityScoreEvaluatorBasicsController.WeightedButtonElementName),
                root.Q<Button>(UtilityScoreEvaluatorBasicsController.TieButtonElementName),
                root.Q<Button>(UtilityScoreEvaluatorBasicsController.LinesButtonElementName),
                root.Q<Button>(UtilityScoreEvaluatorBasicsController.InvalidButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Utility Score Evaluator Test {width}x{height}" };
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
