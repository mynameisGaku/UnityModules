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

namespace StartupFlow.Samples.PlayMode.Tests
{
    /// <summary>import済みBasicsの実Button、flow結果、responsive geometryを検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class StartupFlowBasicsButtonTests
    {
        private const string PanelSettingsGuid = "c0600000000000000000000000000006";
        private GameObject _host;
        private UIDocument _document;
        private StartupFlowBasicsController _sample;
        private PanelSettings _panelSettings;
        private RenderTexture _targetTexture;

        /// <summary>shipped PanelSettingsをcloneし、実RenderTexture panel上へsampleを構築する。</summary>
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

            _host = new GameObject("Startup Flow Basics Test Host");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<StartupFlowBasicsController>();
            _host.SetActive(true);
            yield return WaitUntil(() => ReadyRoot()?.worldBound.width > 0f, "960x600の実panelとsampleが準備されませんでした。");
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

        /// <summary>Success Buttonが3 stepを規定順で完了し、Resetが表示状態を戻す。</summary>
        [UnityTest]
        public IEnumerator SuccessAndReset_UseBoundButtons()
        {
            Click(StartupFlowBasicsController.RunSuccessButtonElementName);
            AssertRunButtons(false);
            yield return WaitUntil(() => _sample.HasResult, "Success flowが完了しませんでした。");
            Assert.That(_sample.LastResult.IsSuccess, Is.True);
            Assert.That(_sample.ExecutionTrace, Is.EqualTo(new[] { "10-check-config", "20-warm-cache", "30-ready-gameplay" }));
            AssertRunButtons(true);

            Click(StartupFlowBasicsController.ResetButtonElementName);
            Assert.That(_sample.HasResult, Is.False);
            Assert.That(_sample.ExecutionTrace, Is.Empty);
            Assert.That(_sample.StageText, Does.StartWith("Ready"));
        }

        /// <summary>Failure Buttonが2番目で停止し、3番目を実行しない。</summary>
        [UnityTest]
        public IEnumerator Failure_StopsAtSecondStep()
        {
            Click(StartupFlowBasicsController.RunFailureButtonElementName);
            yield return WaitUntil(() => _sample.HasResult, "Failure flowが完了しませんでした。");
            Assert.That(_sample.LastResult.Error, Is.EqualTo(StartupFlowError.StepFailed));
            Assert.That(_sample.LastResult.FailedStepId, Is.EqualTo("20-load-profile"));
            Assert.That(_sample.ExecutionTrace, Is.EqualTo(new[] { "10-check-config", "20-load-profile" }));
            Assert.That(_sample.ExecutionTrace, Does.Not.Contain("30-ready-gameplay"));
        }

        /// <summary>Slow ButtonとCancel Buttonが協調cancelを明示結果へ収束させる。</summary>
        [UnityTest]
        public IEnumerator SlowAndCancel_ReturnCanceled()
        {
            Click(StartupFlowBasicsController.RunSlowButtonElementName);
            yield return null;
            Assert.That(_sample.IsRunning, Is.True);
            Click(StartupFlowBasicsController.CancelButtonElementName);
            yield return WaitUntil(() => _sample.HasResult, "cancelしたflowが完了しませんでした。");
            Assert.That(_sample.LastResult.Error, Is.EqualTo(StartupFlowError.Canceled));
            Assert.That(_sample.LastResult.FailedStepId, Is.EqualTo("10-long-warmup"));
            Assert.That(_sample.ExecutionTrace, Is.EqualTo(new[] { "10-long-warmup" }));
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
            var card = root.Q<VisualElement>(StartupFlowBasicsController.CardElementName);
            var names = new[]
            {
                StartupFlowBasicsController.TitleElementName,
                StartupFlowBasicsController.DescriptionElementName,
                StartupFlowBasicsController.StatusElementName,
                StartupFlowBasicsController.StageElementName,
                StartupFlowBasicsController.ProgressTrackElementName,
                StartupFlowBasicsController.TraceElementName,
                StartupFlowBasicsController.RunSuccessButtonElementName,
                StartupFlowBasicsController.RunFailureButtonElementName,
                StartupFlowBasicsController.RunSlowButtonElementName,
                StartupFlowBasicsController.CancelButtonElementName,
                StartupFlowBasicsController.ResetButtonElementName
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
            {
                Assert.That(elements[first].worldBound.Overlaps(elements[second].worldBound), Is.False, $"overlap: {elements[first].name} / {elements[second].name}");
            }

            var buttons = new[]
            {
                root.Q<Button>(StartupFlowBasicsController.RunSuccessButtonElementName),
                root.Q<Button>(StartupFlowBasicsController.RunFailureButtonElementName),
                root.Q<Button>(StartupFlowBasicsController.RunSlowButtonElementName),
                root.Q<Button>(StartupFlowBasicsController.CancelButtonElementName),
                root.Q<Button>(StartupFlowBasicsController.ResetButtonElementName)
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

        private void AssertRunButtons(bool enabled)
        {
            var root = ReadyRoot();
            Assert.That(root.Q<Button>(StartupFlowBasicsController.RunSuccessButtonElementName).enabledSelf, Is.EqualTo(enabled));
            Assert.That(root.Q<Button>(StartupFlowBasicsController.RunFailureButtonElementName).enabledSelf, Is.EqualTo(enabled));
            Assert.That(root.Q<Button>(StartupFlowBasicsController.RunSlowButtonElementName).enabledSelf, Is.EqualTo(enabled));
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

        private VisualElement ReadyRoot() => _document?.rootVisualElement?.Q<VisualElement>(StartupFlowBasicsController.ReadyElementName);

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
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = $"Startup Flow Test {width}x{height}" };
            target.Create();
            return target;
        }

        private static PanelSettings LoadShippedPanelSettings()
        {
#if UNITY_EDITOR
            var path = AssetDatabase.GUIDToAssetPath(PanelSettingsGuid);
            Assert.That(path, Is.Not.Empty, "shipped PanelSettings GUIDを解決できません。");
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
