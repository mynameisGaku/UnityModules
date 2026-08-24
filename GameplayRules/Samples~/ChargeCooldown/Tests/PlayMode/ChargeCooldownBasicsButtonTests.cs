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

namespace GameplayTiming.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、charge回復、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class ChargeCooldownBasicsButtonTests
    {
        private const string PanelSettingsGuid = "fc230000000000000000000000000011";
        private GameObject _host;
        private UIDocument _document;
        private ChargeCooldownBasicsController _sample;
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
            _host = new GameObject("Charge Cooldown Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<ChargeCooldownBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(ChargeCooldownBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            if (_targetTexture != null) { _targetTexture.Release(); UnityEngine.Object.Destroy(_targetTexture); }
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialState_IsFullAndCanonical()
        {
            Assert.That(_sample.LastSucceeded, Is.True);
            Assert.That(_sample.LastError, Is.EqualTo(ChargeCooldownError.None));
            Assert.That(_sample.Rules.MaximumCharges, Is.EqualTo(3));
            Assert.That(_sample.Rules.RechargeIntervalTicks, Is.EqualTo(10));
            AssertState(3, 100, 0);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpendButton_DrainsChargesAndReportsEmptyAttempt()
        {
            Click(ChargeCooldownBasicsController.SpendButtonElementName);
            Assert.That(_sample.LastResult.ChargeSpent, Is.True);
            AssertState(2, 100, 110);
            Click(ChargeCooldownBasicsController.SpendButtonElementName);
            Click(ChargeCooldownBasicsController.SpendButtonElementName);
            AssertState(0, 100, 110);
            Click(ChargeCooldownBasicsController.SpendButtonElementName);
            Assert.That(_sample.LastResult.ChargeSpent, Is.False);
            Assert.That(_sample.LastResult.IsReady, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AdvanceButtons_RespectBoundaryAndCatchUp()
        {
            Click(ChargeCooldownBasicsController.SpendButtonElementName);
            Click(ChargeCooldownBasicsController.SpendButtonElementName);
            Click(ChargeCooldownBasicsController.SpendButtonElementName);
            Click(ChargeCooldownBasicsController.AdvanceNineButtonElementName);
            Assert.That(_sample.LastResult.ChargesRestored, Is.Zero);
            AssertState(0, 109, 110);
            Click(ChargeCooldownBasicsController.AdvanceOneButtonElementName);
            Assert.That(_sample.LastResult.ChargesRestored, Is.EqualTo(1));
            AssertState(1, 110, 120);
            Click(ChargeCooldownBasicsController.AdvanceTwentyFiveButtonElementName);
            Assert.That(_sample.LastResult.ChargesRestored, Is.EqualTo(2));
            AssertState(3, 135, 0);
            Click(ChargeCooldownBasicsController.ResetButtonElementName);
            AssertState(3, 100, 0);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(7));
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

        private void AssertState(int charges, long last, long next)
        {
            Assert.That(_sample.State.AvailableCharges, Is.EqualTo(charges));
            Assert.That(_sample.State.LastEvaluatedTick, Is.EqualTo(last));
            Assert.That(_sample.State.NextRechargeTick, Is.EqualTo(next));
        }

        private IEnumerator AssertGeometry(bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(ChargeCooldownBasicsController.CardElementName);
            var names = new[]
            {
                ChargeCooldownBasicsController.TitleElementName, ChargeCooldownBasicsController.DescriptionElementName,
                ChargeCooldownBasicsController.ConfigurationElementName, ChargeCooldownBasicsController.InputElementName,
                ChargeCooldownBasicsController.StageElementName, ChargeCooldownBasicsController.ResultElementName,
                ChargeCooldownBasicsController.ResetButtonElementName, ChargeCooldownBasicsController.SpendButtonElementName,
                ChargeCooldownBasicsController.AdvanceNineButtonElementName, ChargeCooldownBasicsController.AdvanceOneButtonElementName,
                ChargeCooldownBasicsController.AdvanceTwentyFiveButtonElementName
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
            var buttons = names.Skip(6).Select(name => root.Q<Button>(name)).ToArray();
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
            Assert.That(button, Is.Not.Null, name);
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Charge Cooldown Test {width}x{height}" };
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
            while (!predicate()) { if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(failure); yield return null; }
        }
        private static string Describe(VisualElement element, Rect bounds, Rect safe) => $"{element.name} text='{(element as TextElement)?.text}' bounds={bounds} safe={safe}";
    }
}
