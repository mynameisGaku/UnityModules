using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AdaptiveLayout.Tests.PlayMode
{
    [Parallelizable(ParallelScope.None)]
    public sealed class SafeAreaComponentTests
    {
        private GameObject _root;
        private PanelSettings _panelSettings;
        private ThemeStyleSheet _themeStyleSheet;
        private RenderTexture _targetTexture;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
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

        [Test]
        public void RectTransform_AllEdges_AppliesNormalizedSafeArea()
        {
            var component = CreateRectTransformComponent(out var target);
            component.Source = new FakeSafeAreaSource(1000, 500, new Rect(100f, 50f, 800f, 400f));

            _root.SetActive(true);

            Assert.That(component.Refresh(), Is.True);
            Assert.That(target.anchorMin, Is.EqualTo(new Vector2(0.1f, 0.1f)));
            Assert.That(target.anchorMax, Is.EqualTo(new Vector2(0.9f, 0.9f)));
            Assert.That(target.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(target.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RectTransform_SelectedEdges_AppliesOnlySelectedEdges()
        {
            var component = CreateRectTransformComponent(out var target);
            component.Edges = SafeAreaEdges.Left | SafeAreaEdges.Top;
            component.Source = new FakeSafeAreaSource(1000, 500, new Rect(100f, 50f, 800f, 400f));

            _root.SetActive(true);

            Assert.That(component.Refresh(), Is.True);
            Assert.That(target.anchorMin, Is.EqualTo(new Vector2(0.1f, 0f)));
            Assert.That(target.anchorMax, Is.EqualTo(new Vector2(1f, 0.9f)));
        }

        [Test]
        public void RectTransform_Disable_RestoresOriginalLayout()
        {
            var component = CreateRectTransformComponent(out var target);
            var originalMin = target.anchorMin;
            var originalMax = target.anchorMax;
            var originalOffsetMin = target.offsetMin;
            var originalOffsetMax = target.offsetMax;
            component.Source = new FakeSafeAreaSource(1000, 500, new Rect(100f, 50f, 800f, 400f));

            _root.SetActive(true);
            component.enabled = false;

            Assert.That(target.anchorMin, Is.EqualTo(originalMin));
            Assert.That(target.anchorMax, Is.EqualTo(originalMax));
            Assert.That(target.offsetMin, Is.EqualTo(originalOffsetMin));
            Assert.That(target.offsetMax, Is.EqualTo(originalOffsetMax));
        }

        [UnityTest]
        public IEnumerator RectTransform_SourceChanges_UpdatesOnLateUpdate()
        {
            var source = new FakeSafeAreaSource(1000, 500, new Rect(100f, 50f, 800f, 400f));
            var component = CreateRectTransformComponent(out var target);
            component.Source = source;
            _root.SetActive(true);

            yield return null;
            source.Set(1000, 500, new Rect(200f, 0f, 800f, 500f));
            yield return null;

            Assert.That(target.anchorMin, Is.EqualTo(new Vector2(0.2f, 0f)));
            Assert.That(target.anchorMax, Is.EqualTo(Vector2.one));
        }

        [UnityTest]
        public IEnumerator VisualElement_AllEdges_UsesPanelCoordinatesAndRestoresInlineStyle()
        {
            _root = new GameObject("SafeAreaDocument");
            _root.SetActive(false);
            _targetTexture = new RenderTexture(1000, 500, 0);
            _targetTexture.Create();
            _themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.themeStyleSheet = _themeStyleSheet;
            _panelSettings.targetTexture = _targetTexture;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;

            var document = _root.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            var component = _root.AddComponent<SafeAreaVisualElement>();
            component.TargetElementName = "safe-content";
            component.Source = new FakeSafeAreaSource(1000, 500, new Rect(100f, 50f, 800f, 400f));
            _root.SetActive(true);

            document.rootVisualElement.style.width = 1000f;
            document.rootVisualElement.style.height = 500f;
            var target = new VisualElement { name = "safe-content" };
            document.rootVisualElement.Add(target);
            var originalPosition = target.style.position;
            var applied = false;
            for (var frame = 0; frame < 12 && !applied; frame++)
            {
                yield return null;
                applied = component.Refresh();
            }

            var rootBounds = document.rootVisualElement.worldBound;
            var parentBounds = target.parent?.worldBound ?? default;
            var panel = target.panel;
            var convertedTopLeft = panel == null
                ? Vector2.negativeInfinity
                : RuntimePanelUtils.ScreenToPanel(panel, new Vector2(100f, 50f));
            var convertedBottomRight = panel == null
                ? Vector2.positiveInfinity
                : RuntimePanelUtils.ScreenToPanel(panel, new Vector2(900f, 450f));
            Assert.That(applied, Is.True,
                $"Root={rootBounds}; Parent={parentBounds}; TopLeft={convertedTopLeft}; BottomRight={convertedBottomRight}; Panel={(panel != null)}");
            yield return null;
            Assert.That(target.worldBound.xMin, Is.EqualTo(100f).Within(0.5f));
            Assert.That(target.worldBound.yMin, Is.EqualTo(50f).Within(0.5f));
            Assert.That(target.worldBound.width, Is.EqualTo(800f).Within(0.5f));
            Assert.That(target.worldBound.height, Is.EqualTo(400f).Within(0.5f));

            component.enabled = false;

            Assert.That(target.style.position, Is.EqualTo(originalPosition));
        }

        [UnityTest]
        public IEnumerator VisualElement_SelectedEdge_IgnoresUnsafeUnselectedSide()
        {
            _root = new GameObject("SafeAreaSelectedEdgeDocument");
            _root.SetActive(false);
            _targetTexture = new RenderTexture(1000, 500, 0);
            _targetTexture.Create();
            _themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.themeStyleSheet = _themeStyleSheet;
            _panelSettings.targetTexture = _targetTexture;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;

            var document = _root.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            var component = _root.AddComponent<SafeAreaVisualElement>();
            component.TargetElementName = "selected-edge-content";
            component.Edges = SafeAreaEdges.Left;
            component.Source = new FakeSafeAreaSource(1000, 500, new Rect(100f, 0f, 800f, 500f));
            _root.SetActive(true);

            var root = document.rootVisualElement;
            root.style.width = 1000f;
            root.style.height = 500f;
            var parent = new VisualElement();
            parent.style.position = Position.Absolute;
            parent.style.left = 950f;
            parent.style.top = 0f;
            parent.style.width = 50f;
            parent.style.height = 500f;
            var target = new VisualElement { name = "selected-edge-content" };
            parent.Add(target);
            root.Add(parent);

            var applied = false;
            for (var frame = 0; frame < 12 && !applied; frame++)
            {
                yield return null;
                applied = component.Refresh();
            }

            Assert.That(applied, Is.True);
            yield return null;
            Assert.That(target.worldBound.xMin, Is.EqualTo(parent.worldBound.xMin).Within(0.5f));
            Assert.That(target.worldBound.xMax, Is.EqualTo(parent.worldBound.xMax).Within(0.5f));
            Assert.That(target.worldBound.height, Is.EqualTo(parent.worldBound.height).Within(0.5f));
        }

        private SafeAreaRectTransform CreateRectTransformComponent(out RectTransform target)
        {
            _root = new GameObject("SafeAreaTestRoot", typeof(RectTransform));
            _root.SetActive(false);
            var targetObject = new GameObject("SafeAreaTarget", typeof(RectTransform));
            targetObject.transform.SetParent(_root.transform, false);
            target = targetObject.GetComponent<RectTransform>();
            target.anchorMin = new Vector2(0.2f, 0.25f);
            target.anchorMax = new Vector2(0.8f, 0.75f);
            target.offsetMin = new Vector2(4f, 6f);
            target.offsetMax = new Vector2(-8f, -10f);
            return targetObject.AddComponent<SafeAreaRectTransform>();
        }

        private sealed class FakeSafeAreaSource : ISafeAreaSource
        {
            private SafeAreaSnapshot _snapshot;

            internal FakeSafeAreaSource(int width, int height, Rect safeArea)
            {
                Set(width, height, safeArea);
            }

            public bool TryGetSnapshot(out SafeAreaSnapshot snapshot)
            {
                snapshot = _snapshot;
                return true;
            }

            internal void Set(int width, int height, Rect safeArea)
            {
                Assert.That(SafeAreaMath.TryCreateSnapshot(width, height, safeArea, out _snapshot), Is.True);
            }
        }
    }
}
