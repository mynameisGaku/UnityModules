using System;
using System.Collections.Generic;

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
        /// <summary>取り消せる変更。古い順に保持する。</summary>
        private readonly List<ValueChangeCommand> _undo = new List<ValueChangeCommand>();

        /// <summary>やり直せる変更。末尾が次に適用する変更。</summary>
        private readonly List<ValueChangeCommand> _redo = new List<ValueChangeCommand>();

        /// <summary>追跡範囲から外れた行を列挙中の辞書変更なしで取り除くための作業領域。</summary>
        private readonly List<DebugElement> _removed = new List<DebugElement>();

        private readonly Dictionary<DebugElement, DebugValueSnapshot> _lastSeen =
            new Dictionary<DebugElement, DebugValueSnapshot>();

        /// <summary>接続したメニューに属する値行。別メニューから届く全体通知をここで除外する。</summary>
        private readonly HashSet<DebugElement> _scope = new HashSet<DebugElement>();

        /// <summary>借用表示を除き、接続したメニューが所有している行。</summary>
        private readonly HashSet<DebugElement> _candidates = new HashSet<DebugElement>();

        /// <summary>所有範囲を組み直している間も、直前の所有行を通知判定へ使う。</summary>
        private readonly HashSet<DebugElement> _refreshCandidateFallback = new HashSet<DebugElement>();

        /// <summary>所属は確認できたが、取得失敗により直前値をまだ持てていない行。</summary>
        private readonly HashSet<DebugElement> _pendingBaselines = new HashSet<DebugElement>();

        /// <summary>保存対象かどうかの確認に失敗し、回復を待っている行。</summary>
        private readonly HashSet<DebugElement> _pendingTracking = new HashSet<DebugElement>();

        /// <summary>同期中に届いた値通知を、外側の同期完了後に処理する。</summary>
        private readonly Queue<PendingChange> _pendingChanges = new Queue<PendingChange>();

        /// <summary>再入中の同じ行を、最後に届いた位置へ集約するための世代。</summary>
        private readonly Dictionary<DebugElement, ulong> _pendingChangeGenerations = new Dictionary<DebugElement, ulong>();

        private ulong _pendingChangeGeneration;

        /// <summary>履歴消去中の値取得によって変更され、基準値の再取得が必要になった行。</summary>
        private readonly HashSet<DebugElement> _clearDirtyElements = new HashSet<DebugElement>();

        /// <summary>履歴消去中に追跡集合を固定して読むための作業領域。</summary>
        private readonly List<DebugElement> _clearCaptureElements = new List<DebugElement>();

        /// <summary>到達したページごとの所有子行版。</summary>
        private readonly Dictionary<DebugPage, uint> _scopePageVersions = new Dictionary<DebugPage, uint>();

        /// <summary>現在接続しているメニュー。</summary>
        private DebugMenuRoot _menu;

        /// <summary>スコープを最後に組み直した最上位ページの版数。</summary>
        private uint _scopePageVersion;

        /// <summary>子行取得に失敗し、同じ構造版でも所有範囲の再走査が必要か。</summary>
        private bool _trackingTraversalFailed;

        private bool _attached;

        /// <summary>取り消しと、やり直しの最中か。控えを取らないための印。</summary>
        private bool _applying;

        /// <summary>所有範囲の同期または値通知を処理中か。</summary>
        private bool _processingChange;

        /// <summary>所有範囲を走査中か。</summary>
        private bool _refreshing;

        /// <summary>履歴を消去し、現在値を新しい基準として取得中か。</summary>
        private bool _clearing;

        /// <summary>処理中に構造または値通知が届き、終了後の再同期が必要か。</summary>
        private bool _refreshRequested;

        /// <summary>保持する履歴の上限を指定して作る。</summary>
        /// <param name="capacity">保持する操作の数。</param>
        public DebugMenuHistory(int capacity = 128)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        /// <summary>保持する履歴の上限。</summary>
        public int Capacity { get; }

        /// <summary>取り消せるか。</summary>
        public bool CanUndo
        {
            get
            {
                Refresh(false);
                return _undo.Count > 0;
            }
        }

        /// <summary>やり直せるか。</summary>
        public bool CanRedo
        {
            get
            {
                Refresh(false);
                return _redo.Count > 0;
            }
        }

        /// <summary>控えている操作の数。</summary>
        public int Count
        {
            get
            {
                Refresh(false);
                return _undo.Count;
            }
        }

        /// <summary>次に取り消される操作の表示名。無ければ null。</summary>
        public string NextUndoLabel
        {
            get
            {
                Refresh(false);
                return _undo.Count > 0 ? _undo[_undo.Count - 1].Label : null;
            }
        }

        /// <summary>次にやり直される操作の表示名。無ければ null。</summary>
        public string NextRedoLabel
        {
            get
            {
                Refresh(false);
                return _redo.Count > 0 ? _redo[_redo.Count - 1].Label : null;
            }
        }

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
            ClearCommandsAndPending();
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
            _candidates.Clear();
            _refreshCandidateFallback.Clear();
            _lastSeen.Clear();
            _pendingBaselines.Clear();
            _pendingTracking.Clear();
            _pendingChanges.Clear();
            _pendingChangeGenerations.Clear();
            _pendingChangeGeneration = 0;
            _clearDirtyElements.Clear();
            _clearCaptureElements.Clear();
            _scopePageVersions.Clear();
            _trackingTraversalFailed = false;
            _processingChange = false;
            _refreshing = false;
            _clearing = false;
            _refreshRequested = false;
        }

        /// <summary>
        /// 接続後に追加または削除された行を追跡範囲へ反映する。
        /// 行構造が変わっていなければ走査しないため、毎フレーム呼んでもよい。
        /// </summary>
        public void Refresh() => Refresh(false);

        /// <summary>直前の変更を取り消す。</summary>
        public bool Undo()
        {
            Refresh(false);
            if (_undo.Count == 0) return false;

            var command = _undo[_undo.Count - 1];
            _applying = true;
            try
            {
                if (!command.Undo()) return false;
            }
            finally
            {
                _applying = false;
            }

            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(command);
            return true;
        }

        /// <summary>取り消した変更をやり直す。</summary>
        public bool Redo()
        {
            Refresh(false);
            if (_redo.Count == 0) return false;

            var command = _redo[_redo.Count - 1];
            _applying = true;
            try
            {
                if (!command.Execute()) return false;
            }
            finally
            {
                _applying = false;
            }

            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(command);
            return true;
        }

        /// <summary>
        /// 控えと保留中の変更を全て捨て、完了時点の値を次の変更の基準にする。
        /// 値取得中に別行が変わった場合も、その行を再取得して消去前の値へ戻らないようにする。
        /// </summary>
        public void Clear()
        {
            ClearCommandsAndPending();
            if (_menu == null) return;
            if (_clearing)
            {
                _refreshRequested = true;
                return;
            }

            var wasApplying = _applying;
            _applying = true;
            _clearing = true;
            try
            {
                _refreshRequested = false;
                RefreshCore(true);
                CaptureClearBaselines(true);

                const int maxReseedPasses = 8;
                var pass = 0;
                while (pass < maxReseedPasses &&
                       (_clearDirtyElements.Count > 0 ||
                        _refreshRequested ||
                        _trackingTraversalFailed ||
                        !OwnershipVersionsAreCurrent()))
                {
                    pass++;
                    if (_refreshRequested || _trackingTraversalFailed || !OwnershipVersionsAreCurrent())
                    {
                        _refreshRequested = false;
                        RefreshCore(true);
                    }

                    CaptureClearBaselines(false);
                }

                // 値取得そのものが値を変え続ける行には安定した基準が存在しない。
                // 古い値を残さず、次に正常取得できた値を基準にする。
                if (_clearDirtyElements.Count > 0)
                {
                    _clearCaptureElements.Clear();
                    foreach (var element in _clearDirtyElements) _clearCaptureElements.Add(element);
                    for (var i = 0; i < _clearCaptureElements.Count; i++)
                    {
                        var element = _clearCaptureElements[i];
                        if (!_scope.Contains(element)) continue;

                        _lastSeen.Remove(element);
                        _pendingBaselines.Add(element);
                    }
                }
            }
            finally
            {
                _clearDirtyElements.Clear();
                _clearCaptureElements.Clear();
                _clearing = false;
                _applying = wasApplying;
                ClearCommandsAndPending();
            }
        }

        /// <summary>履歴消去時の追跡行を固定して読み、現在値を新しい基準にする。</summary>
        /// <param name="captureAll">全追跡行を読むなら true。false なら取得中に変わった行だけを読む。</param>
        private void CaptureClearBaselines(bool captureAll)
        {
            _clearCaptureElements.Clear();
            if (captureAll)
            {
                foreach (var element in _scope) _clearCaptureElements.Add(element);
            }
            else
            {
                foreach (var element in _clearDirtyElements) _clearCaptureElements.Add(element);
            }

            for (var i = 0; i < _clearCaptureElements.Count; i++)
            {
                var element = _clearCaptureElements[i];
                _clearDirtyElements.Remove(element);
                if (!_scope.Contains(element)) continue;

                if (DebugValueSnapshot.TryCapture(element, out var snapshot) && snapshot.HasValue)
                {
                    _lastSeen[element] = snapshot;
                    _pendingBaselines.Remove(element);
                }
                else
                {
                    _lastSeen.Remove(element);
                    _pendingBaselines.Add(element);
                }
            }
        }

        /// <summary>値を再取得せず、履歴枝と保留通知だけを破棄する。</summary>
        private void ClearCommandsAndPending()
        {
            _undo.Clear();
            _redo.Clear();
            _pendingChanges.Clear();
            _pendingChangeGenerations.Clear();
        }

        /// <summary>受け取りをやめる。</summary>
        public void Dispose() => Detach();

        private void OnElementChanged(DebugElement element)
        {
            if (_clearing)
            {
                _clearDirtyElements.Add(element);
                return;
            }

            // 取り消し・やり直しで起きた変更まで控えると、履歴が無限に増える。
            if (_applying) return;
            if (_processingChange || _refreshing)
            {
                QueuePendingChange(element);
                _refreshRequested = true;
                return;
            }

            ProcessElementChanged(element);
            DrainPendingChanges();
        }

        /// <summary>再入を除外した状態で1行の変更を履歴へ反映する。</summary>
        private void ProcessElementChanged(DebugElement element)
        {
            _processingChange = true;
            try
            {
                Refresh(false);
                if (!_candidates.Contains(element)) return;

                if (!TryGetTrackingState(element, out var shouldTrack))
                {
                    _pendingTracking.Add(element);
                    MarkBaselinePending(element);
                    return;
                }

                _pendingTracking.Remove(element);
                if (!shouldTrack)
                {
                    StopTracking(element);
                    return;
                }

                _scope.Add(element);

                if (!DebugValueSnapshot.TryCapture(element, out var current) || !current.HasValue)
                {
                    MarkBaselineUnknown(element);
                    return;
                }

                if (!_lastSeen.TryGetValue(element, out var previous))
                {
                    // 初めて見る行。控えだけ取って、履歴には積まない
                    // （何に戻せばよいか分からないため）。
                    _lastSeen[element] = current;
                    _pendingBaselines.Remove(element);
                    return;
                }

                _lastSeen[element] = current;
                _pendingBaselines.Remove(element);

                if (previous.Equals(current)) return;

                _undo.Add(new ValueChangeCommand(this, element, previous, current));
                _redo.Clear();
                if (_undo.Count > Capacity) _undo.RemoveAt(0);
            }
            finally
            {
                _processingChange = false;
            }
        }

        /// <summary>通知時点の追跡判定と値を、通知順を保ったまま控える。</summary>
        private void QueuePendingChange(DebugElement element)
        {
            if (!_candidates.Contains(element) && !_refreshCandidateFallback.Contains(element)) return;
            unchecked
            {
                _pendingChangeGeneration++;
            }

            _pendingChangeGenerations[element] = _pendingChangeGeneration;
            _pendingChanges.Enqueue(new PendingChange(element, _pendingChangeGeneration));
        }

        /// <summary>再入期間の同一行を最後の発生位置へ集約して履歴へ反映する。</summary>
        private void ProcessPendingChange(PendingChange pending)
        {
            if (!_pendingChangeGenerations.TryGetValue(pending.Element, out var latest) || latest != pending.Generation) return;

            _pendingChangeGenerations.Remove(pending.Element);
            ProcessElementChanged(pending.Element);
        }

        /// <summary>接続中メニューの値行だけをスコープと直前値へ反映する。</summary>
        /// <param name="force">版数が同じでも組み直すなら true。</param>
        private void Refresh(bool force)
        {
            if (_menu == null) return;
            if (_clearing)
            {
                _refreshRequested = true;
                return;
            }

            if (_refreshing)
            {
                _refreshRequested = true;
                return;
            }

            _refreshing = true;
            var rerun = false;
            try
            {
                _refreshRequested = false;
                RefreshCore(force);
                rerun = _refreshRequested;
                _refreshRequested = false;
            }
            finally
            {
                _refreshing = false;
            }

            DrainPendingChanges();
            if (!rerun || _menu == null) return;

            _refreshing = true;
            try
            {
                RefreshCore(true);
                if (_refreshRequested) _trackingTraversalFailed = true;
            }
            finally
            {
                _refreshing = false;
            }

            DrainPendingChanges();
        }

        /// <summary>接続中メニューの所有範囲を1回同期する。</summary>
        private void RefreshCore(bool force)
        {
            if (_menu == null) return;
            if (!force &&
                OwnershipVersionsAreCurrent() &&
                !_trackingTraversalFailed)
            {
                RetryPendingTracking();
                RetryPendingBaselines();
                return;
            }

            var previousScope = new HashSet<DebugElement>(_scope);
            var previousCandidates = new HashSet<DebugElement>(_candidates);
            _refreshCandidateFallback.Clear();
            _refreshCandidateFallback.UnionWith(previousCandidates);
            _scope.Clear();
            _candidates.Clear();
            _trackingTraversalFailed = false;
            var seenCandidates = new HashSet<DebugElement>();
            var visitedPageVersions = new Dictionary<DebugPage, uint>();
            _menu.VisitOwned((_, element) =>
            {
                _candidates.Add(element);
                seenCandidates.Add(element);
                if (!TryGetTrackingState(element, out var shouldTrack))
                {
                    _pendingTracking.Add(element);
                    if (previousScope.Contains(element)) _scope.Add(element);
                    return;
                }

                if (!shouldTrack) return;

                _scope.Add(element);
                if (_lastSeen.ContainsKey(element))
                {
                    _pendingBaselines.Remove(element);
                    return;
                }

                if (DebugValueSnapshot.TryCapture(element, out var snapshot) && snapshot.HasValue)
                {
                    _lastSeen[element] = snapshot;
                    _pendingBaselines.Remove(element);
                    return;
                }

                _pendingBaselines.Add(element);
            }, (element, exception) =>
            {
                _trackingTraversalFailed = true;
                element.ReportReadError("履歴対象確認", exception);
            }, page => visitedPageVersions[page] = page.Root.OwnedSubtreeVersion);

            if (_trackingTraversalFailed)
            {
                _candidates.UnionWith(previousCandidates);
                _scope.UnionWith(previousScope);
            }

            if (!_trackingTraversalFailed)
            {
                _removed.Clear();
                foreach (var element in _pendingTracking)
                {
                    if (!seenCandidates.Contains(element)) _removed.Add(element);
                }

                for (var i = 0; i < _removed.Count; i++) _pendingTracking.Remove(_removed[i]);
            }

            _removed.Clear();
            foreach (var pair in _lastSeen)
            {
                if (!_scope.Contains(pair.Key)) _removed.Add(pair.Key);
            }

            for (var i = 0; i < _removed.Count; i++) _lastSeen.Remove(_removed[i]);

            _removed.Clear();
            foreach (var element in _pendingBaselines)
            {
                if (!_scope.Contains(element)) _removed.Add(element);
            }

            for (var i = 0; i < _removed.Count; i++) _pendingBaselines.Remove(_removed[i]);
            PruneCommandsOutsideScope(_undo);
            PruneCommandsOutsideScope(_redo);
            _scopePageVersions.Clear();
            foreach (var pair in visitedPageVersions) _scopePageVersions.Add(pair.Key, pair.Value);
            _scopePageVersion = _menu.PageVersion;
            _refreshCandidateFallback.Clear();
        }

        /// <summary>到達済みページの所有子行版が変わっていないか確認する。</summary>
        private bool OwnershipVersionsAreCurrent()
        {
            if (_menu == null || _scopePageVersion != _menu.PageVersion) return false;

            foreach (var pair in _scopePageVersions)
            {
                if (pair.Key.Root.OwnedSubtreeVersion != pair.Value) return false;
            }

            return true;
        }

        /// <summary>同期中に届いた変更を、通知順を重複させず処理する。</summary>
        private void DrainPendingChanges()
        {
            if (_refreshing || _processingChange || _applying || _pendingChanges.Count == 0) return;

            const int maxChangesPerDrain = 1024;
            var generationCount = _pendingChanges.Count;
            var processed = 0;
            while (_pendingChanges.Count > 0 && processed < generationCount && processed < maxChangesPerDrain)
            {
                ProcessPendingChange(_pendingChanges.Dequeue());
                processed++;
            }

            if (_pendingChanges.Count > 0) _refreshRequested = true;
        }

        /// <summary>保存対象の確認に失敗していた行を、構造全体を走査せずに再確認する。</summary>
        private void RetryPendingTracking()
        {
            if (_pendingTracking.Count == 0) return;

            _removed.Clear();
            foreach (var element in _pendingTracking)
            {
                if (!_candidates.Contains(element))
                {
                    _removed.Add(element);
                    continue;
                }

                if (!TryGetTrackingState(element, out var shouldTrack)) continue;

                _removed.Add(element);
                if (!shouldTrack)
                {
                    StopTracking(element);
                    continue;
                }

                _scope.Add(element);
                if (_lastSeen.ContainsKey(element)) continue;

                if (DebugValueSnapshot.TryCapture(element, out var snapshot) && snapshot.HasValue)
                {
                    _lastSeen[element] = snapshot;
                    _pendingBaselines.Remove(element);
                }
                else
                {
                    _pendingBaselines.Add(element);
                }
            }

            for (var i = 0; i < _removed.Count; i++) _pendingTracking.Remove(_removed[i]);
        }

        /// <summary>構造を走査せず、初期値を読めなかった行だけを再取得する。</summary>
        private void RetryPendingBaselines()
        {
            if (_pendingBaselines.Count == 0) return;

            _removed.Clear();
            foreach (var element in _pendingBaselines)
            {
                if (!DebugValueSnapshot.TryCapture(element, out var snapshot) || !snapshot.HasValue) continue;

                _lastSeen[element] = snapshot;
                _removed.Add(element);
            }

            for (var i = 0; i < _removed.Count; i++) _pendingBaselines.Remove(_removed[i]);
        }

        /// <summary>行が履歴対象かを確認し、独自メタデータの失敗をその行だけへ記録する。</summary>
        private static bool TryGetTrackingState(DebugElement element, out bool shouldTrack)
        {
            try
            {
                shouldTrack = element.IsSaveable && element.ValueKind != DebugValueKind.None;
                element.ClearReadError("履歴対象確認");
                return true;
            }
            catch (Exception exception)
            {
                shouldTrack = false;
                element.ReportReadError("履歴対象確認", exception);
                return false;
            }
        }

        /// <summary>現在値が不明になった行の古い基準値と履歴を捨て、回復値の取得を待つ。</summary>
        private void MarkBaselineUnknown(DebugElement element)
        {
            _lastSeen.Remove(element);
            _pendingBaselines.Add(element);
        }

        /// <summary>追跡判定だけが不明な行は、過去の履歴を残して次の正常値を新しい基準にする。</summary>
        private void MarkBaselinePending(DebugElement element)
        {
            _lastSeen.Remove(element);
            _pendingBaselines.Add(element);
        }

        /// <summary>追跡対象外になった行の状態と履歴を取り除く。</summary>
        private void StopTracking(DebugElement element)
        {
            _scope.Remove(element);
            _lastSeen.Remove(element);
            _pendingBaselines.Remove(element);
            RemoveCommandsFor(_undo, element);
            RemoveCommandsFor(_redo, element);
        }

        /// <summary>現在の所有範囲から外れた行の操作を、指定した履歴枝から取り除く。</summary>
        private void PruneCommandsOutsideScope(List<ValueChangeCommand> commands)
        {
            for (var i = commands.Count - 1; i >= 0; i--)
            {
                if (!_scope.Contains(commands[i].Element)) commands.RemoveAt(i);
            }
        }

        /// <summary>指定行に属する操作を履歴枝から取り除く。</summary>
        private static void RemoveCommandsFor(List<ValueChangeCommand> commands, DebugElement element)
        {
            for (var i = commands.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(commands[i].Element, element)) commands.RemoveAt(i);
            }
        }

        /// <summary>再入中に届いた変更の通知時点情報。</summary>
        private sealed class PendingChange
        {
            public PendingChange(DebugElement element, ulong generation)
            {
                Element = element;
                Generation = generation;
            }

            public DebugElement Element { get; }
            public ulong Generation { get; }
        }

        /// <summary>1 回分の値変更。行の実体を指しているので、戻すと元の場所にも反映される。</summary>
        private sealed class ValueChangeCommand
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

            public DebugElement Element => _element;

            public bool Execute()
            {
                if (!_after.Apply(_element)) return false;

                _owner._lastSeen[_element] = _after;
                _owner._pendingBaselines.Remove(_element);
                return true;
            }

            public bool Undo()
            {
                if (!_before.Apply(_element)) return false;

                _owner._lastSeen[_element] = _before;
                _owner._pendingBaselines.Remove(_element);
                return true;
            }
        }
    }
}
