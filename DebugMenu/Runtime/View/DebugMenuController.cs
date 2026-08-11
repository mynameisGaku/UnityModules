using UnityEngine;
using UnityEngine.UIElements;

namespace DebugMenu
{
    /// <summary>
    /// デバッグメニューをゲームに載せる入口。トグルキー、入力の取り込み、描画の更新を受け持つ。
    /// <para>
    /// これ 1 つをシーンに置けば動く。ページの中身は
    /// <see cref="DebugMenuRegisterAttribute"/> の付いたメソッドから自動的に集まる。
    /// </para>
    /// </summary>
    [AddComponentMenu("Debug/Debug Menu")]
    [DisallowMultipleComponent]
    public sealed class DebugMenuController : MonoBehaviour
    {
        [Header("表示")]
        [Tooltip("ランタイム UI Toolkit に必要。未設定なら Tools > Debug Menu から作れる。")]
        // Inspector から入るので明示的に null を代入しておく。
        // そうしないと「一度も代入されていない」と警告される（CS0649）。
        [SerializeField] private PanelSettings _panelSettings = null;

        [Tooltip("メニューを出し入れするキー。")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        [Tooltip("出ている間 Time.timeScale を 0 にする。")]
        [SerializeField] private bool _pauseWhileVisible = true;

        [Tooltip("起動した時点で開いておく。開発ビルドで常時出しておきたい場合に使う。")]
        [SerializeField] private bool _visibleOnStart = false;

        [Header("見た目")]
        [Tooltip("色、文字サイズ、パネル寸法。USS アセットは不要。")]
        [SerializeField] private DebugMenuTheme _theme = new DebugMenuTheme();

        [Header("保存")]
        [Tooltip("起動時に前回の値を読み込み、終了時に書き出す。")]
        [SerializeField] private bool _persistValues = true;

        private UIDocument _document;
        private DebugMenuView _view;
        private DebugMenuInputRepeater _repeater;
        private DebugMenuSettings _settings;
        private DebugMenuFavorites _favorites;
        private DebugMenuHistory _history;
        private DebugMenuSearchPage _searchPage;

        private float _savedTimeScale = 1f;
        private bool _ownsTimeScalePause;
        private bool _pendingVisibleOnStart;
        private bool _themeRefreshPending;
        private bool _beginSearchEditPending;

        /// <summary>メニュー本体。ページを直接足したいときに使う。</summary>
        public DebugMenuRoot Menu { get; private set; }

        /// <summary>色、文字サイズ、パネル寸法。ビュー生成前に変更する。</summary>
        public DebugMenuTheme Theme => _theme ??= new DebugMenuTheme();

        /// <summary>値変更の取り消しとやり直しを管理する履歴。</summary>
        public DebugMenuHistory History => _history;

        /// <summary>全ページを対象にする検索画面。</summary>
        public DebugMenuSearchPage SearchPage => _searchPage;

        /// <summary>
        /// 入力状態を埋める処理。差し替えれば任意の入力系に対応できる。
        /// 既定では Input System と旧 Input のうち使える方を自動で選ぶ。
        /// </summary>
        public System.Func<DebugMenuInputState> InputProvider { get; set; }

        private void Awake()
        {
            Menu = new DebugMenuRoot { PauseWhileVisible = _pauseWhileVisible };
            _repeater = new DebugMenuInputRepeater();
            _favorites = new DebugMenuFavorites();

            DebugMenuAutoRegistrar.Populate(Menu);

            // お気に入りは最後に足す。先に足すと、まだ登録されていないページを拾えない。
            Menu.AddPage(_favorites.Page);

            if (_persistValues)
            {
                _settings = new DebugMenuSettings();
                _settings.Load(Menu);
            }

            _searchPage = new DebugMenuSearchPage(Menu);
            Menu.AddPage(_searchPage.Page);

            _history = new DebugMenuHistory();
            _history.Attach(Menu);

            InputProvider ??= ReadKeyboard;

            Menu.VisibilityChanged += OnVisibilityChanged;

            // 表示はビューが組み上がってから。ここで立てても映す先がまだ無い。
            _pendingVisibleOnStart = _visibleOnStart;
        }

        private void OnDestroy()
        {
            _history?.Dispose();
            if (Menu != null) Menu.VisibilityChanged -= OnVisibilityChanged;
            if (_persistValues && _settings != null && Menu != null) _settings.Save(Menu);

            if (_view != null)
            {
                _view.CancelPointerInteractions();
                _view.Root.RemoveFromHierarchy();
                _view = null;
            }

            RestoreTimeScale();
        }

        private void OnDisable()
        {
            if (Menu != null && Menu.IsVisible) Menu.SetVisible(false);
            else RestoreTimeScale();
        }

        private void Update()
        {
            if (Menu == null) return;
            if (_themeRefreshPending && (_view == null || !_view.IsEditingText))
            {
                _themeRefreshPending = false;
                ApplyTheme();
            }
            SyncTimeScalePause();
            if (!EnsureView()) return;

            if (_beginSearchEditPending && Menu.IsVisible)
            {
                _view.Refresh();
                if (_view.TryBeginEditCurrent()) _beginSearchEditPending = false;
            }

            if (DebugMenuKeyboard.WasPressed(_toggleKey)) Menu.Toggle();

            var textInputConsumed = _view.ConsumeTextInput();
            var shortcutInvoked = !textInputConsumed && Menu.TryInvokeShortcut(
                key => key != _toggleKey && DebugMenuKeyboard.WasPressed(key));

            if (!Menu.IsVisible) return;

            if (textInputConsumed || shortcutInvoked)
            {
                _repeater.Reset();
                UpdateVisibleMenu();
                return;
            }

            var state = InputProvider();
            var command = _repeater.Poll(state, Time.unscaledDeltaTime);
            if (command == DebugMenuCommand.Search)
            {
                OpenSearch();
                _repeater.Reset();
            }
            else if (command == DebugMenuCommand.Decide && _view.TryBeginEditCurrent()) _repeater.Reset();
            else if (command != DebugMenuCommand.None)
            {
                if (command == DebugMenuCommand.PreviousPage || command == DebugMenuCommand.NextPage)
                {
                    _view.CancelPointerInteractions();
                }

                DebugMenuCommandDispatcher.Dispatch(Menu, command, _history);
            }

            UpdateVisibleMenu();
        }

        /// <summary>
        /// <see cref="Theme"/> の現在値で表示だけを作り直す。
        /// ページ、カーソル、編集値、表示状態はメニュー本体に残る。
        /// </summary>
        public void ApplyTheme()
        {
            if (_view == null) return;

            // 入力途中の文字を捨てない。確定または取消の直後に Update から再適用する。
            if (_view.IsEditingText)
            {
                _themeRefreshPending = true;
                return;
            }

            _themeRefreshPending = false;
            _view.CancelPointerInteractions();
            _view.Root.RemoveFromHierarchy();
            _view = null;
            EnsureView();
        }

        /// <summary>全体検索ページを開き、次のレイアウト更新で検索語入力を始める。</summary>
        public void OpenSearch()
        {
            if (_searchPage == null || Menu == null) return;

            _view?.CancelPointerInteractions();
            _searchPage.Open();
            _beginSearchEditPending = true;
            _view?.Refresh();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) _themeRefreshPending = true;
        }

