using System;
using System.Collections.Generic;

namespace DebugMenu
{
    /// <summary>
    /// 値が変わった行を新しい順に集め、同じ実体を借りた一覧ページを保つ。
    /// <para>
    /// 同じ行を続けて変更しても 1 件だけ残り、先頭へ移動する。
    /// 借り物なので一覧側で操作した値は元ページへそのまま反映される。
    /// </para>
    /// </summary>
    public sealed class DebugMenuRecentChanges : IDisposable
    {
        private readonly DebugPage _page;
        private readonly int _capacity;
        private readonly Dictionary<DebugElement, DebugPage> _owners = new Dictionary<DebugElement, DebugPage>();
        private readonly List<DebugRecentChange> _entries = new List<DebugRecentChange>();
        private readonly IReadOnlyList<DebugRecentChange> _readOnlyEntries;

        private bool _attached;

        /// <summary>保持件数とページ名を指定して作る。</summary>
        /// <param name="capacity">保持する行数。1 以上。</param>
        /// <param name="pageName">借用表示ページの名前。</param>
        public DebugMenuRecentChanges(int capacity = 16, string pageName = "Recent")
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));

            _capacity = capacity;
            _readOnlyEntries = _entries.AsReadOnly();
            _page = new DebugPage(pageName)
            {
                Description = "最近変更した行を新しい順に表示する。",
            };
        }

        /// <summary>最近変更された行を借りて表示するページ。</summary>
        public DebugPage Page => _page;

        /// <summary>最近変更された行。新しいものが先頭。</summary>
        public IReadOnlyList<DebugRecentChange> Entries => _readOnlyEntries;

        /// <summary>保持している行数。</summary>
        public int Count => _entries.Count;

        /// <summary>保持できる最大件数。</summary>
        public int Capacity => _capacity;

        /// <summary>変更通知を受け取っているか。</summary>
        public bool IsAttached => _attached;

        /// <summary>一覧が変わったときに呼ばれる。</summary>
        public event Action Changed;

        /// <summary>対象メニューの変更通知を受け取り始める。</summary>
        /// <param name="menu">追跡対象のメニュー。</param>
        public void Attach(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (_attached) Detach();

            _entries.Clear();
            RefreshOwners(menu);
            RebuildPage();

            DebugElement.AddChangeListener(OnElementChanged);
            _attached = true;
        }

        /// <summary>
        /// 動的に追加・削除された行を追跡対象へ反映する。
        /// 既に保持している行のうち、メニューから外れたものは一覧から除く。
        /// </summary>
        /// <param name="menu">追跡対象のメニュー。</param>
        public void Refresh(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            RefreshOwners(menu);

            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (!_owners.ContainsKey(_entries[i].Element)) _entries.RemoveAt(i);
            }

            RebuildPage();
            Changed?.Invoke();
        }

        /// <summary>変更通知の受け取りをやめ、借りていた行を外す。</summary>
        public void Detach()
        {
            if (_attached) DebugElement.RemoveChangeListener(OnElementChanged);

            _attached = false;
            _owners.Clear();
            _entries.Clear();
            RebuildPage();
        }

        /// <summary>追跡は続けたまま、最近変更した一覧だけを空にする。</summary>
        public void Clear()
        {
            if (_entries.Count == 0) return;

            _entries.Clear();
            RebuildPage();
            Changed?.Invoke();
        }

        /// <summary>変更通知を解除し、借りていた行を外す。</summary>
        public void Dispose() => Detach();

        private void RefreshOwners(DebugMenuRoot menu)
        {
            _owners.Clear();
            menu.VisitAll((page, element) =>
            {
                if (ReferenceEquals(page, _page) || _owners.ContainsKey(element)) return;
                if (!DebugValueSnapshot.TryCapture(element, out var snapshot) || !snapshot.HasValue) return;
                _owners.Add(element, page);
            });
        }

        private void OnElementChanged(DebugElement element)
        {
            if (!_owners.TryGetValue(element, out var page)) return;

            for (var i = 0; i < _entries.Count; i++)
            {
                if (!ReferenceEquals(_entries[i].Element, element)) continue;
                _entries.RemoveAt(i);
                break;
            }

            _entries.Insert(0, new DebugRecentChange(page, element));
            if (_entries.Count > _capacity) _entries.RemoveAt(_entries.Count - 1);

            RebuildPage();
            Changed?.Invoke();
        }

        private void RebuildPage()
        {
            _page.Root.ClearBorrowedChildren();
            for (var i = 0; i < _entries.Count; i++) _page.Root.AddBorrowed(_entries[i].Element);
            _page.Invalidate();
        }
    }
}
