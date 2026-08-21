using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AdaptiveLayout.Samples.Tests.PlayMode
{
    [Parallelizable(ParallelScope.None)]
    public sealed class AdaptiveLayoutBasicsTests
    {
        private GameObject _host;
        private PanelSettings _panelSettings;
        private ThemeStyleSheet _themeStyleSheet;
        private RenderTexture _targetTexture;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }

            if (_targetTexture != null)
            {
                _targetTexture.Release();
                Object.DestroyImmediate(_targetTexture);
            }

            if (_panelSettings != null)
            {
                Object.DestroyImmediate(_panelSettings);
            }

            if (_themeStyleSheet != null)
            {
                Object.DestroyImmediate(_themeStyleSheet);
            }
        }

        [UnityTest]
        public IEnumerator Basics_WideViewport_BuildsEveryStableElementInsideSafeContent()
        {
            yield return VerifyViewport(960, 600);
        }

        [UnityTest]
        public IEnumerator Basics_NarrowViewport_KeepsEveryStableElementInsideSafeContent()
        {
            yield return VerifyViewport(640, 360);
        }

        private IEnumerator VerifyViewport(int width, int height)
        {
            CreateHost(width, height, out var document);
            var root = document.rootVisualElement;
            root.style.width = width;
            root.style.height = height;

            for (var frame = 0; frame < 12; frame++)
            {
                yield return null;
            }

            var safeContent = root.Q<VisualElement>(AdaptiveLayoutBasicsController.SafeContentElementName);
            var card = root.Q<VisualElement>(AdaptiveLayoutBasicsController.CardElementName);
            var status = root.Q<Label>(AdaptiveLayoutBasicsController.StatusElementName);
            var screen = root.Q<Label>(AdaptiveLayoutBasicsController.ScreenElementName);
            var safeArea = root.Q<Label>(AdaptiveLayoutBasicsController.SafeAreaElementName);
            var insets = root.Q<Label>(AdaptiveLayoutBasicsController.InsetsElementName);

            Assert.That(safeContent, Is.Not.Null);
            Assert.That(card, Is.Not.Null);
            Assert.That(status, Is.Not.Null);
            Assert.That(screen, Is.Not.Null);
            Assert.That(safeArea, Is.Not.Null);
            Assert.That(insets, Is.Not.Null);
            AssertContainedAndPositive(root.worldBound, safeContent.worldBound, "safe-content");
            AssertContainedAndPositive(safeContent.worldBound, card.worldBound, "card");
            AssertContainedAndPositive(card.worldBound, status.worldBound, "status");
            AssertContainedAndPositive(card.worldBound, screen.worldBound, "screen");
            AssertContainedAndPositive(card.worldBound, safeArea.worldBound, "safe-area");
            AssertContainedAndPositive(card.worldBound, insets.worldBound, "insets");
            Assert.That(status.worldBound.yMax, Is.LessThanOrEqualTo(screen.worldBound.yMin + 0.5f));
            Assert.That(screen.worldBound.yMax, Is.LessThanOrEqualTo(safeArea.worldBound.yMin + 0.5f));
            Assert.That(safeArea.worldBound.yMax, Is.LessThanOrEqualTo(insets.worldBound.yMin + 0.5f));
        }

        private void CreateHost(int width, int height, out UIDocument document)
        {
            _targetTexture = new RenderTexture(width, height, 0);
            _targetTexture.Create();
            _themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.themeStyleSheet = _themeStyleSheet;
            _panelSettings.targetTexture = _targetTexture;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;

            _host = new GameObject("AdaptiveLayoutBasicsTest");
            _host.SetActive(false);
            document = _host.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            _host.AddComponent<SafeAreaVisualElement>();
            _host.AddComponent<AdaptiveLayoutBasicsController>();
            _host.SetActive(true);
        }

        private static void AssertContainedAndPositive(Rect parent, Rect child, string label)
        {
            Assert.That(child.width, Is.GreaterThan(0f), $"{label} width: {child}");
            Assert.That(child.height, Is.GreaterThan(0f), $"{label} height: {child}");
            Assert.That(child.xMin, Is.GreaterThanOrEqualTo(parent.xMin - 0.5f), $"{label} left: child={child}, parent={parent}");
            Assert.That(child.yMin, Is.GreaterThanOrEqualTo(parent.yMin - 0.5f), $"{label} top: child={child}, parent={parent}");
            Assert.That(child.xMax, Is.LessThanOrEqualTo(parent.xMax + 0.5f), $"{label} right: child={child}, parent={parent}");
            Assert.That(child.yMax, Is.LessThanOrEqualTo(parent.yMax + 0.5f), $"{label} bottom: child={child}, parent={parent}");
        }
    }
}
