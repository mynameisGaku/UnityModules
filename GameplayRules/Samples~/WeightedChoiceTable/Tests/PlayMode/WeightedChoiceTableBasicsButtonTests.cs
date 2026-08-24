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

namespace GameplaySelection.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、累積区間選択、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class WeightedChoiceTableBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fd100000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private WeightedChoiceTableBasicsController _sample;
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
            _host = new GameObject("Weighted Choice Table Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<WeightedChoiceTableBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(WeightedChoiceTableBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
            AssertState(0d, 0);
            Assert.That(_sample.LastChange.Succeeded, Is.True);
            Assert.That(_sample.LastChange.Changed, Is.False);
            Assert.That(_sample.LastSelection.Succeeded, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScenarioButtons_BuildIntervalsAndSelectRare()
        {
            RunGoldenSequence();
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SecondSample_SelectsEpicWithoutMutation()
        {
            RunGoldenSequence();
            Click(WeightedChoiceTableBasicsController.SelectEpicButtonElementName);
            AssertState(10d, 3);
            AssertSelection(0.95d, 30, 2, 1d, 9d, 10d, 9.5d);
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
            Click(WeightedChoiceTableBasicsController.AddCommonButtonElementName);
            AssertState(6d, 1);
            AssertChange(10, 0d, 6d, 0d, 6d);
            Click(WeightedChoiceTableBasicsController.AddRareButtonElementName);
            AssertState(9d, 2);
            AssertChange(20, 0d, 3d, 6d, 9d);
            Click(WeightedChoiceTableBasicsController.AddEpicButtonElementName);
            AssertState(10d, 3);
            AssertChange(30, 0d, 1d, 9d, 10d);
            Click(WeightedChoiceTableBasicsController.SelectRareButtonElementName);
            AssertState(10d, 3);
            AssertSelection(0.65d, 20, 1, 3d, 6d, 9d, 6.5d);
        }

        private void AssertState(double totalWeight, int count)
        {
            Assert.That(_sample.TotalWeight, Is.EqualTo(totalWeight).Within(1e-12d));
            Assert.That(_sample.EntryCount, Is.EqualTo(count));
        }

        private void AssertChange(int identifier, double previousWeight, double currentWeight, double previousTotal, double currentTotal)
        {
            Assert.That(_sample.LastChange.Succeeded, Is.True);
            Assert.That(_sample.LastChange.AffectedIdentifier, Is.EqualTo(identifier));
            Assert.That(_sample.LastChange.PreviousWeight, Is.EqualTo(previousWeight));
            Assert.That(_sample.LastChange.CurrentWeight, Is.EqualTo(currentWeight));
            Assert.That(_sample.LastChange.PreviousTotalWeight, Is.EqualTo(previousTotal));
            Assert.That(_sample.LastChange.CurrentTotalWeight, Is.EqualTo(currentTotal));
            Assert.That(_sample.LastChange.Error, Is.EqualTo(WeightedChoiceError.None));
        }

        private void AssertSelection(double sample, int identifier, int index, double weight, double start, double end, double ticket)
        {
            Assert.That(_sample.LastSelection.Succeeded, Is.True);
            Assert.That(_sample.LastSelection.NormalizedSample, Is.EqualTo(sample));
            Assert.That(_sample.LastSelection.SelectedIdentifier, Is.EqualTo(identifier));
            Assert.That(_sample.LastSelection.SelectedIndex, Is.EqualTo(index));
            Assert.That(_sample.LastSelection.SelectedWeight, Is.EqualTo(weight));
            Assert.That(_sample.LastSelection.IntervalStart, Is.EqualTo(start));
            Assert.That(_sample.LastSelection.IntervalEnd, Is.EqualTo(end));
            Assert.That(_sample.LastSelection.Ticket, Is.EqualTo(ticket).Within(1e-12d));
            Assert.That(_sample.LastSelection.Error, Is.EqualTo(WeightedChoiceError.None));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(WeightedChoiceTableBasicsController.CardElementName);
            var names = new[]
            {
                WeightedChoiceTableBasicsController.TitleElementName,
                WeightedChoiceTableBasicsController.DescriptionElementName,
                WeightedChoiceTableBasicsController.ConfigurationElementName,
                WeightedChoiceTableBasicsController.InputElementName,
                WeightedChoiceTableBasicsController.StageElementName,
                WeightedChoiceTableBasicsController.ResultElementName,
                WeightedChoiceTableBasicsController.AddCommonButtonElementName,
                WeightedChoiceTableBasicsController.AddRareButtonElementName,
                WeightedChoiceTableBasicsController.AddEpicButtonElementName,
                WeightedChoiceTableBasicsController.SelectRareButtonElementName,
                WeightedChoiceTableBasicsController.SelectEpicButtonElementName
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
                root.Q<Button>(WeightedChoiceTableBasicsController.AddCommonButtonElementName),
                root.Q<Button>(WeightedChoiceTableBasicsController.AddRareButtonElementName),
                root.Q<Button>(WeightedChoiceTableBasicsController.AddEpicButtonElementName),
                root.Q<Button>(WeightedChoiceTableBasicsController.SelectRareButtonElementName),
                root.Q<Button>(WeightedChoiceTableBasicsController.SelectEpicButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Weighted Choice Table Test {width}x{height}" };
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
