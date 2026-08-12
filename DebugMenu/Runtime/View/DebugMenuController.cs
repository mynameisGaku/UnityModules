using System;
using System.Collections.Generic;
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
        private const float InputErrorLogIntervalSeconds = 5f;
        private const int InputErrorLogCacheCapacity = 32;

        private static readonly Func<DebugMenuInputState> DefaultInputProvider = ReadDefaultInput;

        /// <summary>入力プロバイダーと例外内容の組を、警告の抑制単位として保持する。</summary>
        private readonly struct InputProviderErrorKey : IEquatable<InputProviderErrorKey>
        {
            public InputProviderErrorKey(Func<DebugMenuInputState> provider, string error)
            {
                Provider = provider;
                Error = error;
            }

            private Func<DebugMenuInputState> Provider { get; }
            private string Error { get; }

            public bool Equals(InputProviderErrorKey other)
            {
                return ReferenceEquals(Provider, other.Provider) &&
                       string.Equals(Error, other.Error, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is InputProviderErrorKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Provider != null ? Provider.GetHashCode() : 0) * 397) ^
                           StringComparer.Ordinal.GetHashCode(Error ?? string.Empty);
                }
            }
        }

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

        [Tooltip("通常保存とプロファイル保存で使う形式。読み込みは内容から自動判別する。")]
        [SerializeField] private DebugMenuSettingsFormat _settingsFormat = DebugMenuSettingsFormat.Json;

        private UIDocument _document;
        private DebugMenuView _view;
        private DebugMenuInputRepeater _repeater;
        private DebugMenuSettings _settings;
        private DebugMenuFavorites _favorites;
        private DebugMenuHistory _history;
        private DebugMenuSearchPage _searchPage;
        private DebugMenuProfiles _profiles;
        private DebugMenuSettingsPage _settingsPage;
        private DebugMenuAppearancePage _appearancePage;
        private DebugMenuRecentChanges _recentChanges;
        private DebugMenuToastService _toasts;

        /// <summary>入力取得警告を再び出してよい実時間を、プロバイダーと例外内容ごとに保持する。</summary>
        private readonly Dictionary<InputProviderErrorKey, float> _inputErrorNextLogTimes =
            new Dictionary<InputProviderErrorKey, float>();

        /// <summary>期限切れの警告抑制キーを列挙中に退避する。</summary>
        private readonly List<InputProviderErrorKey> _expiredInputErrorKeys = new List<InputProviderErrorKey>();

        /// <summary>共有の一時停止管理へ渡す、このコントローラー固有の識別子。</summary>
        private readonly object _timeScalePauseOwner = new object();

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

        /// <summary>名前付き設定プロファイル。</summary>
        public DebugMenuProfiles Profiles => _profiles;

        /// <summary>最近変更した項目の一覧。</summary>
        public DebugMenuRecentChanges RecentChanges => _recentChanges;

        /// <summary>プロファイルと任意ファイル保存を操作するページ。</summary>
        public DebugMenuSettingsPage SettingsPage => _settingsPage;

        /// <summary>実行中に文字とGUIの寸法を調整するページ。</summary>
        public DebugMenuAppearancePage AppearancePage => _appearancePage;

        /// <summary>設定操作などの短い結果を画面へ出す通知サービス。</summary>
        public DebugMenuToastService Toasts => _toasts;

        /// <summary>
        /// 入力状態を埋める処理。差し替えれば任意の入力系に対応できる。
        /// 差し替えた処理は従来どおり、メニュー表示中に操作を読むときだけ呼ばれる。
        /// 既定処理だけは非表示中も Start を読み、メニューを開ける。
        /// </summary>
        public System.Func<DebugMenuInputState> InputProvider { get; set; }

        private void Awake()
        {
            Menu = new DebugMenuRoot { PauseWhileVisible = _pauseWhileVisible };
            _repeater = new DebugMenuInputRepeater();
            _favorites = new DebugMenuFavorites();
            _toasts = new DebugMenuToastService();

            DebugMenuAutoRegistrar.Populate(Menu);

            // お気に入りは自動登録の後に足し、登録済みページを全て拾えるようにする。
            Menu.AddPage(_favorites.Page);

            // 保存値を復元できるよう、設定の読み込みより先に外観行を登録する。
            _appearancePage = new DebugMenuAppearancePage(Theme, RequestApplyTheme);
            Menu.AddPage(_appearancePage.Page);

            _settings = new DebugMenuSettings(format: _settingsFormat);
            if (_persistValues) LoadPersistedValues();

            _profiles = new DebugMenuProfiles(format: _settingsFormat);
            _settingsPage = new DebugMenuSettingsPage(Menu, _settings, _profiles, _toasts);
            Menu.AddPage(_settingsPage.Page);

            _searchPage = new DebugMenuSearchPage(Menu);
            Menu.AddPage(_searchPage.Page);

            _recentChanges = new DebugMenuRecentChanges();
            _recentChanges.Attach(Menu);
            Menu.AddPage(_recentChanges.Page);

            _history = new DebugMenuHistory();
            _history.Attach(Menu);

            InputProvider ??= DefaultInputProvider;

            Menu.VisibilityChanged += OnVisibilityChanged;

            // 表示はビューが組み上がってから。ここで立てても映す先がまだ無い。
            _pendingVisibleOnStart = _visibleOnStart;
        }

        private void OnDestroy()
        {
            try
            {
                _history?.Dispose();
                _recentChanges?.Dispose();
                _settingsPage?.Dispose();
                if (Menu != null) Menu.VisibilityChanged -= OnVisibilityChanged;
                if (_persistValues && _settings != null && Menu != null) SavePersistedValues();
            }
            finally
            {
                if (_view != null)
                {
                    _view.CancelPointerInteractions();
                    _view.Root.RemoveFromHierarchy();
                    _view = null;
                }

                RestoreTimeScale();
            }
        }

        private void OnDisable()
        {
            if (Menu != null && Menu.IsVisible) Menu.SetVisible(false);
            else RestoreTimeScale();
        }

        private void LoadPersistedValues()
        {
            try
            {
                _settings.Load(Menu);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DebugMenu] 起動時の保存値を読めなかった。既定値で続行する。\n{exception.Message}", this);
            }
        }

        private void SavePersistedValues()
        {
            try
            {
                _settings.Save(Menu);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DebugMenu] 終了時の保存に失敗した。\n{exception.Message}", this);
            }
        }

        private void Update()
        {
            if (Menu == null) return;
            _history?.Refresh();
            _toasts?.Tick(Time.unscaledDeltaTime);
            if (_themeRefreshPending &&
                (_view == null || (!_view.IsEditingText && !_view.HasActivePointerInteraction)))
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

            var inputProvider = InputProvider;
            var usesDefaultInput = ReferenceEquals(inputProvider, DefaultInputProvider);
            var state = default(DebugMenuInputState);
            if (usesDefaultInput) TryReadInputProvider(inputProvider, out state);
            var toggleRequested = DebugMenuKeyboard.WasPressed(_toggleKey) || state.ToggleMenu;
            state.ToggleMenu = false;
            if (toggleRequested) Menu.Toggle();

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

            // 差し替え入力は従来どおり、表示中にメニュー操作を読むときだけ呼ぶ。
            if (!usesDefaultInput) TryReadInputProvider(inputProvider, out state);

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
        /// 既定または差し替え入力を例外境界の内側で読む。
        /// 失敗時は入力なしへ戻し、押しっぱなし状態を次の正常入力へ持ち越さない。
        /// </summary>
        /// <param name="provider">今フレームに読む入力プロバイダー。null は入力なしとして扱う。</param>
        /// <param name="state">取得した入力状態。失敗時は全操作が解除された状態。</param>
        /// <returns>正常に取得できたか。null は正常な入力なしとして true。</returns>
        private bool TryReadInputProvider(Func<DebugMenuInputState> provider, out DebugMenuInputState state)
        {
            state = default;
            if (provider == null) return true;

            try
            {
                state = provider();
                return true;
            }
            catch (Exception exception)
            {
                _repeater?.Reset();
                LogInputProviderError(provider, exception);
                return false;
            }
        }

        /// <summary>同じプロバイダーの同じ例外を5秒に1回だけ、スタック情報付きで警告する。</summary>
        private void LogInputProviderError(Func<DebugMenuInputState> provider, Exception exception)
        {
            var now = Time.realtimeSinceStartup;
            var signature = exception.GetType().FullName + ": " + exception.Message;
            var key = new InputProviderErrorKey(provider, signature);
            if (_inputErrorNextLogTimes.TryGetValue(key, out var nextLogTime) && now < nextLogTime) return;

            RemoveExpiredInputErrorLogs(now);
            if (_inputErrorNextLogTimes.Count >= InputErrorLogCacheCapacity &&
                !_inputErrorNextLogTimes.ContainsKey(key))
            {
                RemoveOldestInputErrorLog();
            }

            _inputErrorNextLogTimes[key] = now + InputErrorLogIntervalSeconds;
            Debug.LogWarning(
                $"[DebugMenu] 入力プロバイダーの読み取りに失敗した。入力なしとして続行する。\n{exception}",
                this);
        }

        /// <summary>警告抑制の期限を過ぎた組を取り除き、差し替え済みプロバイダーを保持し続けない。</summary>
        private void RemoveExpiredInputErrorLogs(float now)
        {
            _expiredInputErrorKeys.Clear();
            foreach (var entry in _inputErrorNextLogTimes)
            {
                if (entry.Value <= now) _expiredInputErrorKeys.Add(entry.Key);
            }

            for (var i = 0; i < _expiredInputErrorKeys.Count; i++)
            {
                _inputErrorNextLogTimes.Remove(_expiredInputErrorKeys[i]);
            }
        }

        /// <summary>短時間に異なる例外が続いても警告抑制キャッシュを一定数に保つ。</summary>
        private void RemoveOldestInputErrorLog()
        {
            var found = false;
            var oldestTime = float.MaxValue;
            var oldestKey = default(InputProviderErrorKey);
            foreach (var entry in _inputErrorNextLogTimes)
            {
                if (found && entry.Value >= oldestTime) continue;

                found = true;
                oldestTime = entry.Value;
                oldestKey = entry.Key;
            }

            if (found) _inputErrorNextLogTimes.Remove(oldestKey);
        }

        /// <summary>
        /// <see cref="Theme"/> の現在値で表示だけを作り直す。
        /// ページ、カーソル、編集値、表示状態はメニュー本体に残る。
        /// </summary>
        public void ApplyTheme()
        {
            if (_view == null) return;

            // 入力途中の文字を捨てない。確定または取消の直後に Update から再適用する。
            if (_view.IsEditingText || _view.HasActivePointerInteraction)
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

        /// <summary>現在の入力イベントが終わった後でテーマを表示へ反映するよう要求する。</summary>
        public void RequestApplyTheme() => _themeRefreshPending = true;

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
            if (Application.isPlaying) RequestApplyTheme();
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

            _view = new DebugMenuView(Menu, Theme, _toasts);
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

            if (_ownsTimeScalePause)
            {
                DebugMenuPauseCoordinator.KeepPaused(_timeScalePauseOwner);
                return;
            }

            DebugMenuPauseCoordinator.Acquire(_timeScalePauseOwner);
            _ownsTimeScalePause = true;
        }

        private void RestoreTimeScale()
        {
            if (!_ownsTimeScalePause) return;

            DebugMenuPauseCoordinator.Release(_timeScalePauseOwner);
            _ownsTimeScalePause = false;
        }

        /// <summary>
        /// 既定のキーボードとゲームパッド入力を論理和でまとめる。
        /// </summary>
        private static DebugMenuInputState ReadDefaultInput()
        {
            var keyboard = ReadKeyboard();
            var gamepad = DebugMenuGamepad.Read();
            return DebugMenuInputState.Combine(keyboard, gamepad);
        }

        /// <summary>
        /// Input System と旧 Input のうち使える方からキーボードを読む。
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

    /// <summary>
    /// 複数のデバッグメニューによる一時停止をまとめ、最後の所有者が離れたときだけ復元する。
    /// </summary>
    internal static class DebugMenuPauseCoordinator
    {
        /// <summary>一時停止を要求しているコントローラーごとの識別子。</summary>
        private static readonly HashSet<object> Owners = new HashSet<object>();

        /// <summary>全所有者が離れたときに戻す時間倍率。</summary>
        private static float _resumeTimeScale = 1f;

        /// <summary>再生開始時に前回の所有情報を捨てる。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Owners.Clear();
            _resumeTimeScale = Time.timeScale;
        }

        /// <summary>一時停止の所有権を得る。既に所有している識別子は数え直さない。</summary>
        /// <param name="owner">コントローラーごとに固定された識別子。</param>
        public static void Acquire(object owner)
        {
            if (owner == null || !Owners.Add(owner)) return;

            if (Owners.Count == 1) _resumeTimeScale = Time.timeScale;
            KeepPaused();
        }

        /// <summary>
        /// 所有中の停止を維持する。外部が非ゼロ値へ変えた場合はその値を復元先として覚え直す。
        /// </summary>
        /// <param name="owner">所有権を確認する識別子。</param>
        public static void KeepPaused(object owner)
        {
            if (owner == null || !Owners.Contains(owner)) return;
            KeepPaused();
        }

        /// <summary>所有権を返す。最後の所有者なら、停止値をまだ所有している場合だけ復元する。</summary>
        /// <param name="owner">返す所有権の識別子。</param>
        public static void Release(object owner)
        {
            if (owner == null || !Owners.Remove(owner)) return;

            if (Owners.Count > 0)
            {
                KeepPaused();
                return;
            }

            // 外部が停止値を上書き済みなら、その値を尊重して古い倍率を戻さない。
            if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = _resumeTimeScale;
            _resumeTimeScale = Time.timeScale;
        }

        /// <summary>外部の非ゼロ変更を復元先へ移してから、表示中の停止をかけ直す。</summary>
        private static void KeepPaused()
        {
            var current = Time.timeScale;
            if (Mathf.Approximately(current, 0f)) return;

            _resumeTimeScale = current;
            Time.timeScale = 0f;
        }

    }
}
