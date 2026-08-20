using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GenerationalHandles.Samples
{
    /// <summary>slot再利用、generation更新、古いhandle拒否を実Buttonで確認するサンプル。</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GenerationalHandleBasicsController : MonoBehaviour
    {
        /// <summary>card要素名。</summary>
        public const string CardElementName = "generational-handle-basics-card";

        /// <summary>title要素名。</summary>
        public const string TitleElementName = "generational-handle-basics-title";

        /// <summary>説明要素名。</summary>
        public const string DescriptionElementName = "generational-handle-basics-description";

        /// <summary>契約表示要素名。</summary>
        public const string ConfigurationElementName = "generational-handle-basics-configuration";

        /// <summary>pool状態要素名。</summary>
        public const string PoolElementName = "generational-handle-basics-pool";

        /// <summary>操作結果要素名。</summary>
        public const string StageElementName = "generational-handle-basics-stage";

        /// <summary>handle表示要素名。</summary>
        public const string ResultElementName = "generational-handle-basics-result";

        /// <summary>Button列要素名。</summary>
        public const string ButtonRowElementName = "generational-handle-basics-buttons";

        /// <summary>最初のAcquire Button要素名。</summary>
        public const string AcquireFirstButtonElementName = "generational-handle-basics-acquire-first";

        /// <summary>2番目のAcquire Button要素名。</summary>
        public const string AcquireSecondButtonElementName = "generational-handle-basics-acquire-second";

        /// <summary>最初のRelease Button要素名。</summary>
        public const string ReleaseFirstButtonElementName = "generational-handle-basics-release-first";

        /// <summary>再Acquire Button要素名。</summary>
        public const string ReacquireButtonElementName = "generational-handle-basics-reacquire";

        /// <summary>古いhandle検証Button要素名。</summary>
        public const string RejectStaleButtonElementName = "generational-handle-basics-reject-stale";

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _card;
        private VisualElement _buttonRow;
        private Label _title;
        private Label _description;
        private Label _configuration;
        private Label _poolLabel;
        private Label _stage;
        private Label _result;
        private Button[] _buttons;
        private GenerationHandlePool _pool;
        private GenerationHandle _firstHandle;
        private GenerationHandle _secondHandle;
        private GenerationHandle _reusedHandle;
        private GenerationHandleError _lastError;
        private bool _staleRejected;
        private int _buttonActionCount;

        /// <summary>最初に割り当てたhandle。</summary>
        public GenerationHandle FirstHandle => _firstHandle;

        /// <summary>2番目に割り当てたhandle。</summary>
        public GenerationHandle SecondHandle => _secondHandle;

        /// <summary>解放slotへ再割当したhandle。</summary>
        public GenerationHandle ReusedHandle => _reusedHandle;

        /// <summary>最後の操作error。</summary>
        public GenerationHandleError LastError => _lastError;

        /// <summary>古いhandleが新しいentryを変えずに拒否されたか。</summary>
        public bool StaleRejected => _staleRejected;

        /// <summary>実Button操作数。</summary>
        public int ButtonActionCount => _buttonActionCount;

        /// <summary>現在有効なhandle数。</summary>
        public int ActiveCount => _pool?.ActiveCount ?? 0;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            BuildUi();
            ResetStateCore();
        }

        private void OnDisable()
        {
            if (_root != null) _root.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            if (_document != null && _document.rootVisualElement != null) _document.rootVisualElement.Clear();
            _buttons = null;
            _root = null;
            _card = null;
            _buttonRow = null;
        }

        private void BuildUi()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.style.flexGrow = 1f;
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.025f, 0.055f, 0.07f, 1f);

            _card = new VisualElement { name = CardElementName };
            _card.style.width = new Length(88f, LengthUnit.Percent);
            _card.style.height = new Length(92f, LengthUnit.Percent);
            _card.style.maxWidth = 900f;
            _card.style.backgroundColor = new Color(0.045f, 0.13f, 0.16f, 1f);
            _card.style.borderTopLeftRadius = 24f;
            _card.style.borderTopRightRadius = 24f;
            _card.style.borderBottomLeftRadius = 24f;
            _card.style.borderBottomRightRadius = 24f;
            _card.style.justifyContent = Justify.Center;
            _root.Add(_card);

            _title = AddLabel(TitleElementName, "Generational Handle Basics", 31f, new Color(0.93f, 1f, 0.98f, 1f));
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _description = AddLabel(DescriptionElementName, "同じslotを安全に再利用し、古い参照をgenerationで拒否します。", 15f, new Color(0.78f, 0.91f, 0.90f, 1f));
            _configuration = AddLabel(ConfigurationElementName, "CAPACITY 3  ·  LOWEST FREE SLOT  ·  GENERATION CHECK  ·  NO GLOBAL", 12f, new Color(0.56f, 1f, 0.72f, 1f));
            _configuration.style.unityFontStyleAndWeight = FontStyle.Bold;
            _poolLabel = AddLabel(PoolElementName, string.Empty, 13f, new Color(0.90f, 0.98f, 0.97f, 1f));
            _stage = AddLabel(StageElementName, string.Empty, 17f, new Color(0.48f, 0.90f, 1f, 1f));
            _stage.style.unityFontStyleAndWeight = FontStyle.Bold;

            _result = AddLabel(ResultElementName, string.Empty, 12f, new Color(0.85f, 0.97f, 0.95f, 1f));
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.backgroundColor = new Color(0.02f, 0.07f, 0.085f, 1f);
            _result.style.borderTopLeftRadius = 10f;
            _result.style.borderTopRightRadius = 10f;
            _result.style.borderBottomLeftRadius = 10f;
            _result.style.borderBottomRightRadius = 10f;
            _result.style.paddingTop = 8f;
            _result.style.paddingBottom = 8f;

            _buttonRow = new VisualElement { name = ButtonRowElementName };
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.flexWrap = Wrap.Wrap;
            _buttonRow.style.justifyContent = Justify.Center;
            _card.Add(_buttonRow);

            _buttons = new[]
            {
                CreateButton(AcquireFirstButtonElementName, "Acquire A", AcquireFirst),
                CreateButton(AcquireSecondButtonElementName, "Acquire B", AcquireSecond),
                CreateButton(ReleaseFirstButtonElementName, "Release A", ReleaseFirst),
                CreateButton(ReacquireButtonElementName, "Reacquire", Reacquire),
                CreateButton(RejectStaleButtonElementName, "Reject Stale A", RejectStale)
            };
            for (var index = 0; index < _buttons.Length; index++) _buttonRow.Add(_buttons[index]);

            _root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            ApplyResponsiveLayout();
        }

        private Label AddLabel(string elementName, string text, float fontSize, Color color)
        {
            var label = new Label(text) { name = elementName };
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            _card.Add(label);
            return label;
        }

        private static Button CreateButton(string elementName, string text, Action callback)
        {
            var button = new Button(callback) { name = elementName, text = text };
            button.style.flexGrow = 1f;
            button.style.color = new Color(0.035f, 0.13f, 0.15f, 1f);
            button.style.backgroundColor = new Color(0.71f, 0.95f, 0.87f, 1f);
            return button;
        }

        private void AcquireFirst()
        {
            _lastError = _pool.TryAcquire(out var handle, out var error) ? GenerationHandleError.None : error;
            if (_lastError == GenerationHandleError.None) _firstHandle = handle;
            CompleteAction(_lastError == GenerationHandleError.None ? "A acquired  ·  Slot 0 / Generation 1" : $"Acquire A failed  ·  {_lastError}");
        }

        private void AcquireSecond()
        {
            _lastError = _pool.TryAcquire(out var handle, out var error) ? GenerationHandleError.None : error;
            if (_lastError == GenerationHandleError.None) _secondHandle = handle;
            CompleteAction(_lastError == GenerationHandleError.None ? "B acquired  ·  next ascending slot" : $"Acquire B failed  ·  {_lastError}");
        }

        private void ReleaseFirst()
        {
            _lastError = _pool.Release(_firstHandle);
            CompleteAction(_lastError == GenerationHandleError.None ? "A released  ·  Slot 0 advanced" : $"Release A failed  ·  {_lastError}");
        }

        private void Reacquire()
        {
            _lastError = _pool.TryAcquire(out var handle, out var error) ? GenerationHandleError.None : error;
            if (_lastError == GenerationHandleError.None) _reusedHandle = handle;
            CompleteAction(_lastError == GenerationHandleError.None ? "Smallest slot reused  ·  Generation 2" : $"Reacquire failed  ·  {_lastError}");
        }

        private void RejectStale()
        {
            var activeBefore = _pool.ActiveCount;
            _lastError = _pool.Release(_firstHandle);
            _staleRejected = _lastError == GenerationHandleError.StaleHandle
                && _pool.ActiveCount == activeBefore
                && _pool.IsActive(_reusedHandle);
            CompleteAction(_staleRejected ? "Stale A rejected  ·  reused entry unchanged" : "Stale guard failed");
        }

        private void CompleteAction(string stage)
        {
            _buttonActionCount++;
            _stage.text = stage;
            RefreshLabels();
        }

        private void ResetStateCore()
        {
            _pool = new GenerationHandlePool(3);
            _firstHandle = default;
            _secondHandle = default;
            _reusedHandle = default;
            _lastError = GenerationHandleError.None;
            _staleRejected = false;
            _buttonActionCount = 0;
            _stage.text = "Ready  ·  run the deterministic allocation sequence";
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _poolLabel.text = $"ACTIVE {_pool.ActiveCount}   ·   AVAILABLE {_pool.AvailableCount}   ·   RETIRED {_pool.RetiredCount}   ·   ACTIONS {_buttonActionCount}";
            _result.text = $"A {Format(_firstHandle)}   ·   B {Format(_secondHandle)}   ·   REUSED {Format(_reusedHandle)}   ·   ERROR {_lastError}";
        }

        private static string Format(GenerationHandle handle) => handle.IsValid ? $"{handle.Slot}:{handle.Generation}" : "-";

        private void HandleGeometryChanged(GeometryChangedEvent _) => ApplyResponsiveLayout();

        private void ApplyResponsiveLayout()
        {
            if (_root == null || _card == null || _buttons == null) return;
            var compact = _root.resolvedStyle.width > 0f && (_root.resolvedStyle.width < 720f || _root.resolvedStyle.height < 440f);
            _card.style.paddingLeft = compact ? 14f : 32f;
            _card.style.paddingRight = compact ? 14f : 32f;
            _card.style.paddingTop = compact ? 10f : 24f;
            _card.style.paddingBottom = compact ? 10f : 24f;
            _title.style.fontSize = compact ? 22f : 31f;
            _title.style.marginBottom = compact ? 4f : 10f;
            _description.style.fontSize = compact ? 11f : 15f;
            _description.style.marginBottom = compact ? 5f : 10f;
            _configuration.style.fontSize = compact ? 9f : 12f;
            _configuration.style.marginBottom = compact ? 3f : 8f;
            _poolLabel.style.fontSize = compact ? 10f : 13f;
            _poolLabel.style.marginBottom = compact ? 3f : 7f;
            _stage.style.fontSize = compact ? 13f : 17f;
            _stage.style.marginBottom = compact ? 4f : 8f;
            _result.style.fontSize = compact ? 8.5f : 12f;
            _result.style.paddingTop = compact ? 4f : 8f;
            _result.style.paddingBottom = compact ? 4f : 8f;
            _result.style.marginBottom = compact ? 4f : 9f;
            _buttonRow.style.marginTop = compact ? 1f : 3f;
            for (var index = 0; index < _buttons.Length; index++)
            {
                _buttons[index].style.flexBasis = compact ? 160f : 130f;
                _buttons[index].style.minWidth = compact ? 140f : 110f;
                _buttons[index].style.minHeight = compact ? 30f : 42f;
                _buttons[index].style.fontSize = compact ? 11f : 13f;
                _buttons[index].style.marginLeft = 4f;
                _buttons[index].style.marginRight = 4f;
                _buttons[index].style.marginTop = 2f;
                _buttons[index].style.marginBottom = 2f;
            }
        }
    }
}