        private void UpdateVisibleMenu()
        {
            if (Menu == null || !Menu.IsVisible) return;

            // ピン留めが変わったときだけ組み直す。
            _favorites.SyncIfDirty(Menu);

            Menu.Tick(Time.unscaledDeltaTime);
            _view.Refresh();
        }

        /// <summary>
        /// ビューを 1 度だけ組み立てる。
        /// <para>
        /// <c>UIDocument.rootVisualElement</c> は <c>Awake</c> の時点ではまだ無い。
        /// <c>panelSettings</c> を入れたあとに作られるので、揃うまで毎フレーム試す。
        /// ここを Awake でやると、無言で何も描かれない状態になる。
        /// </para>
        /// </summary>
        /// <returns>ビューが使える状態なら true。</returns>
        private bool EnsureView()
        {
            if (_view != null) return true;

            if (_panelSettings == null)
            {
                enabled = false;
                Debug.LogError(
                    "[DebugMenu] PanelSettings が未設定なので表示できない。" +
                    " Tools > Debug Menu > Create Panel Settings で作って割り当てること。",
                    this);
                return false;
            }

            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
                if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            }

            if (_document.panelSettings != _panelSettings) _document.panelSettings = _panelSettings;

            var root = _document.rootVisualElement;
            if (root == null) return false;   // まだ組み上がっていない。次のフレームで再挑戦する。

