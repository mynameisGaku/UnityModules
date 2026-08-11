using System;
using System.Collections.Generic;
using Containers;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// デバッグメニュー全体。ページの集まりと、いま見ているページの履歴を持つ。
    /// <para>
    /// 入力の解釈と画面の切り替えはここに集約してある。行の層はメニュー全体を知らず、
    /// 描画の層は行の並びしか見ない。3 つを分けておくと、入力も描画も無い状態で
    /// メニューの挙動をテストできる。
    /// </para>
    /// </summary>
    public sealed class DebugMenuRoot
    {
        private readonly FastList<DebugPage> _pages = new FastList<DebugPage>();

        /// <summary>いま見ているページまでの経路。戻るときに 1 枚ずつ取り出す。</summary>
        private readonly FastList<DebugPage> _pageStack = new FastList<DebugPage>();

        /// <summary>表示・非表示が切り替わったときに発火する。</summary>
        public event Action<bool> VisibilityChanged;

        /// <summary>見ているページが変わったときに発火する。</summary>
        public event Action<DebugPage> PageChanged;

        /// <summary>登録されているページ。</summary>
        public FastList<DebugPage> Pages => _pages;

        /// <summary>いま見ているページ。1 枚も無ければ null。</summary>
        public DebugPage CurrentPage => _pageStack.Count > 0 ? _pageStack[_pageStack.Count - 1] : null;

        /// <summary>表示層がパンくずを組み立てるために読む現在のページ経路。</summary>
        internal FastList<DebugPage> PageStack => _pageStack;

        /// <summary>いま見ているページの深さ。0 なら最上位。</summary>
        public int Depth => Mathf.Max(0, _pageStack.Count - 1);

        /// <summary>メニューが出ているか。</summary>
        public bool IsVisible { get; private set; }

        /// <summary>
        /// 出ている間ゲームを止めるか。既定は true。
        /// <para>
        /// 止めないと、値をいじるつもりの入力がゲームにも届く。動かしながら詰めたいときだけ
        /// false にする（その場合は操作がぶつからないか確かめること）。
        /// </para>
        /// </summary>
        public bool PauseWhileVisible { get; set; } = true;

        /// <summary>ページを登録する。最初に登録したページが起点になる。</summary>
        /// <param name="page">登録するページ。</param>
        public DebugPage AddPage(DebugPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));

            _pages.Add(page);
            if (_pageStack.Count == 0) _pageStack.Add(page);
            return page;
        }

        /// <summary>名前を指定してページを作り、登録する。</summary>
        /// <param name="name">ページ名。</param>
        public DebugPage AddPage(string name) => AddPage(new DebugPage(name));

        /// <summary>登録済みのページを名前で探す。</summary>
        /// <param name="name">ページ名。</param>
        public DebugPage FindPage(string name)
        {
            for (var i = 0; i < _pages.Count; i++)
            {
                if (string.Equals(_pages[i].Name, name, StringComparison.Ordinal)) return _pages[i];
            }

            return null;
        }

        /// <summary>出す・消すを切り替える。</summary>
        public void Toggle() => SetVisible(!IsVisible);

        /// <summary>表示状態を設定する。</summary>
        /// <param name="visible">出すなら true。</param>
        public void SetVisible(bool visible)
        {
            if (IsVisible == visible) return;

            IsVisible = visible;
            VisibilityChanged?.Invoke(visible);
        }

        /// <summary>指定のページへ潜る。戻る先として今のページが積まれる。</summary>
        /// <param name="page">移動先のページ。</param>
        public void PushPage(DebugPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (ReferenceEquals(CurrentPage, page)) return;

            _pageStack.Add(page);
            PageChanged?.Invoke(page);
        }

        /// <summary>1 つ前のページへ戻る。最上位では何も起きない。</summary>
        public bool PopPage()
        {
            if (_pageStack.Count <= 1) return false;

            _pageStack.RemoveAt(_pageStack.Count - 1);
            PageChanged?.Invoke(CurrentPage);
            return true;
        }

        /// <summary>最上位のページまで一気に戻る。</summary>
        public void PopToRoot()
        {
            if (_pageStack.Count <= 1) return;

            _pageStack.Resize(1);
            PageChanged?.Invoke(CurrentPage);
        }

        /// <summary>起点のページを差し替える。履歴は捨てられる。</summary>
        /// <param name="page">起点にするページ。</param>
        public void SetRootPage(DebugPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));

            _pageStack.Clear();
            _pageStack.Add(page);
            PageChanged?.Invoke(page);
        }

        /// <summary>登録済みの最上位ページを循環する。子ページを開いている場合は履歴を破棄する。</summary>
        /// <param name="delta">移動するページ数。負なら前へ、正なら次へ進む。</param>
        public void MoveRootPage(int delta)
        {
            if (delta == 0 || _pages.Count == 0) return;

            var currentRoot = _pageStack.Count > 0 ? _pageStack[0] : null;
            var currentIndex = currentRoot == null ? -1 : _pages.IndexOf(currentRoot);
            if (currentIndex < 0) currentIndex = delta > 0 ? -1 : 0;

            var nextIndex = (currentIndex + (delta % _pages.Count)) % _pages.Count;
            if (nextIndex < 0) nextIndex += _pages.Count;

            var nextPage = _pages[nextIndex];
            if (ReferenceEquals(CurrentPage, nextPage)) return;

            SetRootPage(nextPage);
        }

        // ── 入力の解釈 ──────────────────────────────────────────────────────

        /// <summary>カーソルを動かす。</summary>
        /// <param name="delta">動かす行数。負で上。</param>
        public void MoveCursor(int delta) => CurrentPage?.MoveCursor(delta);

        /// <summary>
        /// 決定する。遷移行なら潜り、それ以外は行に任せる。
        /// <para>
        /// 遷移をここで処理するのは、行の層がメニュー全体を知らずに済むようにするため。
        /// </para>
        /// </summary>
        public void Decide()
        {
            var element = CurrentPage?.CurrentElement;
            if (element == null) return;

            if (element is DebugPageLink link && link.Mode == DebugAttachMode.Page)
            {
                PushPage(link.Target);
                return;
            }

            element.OnDecide();
            CurrentPage.Invalidate();
        }

        /// <summary>取り消す。1 つ前のページへ戻り、最上位ならメニューを閉じる。</summary>
        public void Cancel()
        {
            if (!PopPage()) SetVisible(false);
        }

        /// <summary>左右キーの入力を今の行へ渡す。</summary>
        /// <param name="delta">左で -1、右で +1。</param>
        public void Adjust(int delta)
        {
            var element = CurrentPage?.CurrentElement;
            if (element == null || !element.IsAdjustable) return;

            element.OnAdjust(delta);
        }

        /// <summary>
        /// ショートカットキーが割り当てられた行を探して実行する。
        /// <para>
        /// 登録された全ページを走査するので、いまどのページを開いていても効く。
        /// 実行できたら true を返す。
        /// </para>
        /// </summary>
        /// <param name="key">押されたキー。</param>
        public bool TryInvokeShortcut(KeyCode key)
        {
            if (key == KeyCode.None) return false;

            return TryInvokeShortcut(shortcut => shortcut == key);
        }

        /// <summary>登録済みのショートカットを走査し、押された最初の行を実行する。</summary>
        /// <param name="isPressed">指定されたキーがこのフレームで押されたかを返す関数。</param>
        public bool TryInvokeShortcut(Func<KeyCode, bool> isPressed)
        {
            if (isPressed == null) throw new ArgumentNullException(nameof(isPressed));

            DebugElement found = null;
            DebugPage owningPage = null;
            DebugPage rootPage = null;
            var visited = new HashSet<DebugPage>();

            for (var i = 0; i < _pages.Count && found == null; i++)
            {
                var page = _pages[i];
                TryFindShortcut(page, page, isPressed, visited, out found, out owningPage, out rootPage);
            }

            if (found == null) return false;

            if (found is DebugPageLink link && link.Mode == DebugAttachMode.Page)
            {
                SetRootPage(rootPage);
                PushPage(link.Target);
            }
            else
            {
                found.OnDecide();
            }

            owningPage.Invalidate();
            if (!ReferenceEquals(owningPage, rootPage)) rootPage.Invalidate();
            return true;
        }

        private static bool TryFindShortcut(
            DebugPage page,
            DebugPage rootPage,
            Func<KeyCode, bool> isPressed,
            HashSet<DebugPage> visited,
            out DebugElement found,
            out DebugPage owningPage,
            out DebugPage foundRootPage)
        {
            found = null;
            owningPage = null;
            foundRootPage = null;

            if (page == null || !visited.Add(page)) return false;
            return TryFindShortcut(page.Root, page, rootPage, isPressed, visited, out found, out owningPage, out foundRootPage);
        }

        private static bool TryFindShortcut(
            DebugElement element,
            DebugPage owningPage,
            DebugPage rootPage,
            Func<KeyCode, bool> isPressed,
            HashSet<DebugPage> visited,
            out DebugElement found,
            out DebugPage foundOwningPage,
            out DebugPage foundRootPage)
        {
            found = null;
            foundOwningPage = null;
            foundRootPage = null;

            if (element.Shortcut != KeyCode.None && isPressed(element.Shortcut))
            {
                found = element;
                foundOwningPage = owningPage;
                foundRootPage = rootPage;
                return true;
            }

            if (element is DebugPageLink link)
            {
                return TryFindShortcut(
                    link.Target,
                    rootPage,
                    isPressed,
                    visited,
                    out found,
                    out foundOwningPage,
                    out foundRootPage);
            }

            var children = element.Children;
            for (var i = 0; i < children.Count; i++)
            {
                if (TryFindShortcut(
                        children[i],
                        owningPage,
                        rootPage,
                        isPressed,
                        visited,
                        out found,
                        out foundOwningPage,
                        out foundRootPage))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>今の行に付いている説明文。無ければページの説明文。</summary>
        public string CurrentDescription
        {
            get
            {
                var page = CurrentPage;
                if (page == null) return string.Empty;

                var element = page.CurrentElement;
                if (element != null && !string.IsNullOrEmpty(element.Description)) return element.Description;

                return page.Description;
            }
        }

        /// <summary>いま見ているページを 1 フレーム進める。</summary>
        /// <param name="deltaSeconds">前フレームからの経過秒。</param>
        public void Tick(float deltaSeconds)
        {
            if (!IsVisible) return;
            CurrentPage?.Tick(deltaSeconds);
        }

        /// <summary>登録された全ページの全行を訪ねる。保存・復元・検索に使う。</summary>
        /// <param name="visit">各行に対して呼ばれる処理。</param>
        public void VisitAll(Action<DebugPage, DebugElement> visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));

            for (var i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                page.VisitAll(element => visit(page, element));
            }
        }
    }
}
