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

namespace AudioControl.Samples.Tests.PlayMode
{
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class AudioControlBasicsButtonTests
    {
        private const string PanelSettingsGuid = "b2ea0a7d65c44ae8b88df3d7b44938b1";
        private const int WideWidth = 960;
        private const int WideHeight = 600;
        private const int NarrowWidth = 640;
        private const int NarrowHeight = 360;
        private const float Tolerance = 0.5f;
        private GameObject _host;
        private UIDocument _document;
        private AudioControlController _controller;
        private AudioControlBasicsController _sample;
        private PanelSettings _panel;
        private RenderTexture _target;

        [UnitySetUp]
        public IEnumerator CreateView()
        {
            _panel = LoadAndClonePanel();
            Assert.That(_panel.themeStyleSheet, Is.Not.Null);
            Assert.That(_panel.scaleMode, Is.EqualTo(PanelScaleMode.ConstantPixelSize));
            _target = CreateTarget(WideWidth, WideHeight);
            _panel.targetTexture = _target;
            _host = new GameObject("Audio Control Basics Tests");
            _host.SetActive(false);
            _host.AddComponent<AudioListener>();
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panel;
            _controller = _host.AddComponent<AudioControlController>();
            typeof(AudioControlController).GetField("_voiceLimit", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(_controller, 4);
            _sample = _host.AddComponent<AudioControlBasicsController>();
            _host.SetActive(true);

            yield return WaitUntil(
                () => Find<VisualElement>(AudioControlBasicsController.ReadyElementName) is { } ready && ready.worldBound.width > 0f,
                3d,
                "Audio Control Basicsの実panelが準備されませんでした。");
        }

        [UnityTearDown]
        public IEnumerator DestroyView()
        {
            var stopAll = Find<Button>(AudioControlBasicsController.StopAllButtonElementName);
            if (stopAll != null && stopAll.enabledSelf) InvokeClick(stopAll);
            if (_host != null) UnityEngine.Object.Destroy(_host);
            yield return null;
            if (_panel != null) UnityEngine.Object.DestroyImmediate(_panel);
            ReleaseTarget();
        }

        [UnityTest]
        public IEnumerator Buttons_PlayFillStealFadeAndStopOwnedVoices()
        {
            InvokeClick(Find<Button>(AudioControlBasicsController.PlayLoopButtonElementName));
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(1));
            Assert.That(_sample.OwnedVoiceCount, Is.EqualTo(1));

            InvokeClick(Find<Button>(AudioControlBasicsController.FillButtonElementName));
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(4));
            Assert.That(_sample.OwnedVoiceCount, Is.EqualTo(4));

            InvokeClick(Find<Button>(AudioControlBasicsController.PlayToneButtonElementName));
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(4), "高priority toneのsteal後も上限を維持しませんでした。");
            Assert.That(_sample.OwnedVoiceCount, Is.EqualTo(4), "steal済みhandleがsample一覧に残りました。");

            InvokeClick(Find<Button>(AudioControlBasicsController.StopOneButtonElementName));
            yield return new WaitForSecondsRealtime(0.28f);
            Assert.That(_controller.ActiveVoiceCount, Is.EqualTo(3));

