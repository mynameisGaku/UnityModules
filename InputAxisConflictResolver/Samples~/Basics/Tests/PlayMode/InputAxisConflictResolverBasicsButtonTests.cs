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

namespace InputAxisConflict.Samples.PlayMode.Tests
{
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputAxisConflictResolverBasicsButtonTests
    {
        private const string PanelSettingsGuid = "ee000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private InputAxisConflictResolverBasicsController _sample;
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
            _host = new GameObject("Input Axis Conflict Resolver Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputAxisConflictResolverBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(InputAxisConflictResolverBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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
        public IEnumerator InitialState_IsNeutralAtTick100()
        {
            Assert.That(_sample.CurrentTick, Is.EqualTo(100));
            Assert.That(_sample.ResolvedValue, Is.Zero);
            Assert.That(_sample.HasConflict, Is.False);
            Assert.That(_sample.LastError, Is.EqualTo(InputAxisConflictError.None));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NegativeThenPositive_UsesLastPressedEdge()
        {
            Click(InputAxisConflictResolverBasicsController.NegativeButtonElementName);
            Assert.That(_sample.ResolvedValue, Is.EqualTo(-1));
            Click(InputAxisConflictResolverBasicsController.PositiveButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(101));
            Assert.That(_sample.HasConflict, Is.True);
            Assert.That(_sample.ResolvedValue, Is.EqualTo(1));
            Assert.That(_sample.PositivePressedThisSample, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleasePositive_FallsBackToNegative()
        {
            Click(InputAxisConflictResolverBasicsController.NegativeButtonElementName);
            Click(InputAxisConflictResolverBasicsController.PositiveButtonElementName);
            Click(InputAxisConflictResolverBasicsController.ReleasePositiveButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(102));
            Assert.That(_sample.NegativePressed, Is.True);
            Assert.That(_sample.PositivePressed, Is.False);
            Assert.That(_sample.PositiveReleasedThisSample, Is.True);
            Assert.That(_sample.ResolvedValue, Is.EqualTo(-1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiveButtons_EndInSameTickNeutralTie()
        {
            Click(InputAxisConflictResolverBasicsController.NegativeButtonElementName);
            Click(InputAxisConflictResolverBasicsController.PositiveButtonElementName);
            Click(InputAxisConflictResolverBasicsController.ReleasePositiveButtonElementName);
            Click(InputAxisConflictResolverBasicsController.ReleaseAllButtonElementName);
            Click(InputAxisConflictResolverBasicsController.SimultaneousButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(104));
            Assert.That(_sample.NegativePressed, Is.True);
            Assert.That(_sample.PositivePressed, Is.True);
            Assert.That(_sample.HasConflict, Is.True);
            Assert.That(_sample.ResolvedValue, Is.Zero);
            Assert.That(_sample.NegativePressedThisSample, Is.True);
            Assert.That(_sample.PositivePressedThisSample, Is.True);
            Assert.That(_sample.ResolutionChanged, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(5));
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

        private IEnumerator AssertGeometry(bool wide)
        {
            var root = ReadyRoot();
            var card = root.Q<VisualElement>(InputAxisConflictResolverBasicsController.CardElementName);
            var names = new[]
            {
                InputAxisConflictResolverBasicsController.TitleElementName,
                InputAxisConflictResolverBasicsController.DescriptionElementName,
                InputAxisConflictResolverBasicsController.ConfigurationElementName,
                InputAxisConflictResolverBasicsController.InputElementName,
                InputAxisConflictResolverBasicsController.StageElementName,
                InputAxisConflictResolverBasicsController.ResultElementName,
                InputAxisConflictResolverBasicsController.NegativeButtonElementName,
                InputAxisConflictResolverBasicsController.PositiveButtonElementName,
                InputAxisConflictResolverBasicsController.ReleasePositiveButtonElementName,
                InputAxisConflictResolverBasicsController.ReleaseAllButtonElementName,
                InputAxisConflictResolverBasicsController.SimultaneousButtonElementName
            };
            var elements = names.Select(name => root.Q<VisualElement>(name)).ToArray();
            Assert.That(elements.All(element => element != null), Is.True);
            var safe = new Rect(card.worldBound.xMin + 5f, card.worldBound.yMin + 5f, card.worldBound.width - 10f, card.worldBound.height - 10f);
            foreach (var element in elements)
            {
                var bounds = element.worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f));
                Assert.That(bounds.height, Is.GreaterThan(0f));
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 0.5f));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + 0.5f));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 0.5f));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + 0.5f));
            }
            for (var first = 0; first < elements.Length; first++)
            for (var second = first + 1; second < elements.Length; second++)
                Assert.That(elements[first].worldBound.Overlaps(elements[second].worldBound), Is.False, $"overlap: {elements[first].name}/{elements[second].name}");
            var buttons = new[]
            {
                root.Q<Button>(InputAxisConflictResolverBasicsController.NegativeButtonElementName),
                root.Q<Button>(InputAxisConflictResolverBasicsController.PositiveButtonElementName),
                root.Q<Button>(InputAxisConflictResolverBasicsController.ReleasePositiveButtonElementName),
                root.Q<Button>(InputAxisConflictResolverBasicsController.ReleaseAllButtonElementName),
                root.Q<Button>(InputAxisConflictResolverBasicsController.SimultaneousButtonElementName)
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
            Assert.That(button, Is.Not.Null);
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            return target;
        }

        private static PanelSettings LoadShippedPanelSettings()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(PanelSettingsGuid));
#else
            Assert.Fail("このfixtureはUnity Editorで実行してください。");
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
    }
}
