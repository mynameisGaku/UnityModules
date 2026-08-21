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

namespace GameplayMath.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、piecewise補間、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class PiecewiseLinearCurveBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fe100000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private PiecewiseLinearCurveBasicsController _sample;
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
            _host = new GameObject("Piecewise Linear Curve Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<PiecewiseLinearCurveBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(PiecewiseLinearCurveBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
            AssertState(0);
            Assert.That(_sample.LastChange.Succeeded, Is.True);
            Assert.That(_sample.LastChange.Changed, Is.False);
            Assert.That(_sample.LastEvaluation.Succeeded, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScenarioButtons_BuildPointsAndEvaluateFive()
        {
            RunGoldenSequence();
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SecondQuery_EvaluatesFifteenWithoutMutation()
        {
            RunGoldenSequence();
            Click(PiecewiseLinearCurveBasicsController.EvaluateFifteenButtonElementName);
            AssertState(3);
            AssertEvaluation(15d, 75d, 1, 2, 10d, 100d, 20d, 50d, 0.5d, false);
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
            Click(PiecewiseLinearCurveBasicsController.AddStartButtonElementName);
            AssertState(1);
            AssertChange(0d, 0d, 0d, 0, 1);
            Click(PiecewiseLinearCurveBasicsController.AddPeakButtonElementName);
            AssertState(2);
            AssertChange(10d, 0d, 100d, 1, 2);
            Click(PiecewiseLinearCurveBasicsController.AddEndButtonElementName);
            AssertState(3);
            AssertChange(20d, 0d, 50d, 2, 3);
            Click(PiecewiseLinearCurveBasicsController.EvaluateFiveButtonElementName);
            AssertState(3);
            AssertEvaluation(5d, 50d, 0, 1, 0d, 0d, 10d, 100d, 0.5d, false);
        }

        private void AssertState(int count)
        {
            Assert.That(_sample.PointCount, Is.EqualTo(count));
        }

        private void AssertChange(double x, double previousY, double currentY, int previousCount, int currentCount)
        {
            Assert.That(_sample.LastChange.Succeeded, Is.True);
            Assert.That(_sample.LastChange.AffectedX, Is.EqualTo(x));
            Assert.That(_sample.LastChange.PreviousY, Is.EqualTo(previousY));
            Assert.That(_sample.LastChange.CurrentY, Is.EqualTo(currentY));
            Assert.That(_sample.LastChange.PreviousPointCount, Is.EqualTo(previousCount));
            Assert.That(_sample.LastChange.CurrentPointCount, Is.EqualTo(currentCount));
            Assert.That(_sample.LastChange.Error, Is.EqualTo(CurveError.None));
        }

        private void AssertEvaluation(double query, double value, int lowerIndex, int upperIndex, double lowerX, double lowerY, double upperX, double upperY, double interpolation, bool clamped)
        {
            Assert.That(_sample.LastEvaluation.Succeeded, Is.True);
            Assert.That(_sample.LastEvaluation.Query, Is.EqualTo(query));
            Assert.That(_sample.LastEvaluation.Value, Is.EqualTo(value).Within(1e-12d));
            Assert.That(_sample.LastEvaluation.LowerIndex, Is.EqualTo(lowerIndex));
            Assert.That(_sample.LastEvaluation.UpperIndex, Is.EqualTo(upperIndex));
            Assert.That(_sample.LastEvaluation.LowerPoint.X, Is.EqualTo(lowerX));
            Assert.That(_sample.LastEvaluation.LowerPoint.Y, Is.EqualTo(lowerY));
            Assert.That(_sample.LastEvaluation.UpperPoint.X, Is.EqualTo(upperX));
            Assert.That(_sample.LastEvaluation.UpperPoint.Y, Is.EqualTo(upperY));
            Assert.That(_sample.LastEvaluation.Interpolation, Is.EqualTo(interpolation).Within(1e-12d));
            Assert.That(_sample.LastEvaluation.Clamped, Is.EqualTo(clamped));
            Assert.That(_sample.LastEvaluation.Error, Is.EqualTo(CurveError.None));
        }

        private IEnumerator AssertGeometry(int width, int height, bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(PiecewiseLinearCurveBasicsController.CardElementName);
            var names = new[]
            {
                PiecewiseLinearCurveBasicsController.TitleElementName,
                PiecewiseLinearCurveBasicsController.DescriptionElementName,
                PiecewiseLinearCurveBasicsController.ConfigurationElementName,
                PiecewiseLinearCurveBasicsController.InputElementName,
                PiecewiseLinearCurveBasicsController.StageElementName,
                PiecewiseLinearCurveBasicsController.ResultElementName,
                PiecewiseLinearCurveBasicsController.AddStartButtonElementName,
                PiecewiseLinearCurveBasicsController.AddPeakButtonElementName,
                PiecewiseLinearCurveBasicsController.AddEndButtonElementName,
                PiecewiseLinearCurveBasicsController.EvaluateFiveButtonElementName,
                PiecewiseLinearCurveBasicsController.EvaluateFifteenButtonElementName
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
                root.Q<Button>(PiecewiseLinearCurveBasicsController.AddStartButtonElementName),
                root.Q<Button>(PiecewiseLinearCurveBasicsController.AddPeakButtonElementName),
                root.Q<Button>(PiecewiseLinearCurveBasicsController.AddEndButtonElementName),
                root.Q<Button>(PiecewiseLinearCurveBasicsController.EvaluateFiveButtonElementName),
                root.Q<Button>(PiecewiseLinearCurveBasicsController.EvaluateFifteenButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Piecewise Linear Curve Test {width}x{height}" };
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