            InvokeClick(Find<Button>(AudioControlBasicsController.StopAllButtonElementName));
            Assert.That(_controller.ActiveVoiceCount, Is.Zero);
            Assert.That(_sample.OwnedVoiceCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReadyView_UsesContainedWideAndNarrowGeometry()
        {
            yield return WaitForGeometry(WideWidth, WideHeight);
            AssertGeometry(true);

            ReplaceTarget(NarrowWidth, NarrowHeight);
            yield return WaitUntil(
                () => Find<Button>(AudioControlBasicsController.StopOneButtonElementName).worldBound.yMin >
                      Find<Button>(AudioControlBasicsController.PlayToneButtonElementName).worldBound.yMax,
                3d,
                "640x360で5 Buttonが3+2行へ折り返されませんでした。");
            yield return WaitForGeometry(NarrowWidth, NarrowHeight);
            AssertGeometry(false);
        }

        [UnityTest]
        public IEnumerator ReadyView_ExposesStableStatusAndControls()
        {
            Assert.That(Find<Label>(AudioControlBasicsController.StatusElementName).text, Does.Contain("0/4"));
            Assert.That(Find<Label>(AudioControlBasicsController.MeterElementName).text, Does.Contain("○○○○"));
            foreach (var button in GetButtons()) Assert.That(button, Is.Not.Null);
            yield break;
        }

        private void AssertGeometry(bool wide)
        {
            var card = Find<VisualElement>(AudioControlBasicsController.CardElementName);
            var elements = new VisualElement[]
            {
                Find<Label>(AudioControlBasicsController.TitleElementName),
                Find<Label>(AudioControlBasicsController.DescriptionElementName),
                Find<Label>(AudioControlBasicsController.StatusElementName),
                Find<Label>(AudioControlBasicsController.StageElementName),
                Find<Label>(AudioControlBasicsController.MeterElementName)
            }.Concat(GetButtons()).ToArray();
            Assert.That(card, Is.Not.Null);
            var safe = new Rect(card.worldBound.xMin + 6f, card.worldBound.yMin + 6f,
                card.worldBound.width - 12f, card.worldBound.height - 12f);
            for (var index = 0; index < elements.Length; index++)
            {
                var element = elements[index];
                Assert.That(element, Is.Not.Null);
                var bounds = element.worldBound;
                Assert.That(bounds.width, Is.GreaterThan(0f), element.name);
                Assert.That(bounds.height, Is.GreaterThan(0f), element.name);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - Tolerance), element.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + Tolerance), element.name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safe.yMin - Tolerance), element.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + Tolerance), element.name);
                for (var other = index + 1; other < elements.Length; other++)
                {
                    Assert.That(bounds.Overlaps(elements[other].worldBound), Is.False,
                        element.name + " / " + elements[other].name);
                }
            }

            var buttons = GetButtons();
            if (wide)
            {
                foreach (var button in buttons) Assert.That(button.worldBound.yMin, Is.EqualTo(buttons[0].worldBound.yMin).Within(Tolerance));
            }
            else
            {
                Assert.That(buttons[1].worldBound.yMin, Is.EqualTo(buttons[0].worldBound.yMin).Within(Tolerance));
                Assert.That(buttons[2].worldBound.yMin, Is.EqualTo(buttons[0].worldBound.yMin).Within(Tolerance));
                Assert.That(buttons[3].worldBound.yMin, Is.GreaterThan(buttons[0].worldBound.yMax));
                Assert.That(buttons[4].worldBound.yMin, Is.EqualTo(buttons[3].worldBound.yMin).Within(Tolerance));
            }
        }

        private T Find<T>(string name) where T : VisualElement => _document?.rootVisualElement?.Q<T>(name);

        private Button[] GetButtons() => new[]
        {
            Find<Button>(AudioControlBasicsController.PlayToneButtonElementName),
            Find<Button>(AudioControlBasicsController.PlayLoopButtonElementName),
            Find<Button>(AudioControlBasicsController.FillButtonElementName),
            Find<Button>(AudioControlBasicsController.StopOneButtonElementName),
            Find<Button>(AudioControlBasicsController.StopAllButtonElementName)
        };

        private static void InvokeClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True, button.name);
            var method = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(EventBase) }, null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(button.clickable, new object[] { null });
        }

        private IEnumerator WaitForGeometry(int width, int height)
        {
            yield return WaitUntil(
                () => Math.Abs(_document.rootVisualElement.contentRect.width - width) <= Tolerance &&
                      Math.Abs(_document.rootVisualElement.contentRect.height - height) <= Tolerance &&
                      Find<VisualElement>(AudioControlBasicsController.CardElementName).worldBound.width > 0f,
                3d,
                $"実panelが{width}x{height}へ解決されませんでした。");
        }

        private void ReplaceTarget(int width, int height)
        {
            ReleaseTarget();
            _target = CreateTarget(width, height);
            _panel.targetTexture = _target;
        }

        private static RenderTexture CreateTarget(int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = $"Audio Control Basics {width}x{height}"
            };
            Assert.That(target.Create(), Is.True);
            return target;
        }

        private void ReleaseTarget()
        {
            if (_panel != null) _panel.targetTexture = null;
            if (_target == null) return;
            _target.Release();
            UnityEngine.Object.DestroyImmediate(_target);
            _target = null;
        }

        private static PanelSettings LoadAndClonePanel()
        {
#if UNITY_EDITOR
            var path = AssetDatabase.GUIDToAssetPath(PanelSettingsGuid);
            Assert.That(path, Is.Not.Empty);
            var source = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            Assert.That(source, Is.Not.Null);
            return UnityEngine.Object.Instantiate(source);
#else
            Assert.Fail("Audio Control sample tests are Editor PlayMode tests.");
            return null;
#endif
        }

        private static IEnumerator WaitUntil(Func<bool> condition, double seconds, string message)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(message);
                yield return null;
            }
        }
    }
}
