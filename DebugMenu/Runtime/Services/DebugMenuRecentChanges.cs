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
        private readonly Dictionary<DebugElement, DebugPage> _candidates = new Dictionary<DebugElement, DebugPage>();
        private readonly List<DebugRecentChange> _entries = new List<DebugRecentChange>();
        private readonly IReadOnlyList<DebugRecentChange> _readOnlyEntries;

        /// <summary>同期中に届いた値通知を、外側の同期完了後に処理する。</summary>
        private readonly Queue<DebugElement> _pendingChanges = new Queue<DebugElement>();

        /// <summary>到達したページごとの所有子行版。</summary>
        private readonly Dictionary<DebugPage, uint> _ownerPageVersions = new Dictionary<DebugPage, uint>();

        /// <summary>保存対象かどうかの確認に失敗し、回復を待っている行。</summary>
        private readonly HashSet<DebugElement> _pendingTracking = new HashSet<DebugElement>();

        /// <summary>変更通知時に対象判定できず、回復時に最近項目へ加える行。</summary>
        private readonly Dictionary<DebugElement, ulong> _pendingChanged = new Dictionary<DebugElement, ulong>();

        /// <summary>最近項目に採用した最終変更順。</summary>
        private readonly Dictionary<DebugElement, ulong> _entrySequences = new Dictionary<DebugElement, ulong>();

        /// <summary>判定不能中の最終変更順を記録する連番。</summary>
        private ulong _pendingChangeSequence;

        /// <summary>現在追跡しているメニュー。動的な行追加を変更通知時に取り込むため保持する。</summary>
        private DebugMenuRoot _menu;

        /// <summary>所有ページを最後に組み直した最上位ページの版数。</summary>
        private uint _ownerPageVersion;

        /// <summary>子行取得に失敗し、同じ構造版でも所有範囲の再走査が必要か。</summary>
        private bool _trackingTraversalFailed;

        /// <summary>所有範囲の同期またはChanged通知を処理中か。</summary>
        private bool _processingChange;

        /// <summary>所有範囲を走査中か。</summary>
        private bool _refreshing;

        /// <summary>処理中に構造または値通知が届き、終了後の再同期が必要か。</summary>
        private bool _refreshRequested;

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
            _menu = menu;
            RefreshOwners(menu);
            RebuildPage();
            RememberOwnershipVersions();

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

            _menu = menu;
            SyncStructure(true, true);
        }

        /// <summary>構造版が変わった場合だけ所有範囲と最近項目を更新する。</summary>
        public void Refresh()
        {
            if (_menu == null) return;

            SyncStructure(true);
        }

        /// <summary>変更通知の受け取りをやめ、借りていた行を外す。</summary>
        public void Detach()
        {
            if (_attached) DebugElement.RemoveChangeListener(OnElementChanged);

            _attached = false;
            _menu = null;
            _owners.Clear();
            _candidates.Clear();
            _pendingTracking.Clear();
            _pendingChanged.Clear();
            _entrySequences.Clear();
            _pendingChangeSequence = 0;
            _pendingChanges.Clear();
            _ownerPageVersions.Clear();
            _entries.Clear();
            _trackingTraversalFailed = false;
            _processingChange = false;
            _refreshing = false;
            _refreshRequested = false;
            RebuildPage();
        }

        /// <summary>追跡は続けたまま、最近変更した一覧だけを空にする。</summary>
        public void Clear()
        {
            if (_entries.Count == 0 && _pendingChanged.Count == 0 && _entrySequences.Count == 0) return;

            _entries.Clear();
            _pendingChanged.Clear();
            _entrySequences.Clear();
            _pendingChanges.Clear();
            RebuildPage();
            RememberOwnershipVersions();
            Changed?.Invoke();
        }

        /// <summary>変更通知を解除し、借りていた行を外す。</summary>
        public void Dispose() => Detach();

        private void RefreshOwners(DebugMenuRoot menu)
        {
            var previousOwners = new Dictionary<DebugElement, DebugPage>(_owners);
            var previousCandidates = new Dictionary<DebugElement, DebugPage>(_candidates);
            _owners.Clear();
            _candidates.Clear();
            _trackingTraversalFailed = false;
            var seenCandidates = new HashSet<DebugElement>();
            var visitedPageVersions = new Dictionary<DebugPage, uint>();
            menu.VisitOwned((page, element) =>
            {
                _candidates[element] = page;
                seenCandidates.Add(element);
                if (!TryGetTrackingState(element, out var shouldTrack))
                {
                    _pendingTracking.Add(element);
                    if (previousOwners.TryGetValue(element, out var previousPage)) _owners[element] = previousPage;
                    return;
                }

                if (shouldTrack) _owners[element] = page;
            }, (element, exception) =>
            {
                _trackingTraversalFailed = true;
                element.ReportReadError("最近項目対象確認", exception);
            }, page => visitedPageVersions[page] = page.Root.OwnedSubtreeVersion);

            if (_trackingTraversalFailed)
            {
                foreach (var pair in previousCandidates)
                {
                    if (!_candidates.ContainsKey(pair.Key)) _candidates.Add(pair.Key, pair.Value);
                }

                foreach (var pair in previousOwners)
                {
                    if (!_owners.ContainsKey(pair.Key)) _owners.Add(pair.Key, pair.Value);
                }
            }

            if (!_trackingTraversalFailed)
            {
                var removed = new List<DebugElement>();
                foreach (var element in _pendingTracking)
                {
                    if (!seenCandidates.Contains(element)) removed.Add(element);
                }

                for (var i = 0; i < removed.Count; i++)
                {
                    _pendingTracking.Remove(removed[i]);
                    _pendingChanged.Remove(removed[i]);
                    _entrySequences.Remove(removed[i]);
                }
            }

            _ownerPageVersions.Clear();
            foreach (var pair in visitedPageVersions) _ownerPageVersions.Add(pair.Key, pair.Value);
            _ownerPageVersion = menu.PageVersion;
        }

        private void OnElementChanged(DebugElement element)
        {
            if (_processingChange || _refreshing)
            {
                _pendingChanges.Enqueue(element);
                _refreshRequested = true;
                return;
            }

            ProcessElementChanged(element);
            DrainPendingChanges();
        }

        /// <summary>再入を除外した状態で1行の変更を最近項目へ反映する。</summary>
        private void ProcessElementChanged(DebugElement element)
        {
            _processingChange = true;
            try
            {
                SyncStructure(true);
                if (!_candidates.TryGetValue(element, out var page)) return;

                if (!TryGetTrackingState(element, out var shouldTrack))
                {
                    _pendingTracking.Add(element);
                    unchecked
                    {
                        _pendingChangeSequence++;
                    }
                    _pendingChanged[element] = _pendingChangeSequence;
                    return;
                }

                _pendingTracking.Remove(element);
                _pendingChanged.Remove(element);
                if (!shouldTrack)
                {
                    _owners.Remove(element);
                    if (RemoveEntry(element)) PublishEntryRemoval();
                    return;
                }

                _owners[element] = page;
                RemoveEntry(element);
                unchecked
                {
                    _pendingChangeSequence++;
                }
                InsertEntryBySequence(page, element, _pendingChangeSequence);

                RebuildPage();
                RememberOwnershipVersions();
                Changed?.Invoke();
            }
            finally
            {
                _processingChange = false;
            }
        }

        /// <summary>構造版または前回の走査失敗に応じ、所有範囲と孤児項目を同期する。</summary>
        private void SyncStructure(bool notify) => SyncStructure(notify, false);

        /// <summary>所有範囲の同期を再入から守って実行する。</summary>
        private void SyncStructure(bool notify, bool force)
        {
            if (_menu == null) return;
            if (_refreshing)
            {
                _refreshRequested = true;
                return;
            }

            _refreshing = true;
            try
            {
                _refreshRequested = false;
                SyncStructureCore(notify, force);
                if (_refreshRequested)
                {
                    _refreshRequested = false;
                    SyncStructureCore(notify, true);
                }

                if (_refreshRequested) _trackingTraversalFailed = true;
            }
            finally
            {
                _refreshing = false;
            }

            DrainPendingChanges();
        }

        /// <summary>所有範囲と最近項目を1回同期する。</summary>
        private void SyncStructureCore(bool notify, bool force)
        {
            if (_menu == null) return;
            if (!force && OwnershipVersionsAreCurrent() &&
                !_trackingTraversalFailed)
            {
                var pendingEntriesChanged = RetryPendingTracking();
                if (!pendingEntriesChanged) return;

                RebuildPage();
                RememberOwnershipVersions();
                if (notify) Changed?.Invoke();
                return;
            }

            RefreshOwners(_menu);
            var entriesChanged = RetryPendingTracking();
            entriesChanged |= RemoveOrphanedEntries();
            entriesChanged |= UpdateEntryPages();
            if (entriesChanged) RebuildPage();
            RememberOwnershipVersions();
            if (entriesChanged && notify) Changed?.Invoke();
        }

        /// <summary>保存対象の確認に失敗していた行を、構造全体を走査せずに再確認する。</summary>
        /// <returns>既存の最近項目を取り除いたならtrue。</returns>
        private bool RetryPendingTracking()
        {
            if (_pendingTracking.Count == 0) return false;

            var resolved = new List<DebugElement>();
            var recoveredChanges = new List<KeyValuePair<DebugElement, ulong>>();
            var entriesChanged = false;
            var ownerChanged = false;
            foreach (var element in _pendingTracking)
            {
                if (!_candidates.TryGetValue(element, out var page))
                {
                    resolved.Add(element);
                    _pendingChanged.Remove(element);
                    entriesChanged |= RemoveEntry(element);
                    continue;
                }

                if (!TryGetTrackingState(element, out var shouldTrack)) continue;

                resolved.Add(element);
                if (shouldTrack)
                {
                    ownerChanged |= !_owners.TryGetValue(element, out var previousPage) || !ReferenceEquals(previousPage, page);
                    _owners[element] = page;
                    if (_pendingChanged.TryGetValue(element, out var sequence))
                        recoveredChanges.Add(new KeyValuePair<DebugElement, ulong>(element, sequence));
                }
                else
                {
                    _owners.Remove(element);
                    _pendingChanged.Remove(element);
                    entriesChanged |= RemoveEntry(element);
                }
            }

            for (var i = 0; i < resolved.Count; i++) _pendingTracking.Remove(resolved[i]);
            recoveredChanges.Sort((left, right) => left.Value.CompareTo(right.Value));
            for (var i = 0; i < recoveredChanges.Count; i++)
            {
                var element = recoveredChanges[i].Key;
                if (!_owners.TryGetValue(element, out var page)) continue;

                _pendingChanged.Remove(element);
                RemoveEntry(element);
                InsertEntryBySequence(page, element, recoveredChanges[i].Value);
                entriesChanged = true;
            }
            if (ownerChanged) entriesChanged |= UpdateEntryPages();
            return entriesChanged;
        }

        /// <summary>行が最近項目の対象かを確認し、独自メタデータの失敗をその行だけへ記録する。</summary>
        private static bool TryGetTrackingState(DebugElement element, out bool shouldTrack)
        {
            try
            {
                shouldTrack = element.IsSaveable && element.ValueKind != DebugValueKind.None;
                element.ClearReadError("最近項目対象確認");
                return true;
            }
            catch (Exception exception)
            {
                shouldTrack = false;
                element.ReportReadError("最近項目対象確認", exception);
                return false;
            }
        }

        /// <summary>指定行を最近項目から取り除く。</summary>
        private bool RemoveEntry(DebugElement element)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_entries[i].Element, element)) continue;

                _entries.RemoveAt(i);
                _entrySequences.Remove(element);
                return true;
            }

            return false;
        }

        /// <summary>項目除去を借用ページへ反映し、変更を通知する。</summary>
        private void PublishEntryRemoval()
        {
            RebuildPage();
            RememberOwnershipVersions();
            Changed?.Invoke();
        }

        /// <summary>所有ページが変わった最近項目のページ情報を更新する。</summary>
        private bool UpdateEntryPages()
        {
            var changed = false;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!_owners.TryGetValue(entry.Element, out var page) || ReferenceEquals(entry.Page, page)) continue;

                _entries[i] = new DebugRecentChange(page, entry.Element);
                changed = true;
            }

            return changed;
        }

        /// <summary>現在の所有子行と最上位ページの版数を同期済みとして記録する。</summary>
        private void RememberOwnershipVersions()
        {
            _ownerPageVersion = _menu?.PageVersion ?? 0u;
        }

        /// <summary>到達済みページの所有子行版が変わっていないか確認する。</summary>
        private bool OwnershipVersionsAreCurrent()
        {
            if (_menu == null || _ownerPageVersion != _menu.PageVersion) return false;

            foreach (var pair in _ownerPageVersions)
            {
                if (pair.Key.Root.OwnedSubtreeVersion != pair.Value) return false;
            }

            return true;
        }

        /// <summary>同期中に届いた変更を、通知順を重複させず処理する。</summary>
        private void DrainPendingChanges()
        {
            if (_refreshing || _processingChange || _pendingChanges.Count == 0) return;

            const int maxChangesPerDrain = 1024;
            var generationCount = _pendingChanges.Count;
            var processed = 0;
            while (_pendingChanges.Count > 0 && processed < generationCount && processed < maxChangesPerDrain)
            {
                ProcessElementChanged(_pendingChanges.Dequeue());
                processed++;
            }

            if (_pendingChanges.Count > 0) _refreshRequested = true;
        }

        /// <summary>追跡先から外れた行を最近一覧から除く。</summary>
        private bool RemoveOrphanedEntries()
        {
            var removed = false;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_owners.ContainsKey(_entries[i].Element)) continue;

                var element = _entries[i].Element;
                _entries.RemoveAt(i);
                _entrySequences.Remove(element);
                removed = true;
            }

            return removed;
        }

        private void RebuildPage()
        {
            _page.Root.ClearBorrowedChildren();
            for (var i = 0; i < _entries.Count; i++) _page.Root.AddBorrowed(_entries[i].Element);
            _page.Invalidate();
        }

        /// <summary>変更連番の降順を保って最近項目へ差し込む。</summary>
        private void InsertEntryBySequence(DebugPage page, DebugElement element, ulong sequence)
        {
            _entrySequences[element] = sequence;
            var insertIndex = 0;
            while (insertIndex < _entries.Count &&
                   _entrySequences.TryGetValue(_entries[insertIndex].Element, out var existingSequence) &&
                   existingSequence > sequence)
            {
                insertIndex++;
            }

            _entries.Insert(insertIndex, new DebugRecentChange(page, element));
            if (_entries.Count <= _capacity) return;

            var evicted = _entries[_entries.Count - 1].Element;
            _entries.RemoveAt(_entries.Count - 1);
            _entrySequences.Remove(evicted);
        }
    }
}
