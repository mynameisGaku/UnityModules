using System;
using Containers;

namespace DebugMenu
{
    /// <summary>
    /// デバッグメニューの 1 ページ。行の木を持ち、見えている行を平坦化して差し出す。
    /// <para>
    /// カーソル位置はページごとに覚える。ページを行き来しても、戻ったときに
    /// さっき見ていた場所から続けられる方が実用的なため。
    /// </para>
    /// </summary>
    public sealed class DebugPage
    {
        private readonly FastList<DebugRow> _visibleRows = new FastList<DebugRow>();

        private int _cursorIndex;
        private bool _rowsDirty = true;
        private uint _lastStructureVersion;

        /// <summary>ページ名を指定して作る。</summary>
        /// <param name="name">ページ名。ページ一覧と保存キーに使う。</param>
        public DebugPage(string name)
        {
            Name = name ?? string.Empty;
            Root = new DebugElement(Name) { IsExpanded = true, IsExpandable = false };
        }

        /// <summary>ページ名。</summary>
        public string Name { get; }

        /// <summary>このページの行を束ねる根。直接ここへ足してもよい。</summary>
        public DebugElement Root { get; }

        /// <summary>どの行にもカーソルが無いときに画面下へ出す説明文。</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 行の並びが変わったことを知らせる。展開状態を変えたり行を足したりしたあとに呼ぶ。
        /// <para>
        /// 平坦化の結果を毎フレーム作り直さないための印。呼び忘れても
        /// <see cref="VisibleRows"/> が版数の変化で気づくので、表示が壊れることはない。
        /// </para>
        /// </summary>
        public void Invalidate() => _rowsDirty = true;

        /// <summary>
        /// 見えている行の並び。展開されていない子は含まない。
        /// <para>戻り値は内部バッファなので、次に呼ぶまでの間だけ有効。</para>
        /// </summary>
        public FastList<DebugRow> VisibleRows
        {
            get
            {
                EnsureRows();
                return _visibleRows;
            }
        }

        /// <summary>カーソルが指す行の位置。範囲外は自動的に丸められる。</summary>
        public int CursorIndex
        {
            get
            {
                // 読むときにも平坦化を確定させる。行が減ったあと VisibleRows を
                // 誰も読まないまま位置だけを問われると、消えた行を指したまま返ってしまう。
                EnsureRows();
                return _cursorIndex;
            }
            set
            {
                var rows = VisibleRows;
                if (rows.Count == 0)
                {
                    _cursorIndex = 0;
                    return;
                }

                _cursorIndex = value < 0 ? 0 : value >= rows.Count ? rows.Count - 1 : value;
            }
        }

        /// <summary>カーソルが指す行。行が無ければ null。</summary>
        public DebugElement CurrentElement
        {
            get
            {
                var rows = VisibleRows;
                return rows.Count == 0 ? null : rows[CursorIndex].Element;
            }
        }

        /// <summary>カーソルを動かす。端では止まる（折り返さない）。</summary>
        /// <param name="delta">動かす行数。負で上。</param>
        public void MoveCursor(int delta) => CursorIndex = _cursorIndex + delta;

        /// <summary>
        /// カーソルを折り返しありで動かす。長い一覧では端で止まる方が扱いやすいが、
        /// 短い一覧では折り返した方が速いので、呼び分けられるようにしてある。
        /// </summary>
        /// <param name="delta">動かす行数。負で上。</param>
        public void MoveCursorWrapped(int delta)
        {
            var rows = VisibleRows;
            if (rows.Count == 0) return;

            var next = (_cursorIndex + delta) % rows.Count;
            _cursorIndex = next < 0 ? next + rows.Count : next;
        }

        /// <summary>指定の行にカーソルを合わせる。見つからなければ何もしない。</summary>
        /// <param name="element">合わせたい行。</param>
        public bool FocusOn(DebugElement element)
        {
            if (element == null) return false;

            var rows = VisibleRows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (!ReferenceEquals(rows[i].Element, element)) continue;

                _cursorIndex = i;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 子ページを組み込む。<see cref="DebugAttachMode.Page"/> なら遷移行を 1 行置き、
        /// <see cref="DebugAttachMode.Inline"/> ならその場に展開する。
        /// </summary>
        /// <param name="child">組み込む子ページ。</param>
        /// <param name="mode">組み込み方。</param>
        /// <param name="label">遷移行の表示名。省略すると子ページ名。</param>
        public DebugElement AddChildPage(DebugPage child, DebugAttachMode mode = DebugAttachMode.Page, string label = null)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));

            var link = new DebugPageLink(label ?? child.Name, child, mode);
            Root.Add(link);
            Invalidate();
            return link;
        }

        /// <summary>画面に出ている行だけを 1 フレーム進める。グラフの標本採取に使う。</summary>
        /// <param name="deltaSeconds">前フレームからの経過秒。</param>
        public void Tick(float deltaSeconds)
        {
            var rows = VisibleRows;
            for (var i = 0; i < rows.Count; i++) rows[i].Element.TryTick(deltaSeconds);
        }

        /// <summary>
        /// このページ以下の全ての行を、展開状態に関係なく訪ねる。
        /// 保存・復元・検索・ショートカットの走査に使う。
        /// </summary>
        /// <param name="visit">各行に対して呼ばれる処理。</param>
        public void VisitAll(Action<DebugElement> visit)
        {
            if (visit == null) throw new ArgumentNullException(nameof(visit));
            VisitRecursive(Root, visit);
        }

        private static void VisitRecursive(DebugElement element, Action<DebugElement> visit)
        {
            var children = element.Children;
            for (var i = 0; i < children.Count; i++)
            {
                visit(children[i]);
                VisitRecursive(children[i], visit);
            }
        }

        private void Rebuild()
        {
            _visibleRows.Clear();
            Flatten(Root, 0);
            _rowsDirty = false;
            _lastStructureVersion = DebugElement.StructureVersion;

            // 行が減ってカーソルが外に出ることがあるので、ここで詰める。
            if (_cursorIndex >= _visibleRows.Count) _cursorIndex = Math.Max(0, _visibleRows.Count - 1);
        }

        private void EnsureRows()
        {
            if (_rowsDirty || _lastStructureVersion != DebugElement.StructureVersion) Rebuild();
        }

        private void Flatten(DebugElement element, int depth)
        {
            var children = element.Children;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                _visibleRows.Add(new DebugRow(child, depth));

                if (child.IsExpanded && child.HasChildren) Flatten(child, depth + 1);
            }
        }
    }
}
