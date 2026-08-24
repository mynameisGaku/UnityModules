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
    /// <summary>import済みBasicsの実Button、維持・切替理由、responsive geometryを検証します。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class StableScoreSelectorBasicsButtonTests
    {
        private const string PanelSettingsGuid = "a0280000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private StableScoreSelectorBasicsController _sample;
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
            _host = new GameObject("Stable Score Selector Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<StableScoreSelectorBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(StableScoreSelectorBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_HasNoPartialSelection()
        {
            Assert.That(_sample.LastSucceeded, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(StableScoreError.None));
            Assert.That(_sample.LastSelection, Is.Null);
            Assert.That(_sample.LastInputPreserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectKeepAndSwitchButtons_ExposeStableDecision()
        {
            Click(StableScoreSelectorBasicsController.SelectButtonElementName);
            AssertSelection(2, StableScoreDecisionReason.SelectedWithoutCurrent, false);

            Click(StableScoreSelectorBasicsController.KeepButtonElementName);
            AssertSelection(1, StableScoreDecisionReason.KeptCurrentBelowMinimumAdvantage, false);
            Assert.That(_sample.LastSelection.ChallengerCandidateIdentifier, Is.EqualTo(2));
            Assert.That(_sample.LastSelection.ChallengerAdvantage, Is.EqualTo(0.06d).Within(1e-12d));

            Click(StableScoreSelectorBasicsController.SwitchButtonElementName);
            AssertSelection(2, StableScoreDecisionReason.SwitchedByMinimumAdvantage, true);
            Assert.That(_sample.LastSelection.ChallengerAdvantage, Is.EqualTo(0.16d).Within(1e-12d));
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(3));
            Assert.That(_sample.LastInputPreserved, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TieMissingAndInvalidInput_KeepExplicitBoundaries()
        {
            Click(StableScoreSelectorBasicsController.TieButtonElementName);
            AssertSelection(20, StableScoreDecisionReason.KeptCurrentTieOrLower, false);
            Assert.That(_sample.LastSelection.BestCandidateIdentifier, Is.EqualTo(10));
            Assert.That(_sample.LastSelection.ChallengerCandidateIdentifier, Is.EqualTo(10));

            Click(StableScoreSelectorBasicsController.MissingButtonElementName);
            AssertSelection(8, StableScoreDecisionReason.ReplacedMissingCurrent, false);
            Assert.That(_sample.LastSelection.ChangedFromRequestedCurrent, Is.True);
            Assert.That(_sample.LastSelection.CurrentWasAvailable, Is.False);

            var invalid = new[] { new StableScoreCandidate(1, 0.2d), new StableScoreCandidate(1, 0.8d) };
            Assert.That(StableScoreSelector.TrySelect(invalid, 0, 0.1d, out var selection, out var error), Is.False);
            Assert.That(selection, Is.Null);
            Assert.That(error, Is.EqualTo(StableScoreError.DuplicateCandidateIdentifier));
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

        private void AssertSelection(int identifier, StableScoreDecisionReason reason, bool switched)
        {
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastError, Is.EqualTo(StableScoreError.None));
            Assert.That(_sample.LastSelection, Is.Not.Null);
            Assert.That(_sample.LastSelection.SelectedCandidateIdentifier, Is.EqualTo(identifier));
            Assert.That(_sample.LastSelection.Reason, Is.EqualTo(reason));
            Assert.That(_sample.LastSelection.SwitchedFromAvailableCurrent, Is.EqualTo(switched));
            Assert.That(_sample.LastInputPreserved, Is.True);
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(StableScoreSelectorBasicsController.CardElementName);
            var names = new[]
            {
                StableScoreSelectorBasicsController.TitleElementName,
                StableScoreSelectorBasicsController.DescriptionElementName,
                StableScoreSelectorBasicsController.ConfigurationElementName,
                StableScoreSelectorBasicsController.InputElementName,
                StableScoreSelectorBasicsController.StageElementName,
                StableScoreSelectorBasicsController.ResultElementName,
                StableScoreSelectorBasicsController.SelectButtonElementName,
                StableScoreSelectorBasicsController.KeepButtonElementName,
                StableScoreSelectorBasicsController.SwitchButtonElementName,
                StableScoreSelectorBasicsController.TieButtonElementName,
                StableScoreSelectorBasicsController.MissingButtonElementName
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
                root.Q<Button>(StableScoreSelectorBasicsController.SelectButtonElementName),
                root.Q<Button>(StableScoreSelectorBasicsController.KeepButtonElementName),
                root.Q<Button>(StableScoreSelectorBasicsController.SwitchButtonElementName),
                root.Q<Button>(StableScoreSelectorBasicsController.TieButtonElementName),
                root.Q<Button>(StableScoreSelectorBasicsController.MissingButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Stable Score Selector Test {width}x{height}" };
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
