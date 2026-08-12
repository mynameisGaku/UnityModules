using System;
using System.Collections.Generic;
using Containers;

namespace DebugMenu
{
    /// <summary>
    /// 値の変更を控え、取り消しとやり直しを提供する。
    /// <para>
    /// デバッグメニューは「触ってみて、違ったら戻す」使い方をされる。戻せないと、
    /// 元の値を覚えていない限り触ること自体をためらうことになる。
    /// </para>
    /// <para>
    /// 変更の通知は「変わったあと」に来るので、<b>直前の値をこちらで持っておく</b>必要がある。
    /// 行ごとの最後に見た値を控え、通知が来たらそれを「前の値」として使う。
    /// </para>
    /// </summary>
    public sealed class DebugMenuHistory : IDisposable
    {
        private readonly UndoRedoStack _stack;
        private readonly Dictionary<DebugElement, DebugValueSnapshot> _lastSeen =
            new Dictionary<DebugElement, DebugValueSnapshot>();

        /// <summary>接続したメニューに属する値行。別メニューから届く全体通知をここで除外する。</summary>
        private readonly HashSet<DebugElement> _scope = new HashSet<DebugElement>();

        /// <summary>現在接続しているメニュー。</summary>
        private DebugMenuRoot _menu;

        /// <summary>スコープを最後に組み直した行構造の版数。</summary>
        private uint _scopeStructureVersion;

        private bool _attached;

        /// <summary>取り消しと、やり直しの最中か。控えを取らないための印。</summary>
        private bool _applying;

        /// <summary>保持する履歴の上限を指定して作る。</summary>
        /// <param name="capacity">保持する操作の数。</param>
        public DebugMenuHistory(int capacity = 128) => _stack = new UndoRedoStack(capacity);

        /// <summary>取り消せるか。</summary>
        public bool CanUndo => _stack.CanUndo;

        /// <summary>やり直せるか。</summary>
        public bool CanRedo => _stack.CanRedo;

        /// <summary>控えている操作の数。</summary>
        public int Count => _stack.UndoCount;

        /// <summary>次に取り消される操作の表示名。無ければ null。</summary>
        public string NextUndoLabel => _stack.NextUndoLabel;

        /// <summary>
        /// 変更の受け取りを始める。
        /// <para>
        /// 同じメニューへ複数の履歴を繋いでも、それぞれが独立して変更を控える。
        /// </para>
        /// </summary>
        /// <param name="menu">控えの初期値を読むためのメニュー。</param>
        public void Attach(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (_attached) Detach();

            // 接続先を替えた後に、前のメニューの行を Undo できてはいけない。
            _stack.Clear();
            _menu = menu;
            Refresh(true);

            DebugElement.AddChangeListener(OnElementChanged);
            _attached = true;
        }

        /// <summary>変更の受け取りをやめる。</summary>
        public void Detach()
        {
            if (!_attached) return;

            DebugElement.RemoveChangeListener(OnElementChanged);
            _attached = false;
            _menu = null;
            _scope.Clear();
            _lastSeen.Clear();
        }

        /// <summary>
        /// 接続後に追加または削除された行を追跡範囲へ反映する。
        /// 行構造が変わっていなければ走査しないため、毎フレーム呼んでもよい。
        /// </summary>
        public void Refresh() => Refresh(false);

        /// <summary>直前の変更を取り消す。</summary>
        public bool Undo()
        {
            _applying = true;
            try
            {
                return _stack.Undo();
            }
            finally
            {
                _applying = false;
            }
        }

        /// <summary>取り消した変更をやり直す。</summary>
        public bool Redo()
        {
            _applying = true;
            try
            {
                return _stack.Redo();
            }
            finally
            {
                _applying = false;
            }
        }

        /// <summary>控えを全て捨てる。</summary>
        public void Clear() => _stack.Clear();

        /// <summary>受け取りをやめる。</summary>
        public void Dispose() => Detach();

        private void OnElementChanged(DebugElement element)
        {
            // 取り消し・やり直しで起きた変更まで控えると、履歴が無限に増える。
            if (_applying) return;

            Refresh(false);
            if (!_scope.Contains(element)) return;

            if (!DebugValueSnapshot.TryCapture(element, out var current) || !current.HasValue) return;

            if (!_lastSeen.TryGetValue(element, out var previous))
            {
                // 初めて見る行。控えだけ取って、履歴には積まない
                // （何に戻せばよいか分からないため）。
                _lastSeen[element] = current;
                return;
            }

            _lastSeen[element] = current;

            if (previous.Equals(current)) return;

            _stack.Push(new ValueChangeCommand(this, element, previous, current));
        }

        /// <summary>接続中メニューの値行だけをスコープと直前値へ反映する。</summary>
        /// <param name="force">版数が同じでも組み直すなら true。</param>
        private void Refresh(bool force)
        {
            if (_menu == null) return;
            if (!force && _scopeStructureVersion == DebugElement.StructureVersion) return;

            _scope.Clear();
            _menu.VisitAll((_, element) =>
            {
                if (!DebugValueSnapshot.TryCapture(element, out var snapshot) || !snapshot.HasValue) return;

                _scope.Add(element);
                if (!_lastSeen.ContainsKey(element)) _lastSeen[element] = snapshot;
            });

            using var removed = TempList<DebugElement>.Rent();
            foreach (var pair in _lastSeen)
            {
                if (!_scope.Contains(pair.Key)) removed.List.Add(pair.Key);
            }

            for (var i = 0; i < removed.List.Count; i++) _lastSeen.Remove(removed.List[i]);
            _scopeStructureVersion = DebugElement.StructureVersion;
        }

        /// <summary>1 回分の値変更。行の実体を指しているので、戻すと元の場所にも反映される。</summary>
        private sealed class ValueChangeCommand : IUndoableCommand
        {
            private readonly DebugMenuHistory _owner;
            private readonly DebugElement _element;
            private readonly DebugValueSnapshot _before;
            private readonly DebugValueSnapshot _after;

            public ValueChangeCommand(DebugMenuHistory owner, DebugElement element, DebugValueSnapshot before, DebugValueSnapshot after)
            {
                _owner = owner;
                _element = element;
                _before = before;
                _after = after;
            }

            public string Label => _element.Label;

            public void Execute()
            {
                _after.Apply(_element);
                _owner._lastSeen[_element] = _after;
            }

            public void Undo()
            {
                _before.Apply(_element);
                _owner._lastSeen[_element] = _before;
            }
        }
    }
}
