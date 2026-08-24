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

namespace ReplayTape.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、record/replay、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class ReplayTapeBasicsButtonTests
    {
        private const string PanelSettingsGuid = "e3000000000000000000000000000010";
        private GameObject _host;
        private UIDocument _document;
        private ReplayTapeBasicsController _sample;
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
            _host = new GameObject("Replay Tape Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<ReplayTapeBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.Q<VisualElement>(ReplayTapeBasicsController.CardElementName)?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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

        /// <summary>空builderを繰り返しBuildしても同じ16-byte tapeを返す。</summary>
        [UnityTest]
        public IEnumerator BuildButton_EmptyTapeIsStable()
        {
            var initial = _sample.LastTape;
            Assert.That(initial.IsValid, Is.True);
            Assert.That(initial.EntryCount, Is.Zero);
            Assert.That(initial.ByteCount, Is.EqualTo(16));
            Click(ReplayTapeBasicsController.BuildButtonElementName);
            var first = _sample.LastTape;
            Click(ReplayTapeBasicsController.BuildButtonElementName);
            Assert.That(_sample.LastTape, Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(initial));
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(2));
            yield return null;
        }

        /// <summary>2 commandを記録し、Buildしたtapeから同じmodelを再現する。</summary>
        [UnityTest]
        public IEnumerator RecordBuildReplay_ReproducesModelAndTape()
        {
            Click(ReplayTapeBasicsController.RecordMoveButtonElementName);
            Assert.That(_sample.PositionX, Is.EqualTo(1));
            Assert.That(_sample.Health, Is.EqualTo(100));
            Assert.That(_sample.EntryCount, Is.EqualTo(1));
            Assert.That(_sample.NextTick, Is.EqualTo(20));
            Click(ReplayTapeBasicsController.RecordDamageButtonElementName);
            Assert.That(_sample.PositionX, Is.EqualTo(1));
            Assert.That(_sample.Health, Is.EqualTo(90));
            Assert.That(_sample.EntryCount, Is.EqualTo(2));
            Assert.That(_sample.NextTick, Is.EqualTo(30));
            Click(ReplayTapeBasicsController.BuildButtonElementName);
            var built = _sample.LastTape;
            var bytes = built.ToByteArray();
            Assert.That(built.EntryCount, Is.EqualTo(2));
            Assert.That(built.ByteCount, Is.EqualTo(56));
            Click(ReplayTapeBasicsController.ReplayButtonElementName);
            Assert.That(_sample.ReplayVerified, Is.True);
            Assert.That(_sample.PositionX, Is.EqualTo(1));
            Assert.That(_sample.Health, Is.EqualTo(90));
            Assert.That(_sample.LastTape.ToByteArray(), Is.EqualTo(bytes));
            Assert.That(_sample.ButtonActionCount, Is.EqualTo(4));
            yield return null;
        }

        /// <summary>Resetがmodel、builder、tick、操作数を初期状態へ戻す。</summary>
        [UnityTest]
        public IEnumerator ResetButton_RestoresInitialState()
        {
            Click(ReplayTapeBasicsController.RecordMoveButtonElementName);
            Click(ReplayTapeBasicsController.RecordDamageButtonElementName);
            Click(ReplayTapeBasicsController.ReplayButtonElementName);
            Click(ReplayTapeBasicsController.ResetButtonElementName);
            Assert.That(_sample.Health, Is.EqualTo(100));
            Assert.That(_sample.PositionX, Is.Zero);
            Assert.That(_sample.EntryCount, Is.Zero);
            Assert.That(_sample.TapeByteCount, Is.EqualTo(16));
            Assert.That(_sample.NextTick, Is.EqualTo(10));
            Assert.That(_sample.ButtonActionCount, Is.Zero);
            Assert.That(_sample.ReplayVerified, Is.False);
            Assert.That(_sample.LastTape.EntryCount, Is.Zero);
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
            var card = root.Q<VisualElement>(ReplayTapeBasicsController.CardElementName);
            var names = new[]
            {
                ReplayTapeBasicsController.TitleElementName,
                ReplayTapeBasicsController.DescriptionElementName,
                ReplayTapeBasicsController.ConfigurationElementName,
                ReplayTapeBasicsController.ModelElementName,
                ReplayTapeBasicsController.StageElementName,
                ReplayTapeBasicsController.TapeElementName,
                ReplayTapeBasicsController.RecordMoveButtonElementName,
                ReplayTapeBasicsController.RecordDamageButtonElementName,
                ReplayTapeBasicsController.BuildButtonElementName,
                ReplayTapeBasicsController.ReplayButtonElementName,
                ReplayTapeBasicsController.ResetButtonElementName
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
                root.Q<Button>(ReplayTapeBasicsController.RecordMoveButtonElementName),
                root.Q<Button>(ReplayTapeBasicsController.RecordDamageButtonElementName),
                root.Q<Button>(ReplayTapeBasicsController.BuildButtonElementName),
                root.Q<Button>(ReplayTapeBasicsController.ReplayButtonElementName),
                root.Q<Button>(ReplayTapeBasicsController.ResetButtonElementName)
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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Replay Tape Test {width}x{height}" };
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
