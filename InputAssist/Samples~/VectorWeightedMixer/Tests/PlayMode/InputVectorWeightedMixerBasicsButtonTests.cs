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

namespace InputMixing.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、weighted mix結果、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputVectorWeightedMixerBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fd000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private InputVectorWeightedMixerBasicsController _sample;
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
            _host = new GameObject("Input Vector Weighted Mixer Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputVectorWeightedMixerBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(InputVectorWeightedMixerBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_IsNeutralAndHealthy()
        {
            Assert.That(_sample.LastResult.Succeeded, Is.True);
            AssertMix(0d, 0d, 0d, 0, 0);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScenarioButtons_ProduceGoldenMixResults()
        {
            Click(InputVectorWeightedMixerBasicsController.EqualButtonElementName);
            AssertMix(0.5d, 0.5d, 2d, 2, 2);
            Click(InputVectorWeightedMixerBasicsController.PlayerHeavyButtonElementName);
            AssertMix(0.75d, 0.25d, 1d, 2, 2);
            Click(InputVectorWeightedMixerBasicsController.ZeroWeightButtonElementName);
            AssertMix(0.4d, -0.2d, 1d, 2, 1);
            Click(InputVectorWeightedMixerBasicsController.EmptyButtonElementName);
            AssertMix(0d, 0d, 0d, 0, 0);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectButton_ReportsExactFailureIndex()
        {
            Click(InputVectorWeightedMixerBasicsController.RejectButtonElementName);
            Assert.That(_sample.LastResult.Succeeded, Is.False);
            Assert.That(_sample.LastResult.Error, Is.EqualTo(InputVectorWeightedMixerError.WeightOutOfRange));
            Assert.That(_sample.LastResult.ContributionCount, Is.EqualTo(2));
            Assert.That(_sample.LastResult.InvalidContributionIndex, Is.EqualTo(1));
            Assert.That(_sample.RejectionObserved, Is.True);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(1));
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
            var card = root.Q<VisualElement>(InputVectorWeightedMixerBasicsController.CardElementName);
            var names = new[]
            {
                InputVectorWeightedMixerBasicsController.TitleElementName,
                InputVectorWeightedMixerBasicsController.DescriptionElementName,
                InputVectorWeightedMixerBasicsController.ConfigurationElementName,
                InputVectorWeightedMixerBasicsController.InputElementName,
                InputVectorWeightedMixerBasicsController.StageElementName,
                InputVectorWeightedMixerBasicsController.ResultElementName,
                InputVectorWeightedMixerBasicsController.EqualButtonElementName,
                InputVectorWeightedMixerBasicsController.PlayerHeavyButtonElementName,
                InputVectorWeightedMixerBasicsController.ZeroWeightButtonElementName,
                InputVectorWeightedMixerBasicsController.EmptyButtonElementName,
                InputVectorWeightedMixerBasicsController.RejectButtonElementName
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
                root.Q<Button>(InputVectorWeightedMixerBasicsController.EqualButtonElementName),
                root.Q<Button>(InputVectorWeightedMixerBasicsController.PlayerHeavyButtonElementName),
                root.Q<Button>(InputVectorWeightedMixerBasicsController.ZeroWeightButtonElementName),
                root.Q<Button>(InputVectorWeightedMixerBasicsController.EmptyButtonElementName),
                root.Q<Button>(InputVectorWeightedMixerBasicsController.RejectButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Input Vector Weighted Mixer Test {width}x{height}" };
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

        private void AssertMix(double horizontal, double vertical, double totalWeight, int contributionCount, int activeCount)
        {
            Assert.That(_sample.LastResult.Succeeded, Is.True);
            Assert.That(_sample.LastResult.Horizontal, Is.EqualTo(horizontal).Within(1e-12d));
            Assert.That(_sample.LastResult.Vertical, Is.EqualTo(vertical).Within(1e-12d));
            Assert.That(_sample.LastResult.TotalWeight, Is.EqualTo(totalWeight).Within(1e-12d));
            Assert.That(_sample.LastResult.ContributionCount, Is.EqualTo(contributionCount));
            Assert.That(_sample.LastResult.ActiveContributionCount, Is.EqualTo(activeCount));
            Assert.That(_sample.LastResult.InvalidContributionIndex, Is.EqualTo(-1));
        }
    }
}
