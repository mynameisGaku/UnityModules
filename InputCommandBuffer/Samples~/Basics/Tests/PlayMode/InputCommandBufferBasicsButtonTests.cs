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

namespace InputBuffering.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、期限内FIFO契約、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class InputCommandBufferBasicsButtonTests
    {
        private const string PanelSettingsGuid = "d9000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private InputCommandBufferBasicsController _sample;
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
            _host = new GameObject("Input Command Buffer Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<InputCommandBufferBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(InputCommandBufferBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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

        /// <summary>初期tick、空buffer、操作数が設定どおりで安定する。</summary>
        [UnityTest]
        public IEnumerator InitialState_IsEmptyAtTick100()
        {
            Assert.That(_sample.CurrentTick, Is.EqualTo(100));
            Assert.That(_sample.BufferedCount, Is.Zero);
            Assert.That(_sample.LastCommandId, Is.Zero);
            Assert.That(_sample.LastError, Is.EqualTo(InputCommandBufferError.None));
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            yield return null;
        }

        /// <summary>tick100で記録したJumpがtick101まで保持されFIFOで消費される。</summary>
        [UnityTest]
        public IEnumerator GoldenButtons_BufferAdvanceAndConsumeJump()
        {
            Click(InputCommandBufferBasicsController.BufferJumpButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(100));
            Assert.That(_sample.BufferedCount, Is.EqualTo(1));
            Assert.That(_sample.LastCommandId, Is.EqualTo(1));
            Assert.That(_sample.LastRecordedTick, Is.EqualTo(100));
            Click(InputCommandBufferBasicsController.AdvanceOneButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(101));
            Assert.That(_sample.BufferedCount, Is.EqualTo(1));
            Assert.That(_sample.LastExpiredCount, Is.Zero);
            Click(InputCommandBufferBasicsController.ConsumeJumpButtonElementName);
            Assert.That(_sample.BufferedCount, Is.Zero);
            Assert.That(_sample.LastConsumed, Is.True);
            Assert.That(_sample.LastCommandId, Is.EqualTo(1));
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(3));
            yield return null;
        }

        /// <summary>tick101で記録したDashがretention +2を超えたtick104で期限切れになる。</summary>
        [UnityTest]
        public IEnumerator Dash_ExpiresOutsideRetentionWindow()
        {
            Click(InputCommandBufferBasicsController.BufferJumpButtonElementName);
            Click(InputCommandBufferBasicsController.AdvanceOneButtonElementName);
            Click(InputCommandBufferBasicsController.ConsumeJumpButtonElementName);
            Click(InputCommandBufferBasicsController.BufferDashButtonElementName);
            Assert.That(_sample.BufferedCount, Is.EqualTo(1));
            Assert.That(_sample.LastCommandId, Is.EqualTo(2));
            Click(InputCommandBufferBasicsController.ExpireButtonElementName);
            Assert.That(_sample.CurrentTick, Is.EqualTo(104));
            Assert.That(_sample.BufferedCount, Is.Zero);
            Assert.That(_sample.LastExpiredCount, Is.EqualTo(1));
            Assert.That(_sample.LastConsumed, Is.False);
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(5));
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
            var card = root.Q<VisualElement>(InputCommandBufferBasicsController.CardElementName);
            var names = new[]
            {
                InputCommandBufferBasicsController.TitleElementName,
                InputCommandBufferBasicsController.DescriptionElementName,
                InputCommandBufferBasicsController.ConfigurationElementName,
                InputCommandBufferBasicsController.InputElementName,
                InputCommandBufferBasicsController.StageElementName,
                InputCommandBufferBasicsController.ResultElementName,
                InputCommandBufferBasicsController.BufferJumpButtonElementName,
                InputCommandBufferBasicsController.AdvanceOneButtonElementName,
                InputCommandBufferBasicsController.ConsumeJumpButtonElementName,
                InputCommandBufferBasicsController.BufferDashButtonElementName,
                InputCommandBufferBasicsController.ExpireButtonElementName
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
                root.Q<Button>(InputCommandBufferBasicsController.BufferJumpButtonElementName),
                root.Q<Button>(InputCommandBufferBasicsController.AdvanceOneButtonElementName),
                root.Q<Button>(InputCommandBufferBasicsController.ConsumeJumpButtonElementName),
                root.Q<Button>(InputCommandBufferBasicsController.BufferDashButtonElementName),
                root.Q<Button>(InputCommandBufferBasicsController.ExpireButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Input Command Buffer Test {width}x{height}" };
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