            _view = new DebugMenuView(Menu, Theme);
            root.Add(_view.Root);

            // 閉じている間は要素ごと外す。非表示でもレイアウトの計算は走るため。
            _view.Root.style.display = Menu.IsVisible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_pendingVisibleOnStart)
            {
                _pendingVisibleOnStart = false;
                Menu.SetVisible(true);
            }

            return true;
        }

        private void OnVisibilityChanged(bool visible)
        {
            if (_view != null)
            {
                if (!visible) _view.CancelPointerInteractions();
                _view.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            _repeater.Reset();
            SyncTimeScalePause();
        }

        private void SyncTimeScalePause()
        {
            if (Menu == null || !Menu.IsVisible || !Menu.PauseWhileVisible)
            {
                RestoreTimeScale();
                return;
            }

            if (_ownsTimeScalePause) return;

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _ownsTimeScalePause = true;
        }

        private void RestoreTimeScale()
        {
            if (!_ownsTimeScalePause) return;

            Time.timeScale = _savedTimeScale;
            _ownsTimeScalePause = false;
        }

        /// <summary>
        /// 既定の入力読み取り。Input System と旧 Input のうち使える方から読む。
        /// </summary>
        private static DebugMenuInputState ReadKeyboard()
        {
            var control =
                DebugMenuKeyboard.IsHeld(KeyCode.LeftControl) ||
                DebugMenuKeyboard.IsHeld(KeyCode.RightControl) ||
                DebugMenuKeyboard.IsHeld(KeyCode.LeftCommand) ||
                DebugMenuKeyboard.IsHeld(KeyCode.RightCommand);
            var shift = DebugMenuKeyboard.IsHeld(KeyCode.LeftShift) || DebugMenuKeyboard.IsHeld(KeyCode.RightShift);
            var pressedF = DebugMenuKeyboard.WasPressed(KeyCode.F);
            var pressedZ = DebugMenuKeyboard.WasPressed(KeyCode.Z);

            return new DebugMenuInputState
            {
                Up = DebugMenuKeyboard.IsHeld(KeyCode.UpArrow),
                Down = DebugMenuKeyboard.IsHeld(KeyCode.DownArrow),
                Left = DebugMenuKeyboard.IsHeld(KeyCode.LeftArrow),
                Right = DebugMenuKeyboard.IsHeld(KeyCode.RightArrow),
                Decide = DebugMenuKeyboard.WasPressed(KeyCode.Return) || DebugMenuKeyboard.WasPressed(KeyCode.KeypadEnter),
                Cancel = DebugMenuKeyboard.WasPressed(KeyCode.Escape),
                PageUp = DebugMenuKeyboard.IsHeld(KeyCode.PageUp),
                PageDown = DebugMenuKeyboard.IsHeld(KeyCode.PageDown),
                PreviousPage = DebugMenuKeyboard.WasPressed(KeyCode.LeftBracket),
                NextPage = DebugMenuKeyboard.WasPressed(KeyCode.RightBracket),
                ToggleFavorite = !control && pressedF,
                ResetValue = !control && DebugMenuKeyboard.WasPressed(KeyCode.R),
                Search = control && pressedF,
                Undo = control && !shift && pressedZ,
                Redo = control && (DebugMenuKeyboard.WasPressed(KeyCode.Y) || shift && pressedZ),
            };
        }
    }
}
