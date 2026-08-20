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

namespace InputArbitration.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、仲裁結果、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputCommandArbiterBasicsButtonTests
    {
        private const string PanelSettingsGuid = "f8000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private InputCommandArbiterBasicsController _sample;
        private PanelSettings _panelSettings;
        private RenderTexture _targetTexture;

        /// <summary>配布PanelSettingsをcloneし、実RenderTexture panel上へsampleを構築する。</summary>
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
            _host = new GameObject("Input Command Arbiter Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputCommandArbiterBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(InputCommandArbiterBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
            yield return null;
        }

        /// <summary>sampleとRenderTextureを必ず破棄する。</summary>
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

        /// <summary>初期状態が成功・未選択・操作なしで安定する。</summary>
        [UnityTest]
        public IEnumerator InitialState_IsSuccessfulWithoutSelection()
        {
            AssertResult(true, false, -1, 0, 0, 0, InputCommandArbitrationError.None);
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        /// <summary>実Button列が未選択、単独選択、高priority、先頭tie-breakへ到達する。</summary>
        [UnityTest]
        public IEnumerator GoldenButtons_ProduceDeterministicSelections()
        {
            Click(InputCommandArbiterBasicsController.NoneButtonElementName);
            AssertResult(true, false, -1, 0, 0, 0, InputCommandArbitrationError.None);
            Click(InputCommandArbiterBasicsController.AttackButtonElementName);
            AssertResult(true, true, 0, 10, 100, 1, InputCommandArbitrationError.None);
            Click(InputCommandArbiterBasicsController.InteractButtonElementName);
            AssertResult(true, true, 1, 20, 200, 2, InputCommandArbitrationError.None);
            Click(InputCommandArbiterBasicsController.TieButtonElementName);
            AssertResult(true, true, 0, 10, 300, 2, InputCommandArbitrationError.None);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        /// <summary>重複command idがeligible状態に関係なく選択前に拒否される。</summary>
        [UnityTest]
        public IEnumerator DuplicateButton_ReturnsExplicitErrorWithoutSelection()
        {
            Click(InputCommandArbiterBasicsController.RejectDuplicateButtonElementName);
            AssertResult(false, false, -1, 0, 0, 0, InputCommandArbitrationError.DuplicateCommandId);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(1));
            yield return null;
        }

        /// <summary>実PanelSettingsでwide 1列とnarrow 3+2列がcard内に収まる。</summary>
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
            var card = root.Q<VisualElement>(InputCommandArbiterBasicsController.CardElementName);
            var names = new[]
            {
                InputCommandArbiterBasicsController.TitleElementName,
                InputCommandArbiterBasicsController.DescriptionElementName,
                InputCommandArbiterBasicsController.RuleElementName,
                InputCommandArbiterBasicsController.InputElementName,
                InputCommandArbiterBasicsController.StageElementName,
                InputCommandArbiterBasicsController.ResultElementName,
                InputCommandArbiterBasicsController.NoneButtonElementName,
                InputCommandArbiterBasicsController.AttackButtonElementName,
                InputCommandArbiterBasicsController.InteractButtonElementName,
                InputCommandArbiterBasicsController.TieButtonElementName,
                InputCommandArbiterBasicsController.RejectDuplicateButtonElementName
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
                root.Q<Button>(InputCommandArbiterBasicsController.NoneButtonElementName),
                root.Q<Button>(InputCommandArbiterBasicsController.AttackButtonElementName),
                root.Q<Button>(InputCommandArbiterBasicsController.InteractButtonElementName),
                root.Q<Button>(InputCommandArbiterBasicsController.TieButtonElementName),
                root.Q<Button>(InputCommandArbiterBasicsController.RejectDuplicateButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Input Command Arbiter Test {width}x{height}" };
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

        private void AssertResult(bool succeeded, bool hasSelection, int index, int commandId, int priority, int eligibleCount, InputCommandArbitrationError error)
        {
            var result = _sample.LastResult;
            Assert.That(result.Succeeded, Is.EqualTo(succeeded));
            Assert.That(result.HasSelection, Is.EqualTo(hasSelection));
            Assert.That(result.SelectedIndex, Is.EqualTo(index));
            Assert.That(result.CommandId, Is.EqualTo(commandId));
            Assert.That(result.Priority, Is.EqualTo(priority));
            Assert.That(result.EligibleCandidateCount, Is.EqualTo(eligibleCount));
            Assert.That(result.Error, Is.EqualTo(error));
        }
    }
}
