using System;
using Containers;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// デバッグメニュー 1 行分の基底。
    /// <para>
    /// ラベル・説明・子行・展開状態・色・単位は全ての型で共通なのでここに集約し、
    /// 型ごとの差分は <see cref="GetValueText"/> / <see cref="OnDecide"/> /
    /// <see cref="OnAdjust"/> の 3 つに閉じてある。
    /// </para>
    /// <para>
    /// <b>行は自分を描かない。</b>描画は <see cref="DebugPage"/> が可視行を平坦化してから
    /// 一括で行う。こうしておくと、行のロジックは UI を一切知らずに済み、
    /// 描画バックエンドを差し替えてもテストがそのまま通る。
    /// </para>
    /// </summary>
    public class DebugElement
    {
        private const float ReadErrorLogIntervalSeconds = 5f;

        private readonly FastList<DebugElement> _children = new FastList<DebugElement>();

        private Func<string> _labelProvider;
        private string _readErrorOperation = string.Empty;
        private string _readErrorMessage = string.Empty;
        private float _nextReadErrorLogTime;
        private float _warnMin;
        private float _warnMax;
        private bool _hasWarnRange;
        private bool _isExpanded;

        /// <summary>どこかの行の値が変わると増える版数。全体を走査せず変更を検知するために使う。</summary>
        private static uint _valueVersion;

        /// <summary>どこかの行のピン留めが変わると増える版数。</summary>
        private static uint _favoriteVersion;

        /// <summary>どこかの行構造または展開状態が変わると増える版数。</summary>
        private static uint _structureVersion;

        /// <summary>互換用の単一変更受け取り先。<see cref="SetChangeListener"/> で差し替える。</summary>
        private static Action<DebugElement> _changeListener;

        /// <summary>複数のサービスが並行して受け取るための変更受け取り先。</summary>
        private static Action<DebugElement> _changeSubscribers;

        /// <summary>表示名と副題を指定して作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="subTitle">右カラムへ出す文字列。値を持つ行では現在値に置き換わる。</param>
        public DebugElement(string label, string subTitle = null)
        {
            Label = label ?? string.Empty;
            SubTitle = subTitle ?? string.Empty;
        }

        // ── 表示 ────────────────────────────────────────────────────────────

        /// <summary>左カラムへ出す表示名。保存キーの自動生成にも使う。</summary>
        public string Label { get; set; }

        /// <summary>右カラムへ出す副題。値を持つ行では現在値に置き換わるため表示されない。</summary>
        public string SubTitle { get; set; }

        /// <summary>値へ添える単位。表示にだけ足し、打ち込みの中身には混ぜない。</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>カーソルが乗っている間に画面下へ出す説明文。空ならページ側の説明が出る。</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 保存・復元に使う絶対キー。空ならメニュー内の位置から自動生成する。
        /// <para>
        /// 明示すると、行を別ページへ移しても表示名を変えても受け取り側のコードが変わらない。
        /// </para>
        /// </summary>
        public string SaveKey { get; set; } = string.Empty;

        /// <summary>
        /// 実際に描く表示名。<see cref="SetLabelProvider"/> が差してあればそちらが優先される。
        /// </summary>
        public string DisplayLabel => _labelProvider != null ? _labelProvider() : Label;

        /// <summary>表示用の取得処理が失敗しているか。失敗した行だけをエラー表示へ切り替えるために使う。</summary>
        public bool HasReadError => !string.IsNullOrEmpty(_readErrorOperation);

        /// <summary>行へ出す短いエラー表示。例外本文はログへ出し、メニューの幅を圧迫しない。</summary>
        public string ReadErrorText => HasReadError ? "ERROR: " + _readErrorOperation : string.Empty;

        /// <summary>最後に失敗した取得処理の例外メッセージ。独自Viewで詳細を出す場合に使う。</summary>
        public string ReadErrorMessage => _readErrorMessage;

        /// <summary>
        /// 表示名を毎フレーム作る関数を差す。監視値をそのまま名前に出したいとき
        /// （<c>HP 120/200</c> のような表示）に使う。null を渡すと解除。
        /// </summary>
        /// <param name="provider">表示名を返す関数。</param>
        public void SetLabelProvider(Func<string> provider) => _labelProvider = provider;

        /// <summary>
        /// 構築時に利用側の取得関数から既定値を読む。失敗時は代替値で行の構築を続け、
        /// 後続行とControllerの初期化を止めない。
        /// </summary>
        /// <typeparam name="T">取得する値の型。</typeparam>
        /// <param name="getter">利用側の取得関数。</param>
        /// <param name="fallback">取得できなかった場合の代替値。</param>
        /// <returns>取得値または代替値。</returns>
        protected T ReadInitialValueOrDefault<T>(Func<T> getter, T fallback = default)
        {
            return TryReadExternalValue(getter, out var value) ? value : fallback;
        }

        /// <summary>利用側の取得関数を安全に呼ぶ。構築時の範囲設定など、View以外の読取にも使う。</summary>
        /// <typeparam name="T">取得する値の型。</typeparam>
        /// <param name="getter">利用側の取得関数。</param>
        /// <param name="value">取得値。失敗時は既定値。</param>
        /// <returns>取得できたなら true。</returns>
        protected bool TryReadExternalValue<T>(Func<T> getter, out T value)
        {
            try
            {
                value = getter();
                ClearReadError("値取得");
                return true;
            }
            catch (Exception exception)
            {
                value = default;
                ReportReadError("値取得", exception);
                return false;
            }
        }

        /// <summary>
        /// 動的な表示名を例外境界の内側で読む。失敗時は静的な <see cref="Label"/> を返し、
        /// 他の行の更新を止めない。
        /// </summary>
        /// <param name="label">表示する名前。失敗時は静的な名前。</param>
        /// <returns>動的な表示名を正常に取得できたか。</returns>
        public bool TryGetDisplayLabel(out string label)
        {
            try
            {
                label = DisplayLabel ?? string.Empty;
                ClearReadError("ラベル取得");
                return true;
            }
            catch (Exception exception)
            {
                label = Label ?? string.Empty;
                ReportReadError("ラベル取得", exception);
                return false;
            }
        }

        /// <summary>
        /// 右カラムの表示値を例外境界の内側で読む。失敗時は明確なエラー文字列を返す。
        /// </summary>
        /// <param name="valueText">表示する値。失敗時はエラー表示。</param>
        /// <returns>値を正常に取得できたか。</returns>
        public bool TryGetDisplayValueText(out string valueText)
        {
            try
            {
                valueText = GetValueText() ?? string.Empty;
                ClearReadError("値取得");
                return true;
            }
            catch (Exception exception)
            {
                ReportReadError("値取得", exception);
                valueText = ReadErrorText;
                return false;
            }
        }

        /// <summary>入力欄の初期文字列を例外境界の内側で読む。失敗時は入力を開始しない。</summary>
        /// <param name="editText">入力欄へ入れる文字列。失敗時は空。</param>
        /// <returns>初期文字列を取得できたなら true。</returns>
        public bool TryGetEditText(out string editText)
        {
            try
            {
                editText = GetEditText() ?? string.Empty;
                ClearReadError("値取得");
                return true;
            }
            catch (Exception exception)
            {
                editText = string.Empty;
                ReportReadError("値取得", exception);
                return false;
            }
        }

        // ── 状態 ────────────────────────────────────────────────────────────

        /// <summary>子行を表示中か。</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;

                _isExpanded = value;
                NotifyStructureChanged();
            }
        }

        /// <summary>決定キーでの開閉を許可するか。false でも <see cref="IsExpanded"/> は効く。</summary>
        public bool IsExpandable { get; set; } = true;

        /// <summary>展開マーカーの表示方針。</summary>
        public DebugMarkerVisibility MarkerVisibility { get; set; } = DebugMarkerVisibility.Auto;

        /// <summary>ピン留めされているか。</summary>
        public bool IsFavorite { get; private set; }

        /// <summary>ラベルの色。null なら選択状態に応じた既定色。</summary>
        public Color? TextColor { get; set; }

        /// <summary>右カラムの色。null ならラベルと同じ扱い。</summary>
        public Color? ValueColor { get; set; }

        /// <summary>この行を直接叩くキー。どのページを開いていても効く。</summary>
        public KeyCode Shortcut { get; set; } = KeyCode.None;

        /// <summary>値が変わったときに呼ばれる。</summary>
        public event Action Changed;

        // ── 子行 ────────────────────────────────────────────────────────────

        /// <summary>子行。実体を別の場所から借りる派生はここを差し替える。</summary>
        public virtual FastList<DebugElement> Children => _children;

        /// <summary>子行を 1 つでも持つか。</summary>
        public bool HasChildren => Children.Count > 0;

        /// <summary>子行を末尾へ足す。足した行をそのまま返すので、続けて設定できる。</summary>
        /// <param name="child">足す行。</param>
        public T Add<T>(T child) where T : DebugElement
        {
            if (child == null) throw new ArgumentNullException(nameof(child));

            _children.Add(child);
            child.Parent = this;
            NotifyStructureChanged();
            return child;
        }

        /// <summary>子行を外す。</summary>
        /// <param name="child">外す行。</param>
        public bool Remove(DebugElement child)
        {
            if (child == null || !_children.Remove(child)) return false;

            child.Parent = null;
            NotifyStructureChanged();
            return true;
        }

        /// <summary>子行を全て外す。</summary>
        public void ClearChildren()
        {
            if (_children.Count == 0) return;

            for (var i = 0; i < _children.Count; i++) _children[i].Parent = null;
            _children.Clear();
            NotifyStructureChanged();
        }

        /// <summary>
        /// 所有権を移さずに子として並べる。
        /// <para>
        /// 同じ行を 2 箇所に出したい場合（お気に入りページなど）に使う。
        /// <see cref="Add{T}"/> を使うと <see cref="Parent"/> が奪われ、
        /// 元の場所での保存キー解決と削除が壊れる。
        /// </para>
        /// <para>
        /// 借りた子は <see cref="ClearBorrowedChildren"/> で外すこと。
        /// <see cref="ClearChildren"/> は所有している前提で <see cref="Parent"/> を消してしまう。
        /// </para>
        /// </summary>
        /// <param name="child">並べる行。所有はしない。</param>
        public T AddBorrowed<T>(T child) where T : DebugElement
        {
            if (child == null) throw new ArgumentNullException(nameof(child));

            _children.Add(child);
            NotifyStructureChanged();
            return child;
        }

        /// <summary>借りている子を外す。<see cref="Parent"/> には触らない。</summary>
        public void ClearBorrowedChildren()
        {
            if (_children.Count == 0) return;

            _children.Clear();
            NotifyStructureChanged();
        }

        /// <summary>この行を子に持つ行。保存キーの自動生成でたどる。</summary>
        public DebugElement Parent { get; private set; }

        // ── 型ごとの差分 ────────────────────────────────────────────────────

        /// <summary>右カラムへ出す文字列。既定は副題。値を持つ派生は現在値を返す。</summary>
        public virtual string GetValueText() => SubTitle;

        /// <summary>決定キーで呼ばれる。既定は開閉のトグル。</summary>
        public virtual void OnDecide()
        {
            if (IsExpandable && HasChildren) IsExpanded = !IsExpanded;
        }

        /// <summary>左右キーで呼ばれる。既定は何もしない。</summary>
        /// <param name="delta">左で -1、右で +1。</param>
        public virtual void OnAdjust(int delta) { }

        /// <summary>左右キーで値が変わる行か。描画側が矢印を出すかの判定に使う。</summary>
        public virtual bool IsAdjustable => false;

        /// <summary>この行が持つ値の種類。</summary>
        public virtual DebugValueKind ValueKind => DebugValueKind.None;

        /// <summary>保存の対象にする行か。親の状態を映しているだけの行は false を返す。</summary>
        public virtual bool IsSaveable => true;

        /// <summary>全体検索の索引へ載せる行か。検索UI自身のような補助行は false を返す。</summary>
        public virtual bool IsSearchable => true;

        /// <summary>構築時の値から変えられているか。「どこを触ったか」の表示に使う。</summary>
        public virtual bool IsModified => false;

        /// <summary>値を構築時の状態へ戻す。値を持たない行は何もしない。</summary>
        public virtual void ResetToDefault() { }

        /// <summary>画面に出ている行だけ毎フレーム呼ばれる。グラフが標本を溜めるのに使う。</summary>
        /// <param name="deltaSeconds">前フレームからの経過秒。</param>
        public virtual void Tick(float deltaSeconds) { }

        /// <summary>
        /// 行のフレーム更新を例外境界の内側で実行する。監視元が一時的に壊れても、
        /// 後続行の監視とメニュー操作を継続する。
        /// </summary>
        /// <param name="deltaSeconds">前フレームからの経過秒。</param>
        /// <returns>更新が正常に完了したか。</returns>
        public bool TryTick(float deltaSeconds)
        {
            try
            {
                Tick(deltaSeconds);
                ClearReadError("更新");
                return true;
            }
            catch (Exception exception)
            {
                ReportReadError("更新", exception);
                return false;
            }
        }

        /// <summary>下限を 0、上限を 1 とした現在位置。スライダー表示に使う。</summary>
        /// <param name="ratio">書き込み先。</param>
        public virtual bool TryGetRatio(out float ratio)
        {
            ratio = 0f;
            return false;
        }

        /// <summary>位置を指定して値を書く。スライダーのドラッグ用。</summary>
        /// <param name="ratio">下限を 0、上限を 1 とした位置。範囲外は端へ丸める。</param>
        public virtual bool TrySetRatio(float ratio) => false;

        /// <summary>選択位置と候補数。描画側の「n/m」表示に使う。</summary>
        /// <param name="index">選択位置（0 起点）。</param>
        /// <param name="count">候補の総数。</param>
        public virtual bool TryGetSelection(out int index, out int count)
        {
            index = 0;
            count = 0;
            return false;
        }

        /// <summary>値を直接打ち込める行か。true なら決定で入力欄が開く。</summary>
        public virtual bool CanTypeValue => false;

        /// <summary>
        /// 打ち込めるが、決定キーでは <see cref="OnDecide"/> を優先したいか。
        /// パスの行のように、決定で一覧を開く方が自然な行が使う。
        /// </summary>
        public virtual bool PrefersDecide => false;

        /// <summary>打ち込みを始めるときの初期文字列。現在値を入れておくと打ち直しが楽になる。</summary>
        public virtual string GetEditText() => string.Empty;

        /// <summary>打ち終えた文字列を値へ反映する。解釈できなければ false（呼び出し側が元の値を保つ）。</summary>
        /// <param name="text">打ち終えた文字列。</param>
        public virtual bool CommitEditText(string text) => false;

        // ── 値の出し入れ（保存・復元とゲーム側からの書き込みに使う） ─────────

        /// <summary>整数として読む。Int と Enum が応じる（Enum は選択位置）。</summary>
        /// <param name="value">書き込み先。</param>
        public virtual bool TryGetInt(out int value)
        {
            value = 0;
            return false;
        }

        /// <summary>小数として読む。</summary>
        /// <param name="value">書き込み先。</param>
        public virtual bool TryGetFloat(out float value)
        {
            value = 0f;
            return false;
        }

        /// <summary>真偽値として読む。</summary>
        /// <param name="value">書き込み先。</param>
        public virtual bool TryGetBool(out bool value)
        {
            value = false;
            return false;
        }

        /// <summary>整数として書く。各行の制約は通常の代入と同じように働く。</summary>
        /// <param name="value">設定する値。</param>
        public virtual bool TrySetInt(int value) => false;

        /// <summary>小数として書く。</summary>
        /// <param name="value">設定する値。</param>
        public virtual bool TrySetFloat(float value) => false;

        /// <summary>真偽値として書く。</summary>
        /// <param name="value">設定する値。</param>
        public virtual bool TrySetBool(bool value) => false;

        // ── 注意色 ──────────────────────────────────────────────────────────

        /// <summary>
        /// この範囲の外に出たら注意色で描く、という範囲を設定する。
        /// フレーム予算の超過や体力が負になったことを目で拾うために使う。
        /// </summary>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        public void SetWarnRange(float min, float max)
        {
            _warnMin = Mathf.Min(min, max);
            _warnMax = Mathf.Max(min, max);
            _hasWarnRange = true;
        }

        /// <summary>注意色の範囲を解除する。</summary>
        public void ClearWarnRange() => _hasWarnRange = false;

        /// <summary>いまの値が注意すべき範囲に入っているか。範囲未設定なら常に false。</summary>
        public bool IsValueWarned
        {
            get
            {
                if (!_hasWarnRange) return false;

                if (!TryGetFloat(out var value))
                {
                    if (HasReadError) return false;
                    if (!TryGetInt(out var intValue)) return false;
                    value = intValue;
                }

                return value < _warnMin || value > _warnMax;
            }
        }

        // ── ピン留め ────────────────────────────────────────────────────────

        /// <summary>
        /// ピン留めを設定する。留めた行はお気に入りのページへ集まるので、
        /// 項目が数百になってもよく使うものだけを 1 ページから触れる。
        /// </summary>
        /// <param name="favorite">留めるなら true。</param>
        public void SetFavorite(bool favorite)
        {
            if (IsFavorite == favorite) return;

            IsFavorite = favorite;
            _favoriteVersion++;
        }

        /// <summary>ピン留めの版数。どこかで留め外しがあると増える。</summary>
        public static uint FavoriteVersion => _favoriteVersion;

        /// <summary>値の版数。どこかで値が変わると増える。</summary>
        public static uint ValueVersion => _valueVersion;

        /// <summary>行構造の版数。どこかで子行または展開状態が変わると増える。</summary>
        public static uint StructureVersion => _structureVersion;

        /// <summary>
        /// 値が変わった行そのものを受け取る互換用の係を差す。差せるのは 1 つだけで、
        /// 上書きすると前の係は外れる。
        /// </summary>
        /// <param name="listener">受け取る係。null で解除。</param>
        public static void SetChangeListener(Action<DebugElement> listener) => _changeListener = listener;

        /// <summary>サービス用の変更受け取り先を追加する。</summary>
        internal static void AddChangeListener(Action<DebugElement> listener) => _changeSubscribers += listener;

        /// <summary>追加済みのサービス用受け取り先だけを外す。</summary>
        internal static void RemoveChangeListener(Action<DebugElement> listener) => _changeSubscribers -= listener;

        /// <summary>展開マーカーを出すべきか。</summary>
        public bool ShouldShowMarker => MarkerVisibility switch
        {
            DebugMarkerVisibility.Always => true,
            DebugMarkerVisibility.Never => false,
            _ => HasChildren,
        };

        /// <summary>
        /// 値が変わったことを知らせる。値を持つ派生が書き換えた直後に呼ぶ。
        /// 版数も併せて進めるので、外からは全体を走査せずに変更を検知できる。
        /// </summary>
        protected void NotifyChanged()
        {
            _valueVersion++;
            Changed?.Invoke();
            _changeListener?.Invoke(this);
            _changeSubscribers?.Invoke(this);
        }

        /// <summary>
        /// 表示用の取得処理で起きた例外をこの行へ記録する。
        /// 例外の文言が変化しても、同じ行からのログは一定時間に1回だけ出す。
        /// </summary>
        /// <param name="operation">失敗した取得処理。</param>
        /// <param name="exception">利用側の処理から出た例外。</param>
        internal void ReportReadError(string operation, Exception exception)
        {
            operation = string.IsNullOrEmpty(operation) ? "取得" : operation;
            var message = exception?.Message ?? "不明な例外";
            _readErrorOperation = operation;
            _readErrorMessage = message;

            var now = Time.realtimeSinceStartup;
            if (now < _nextReadErrorLogTime) return;

            _nextReadErrorLogTime = now + ReadErrorLogIntervalSeconds;
            var details = exception?.ToString() ?? message;
            Debug.LogWarning($"[DebugMenu] 行 '{ResolveSaveKey()}' の{operation}に失敗した。行をエラー表示にして続行する。\n{details}");
        }

        /// <summary>同じ取得処理が回復したときだけ、その処理のエラー表示を解除する。</summary>
        /// <param name="operation">正常に完了した取得処理。</param>
        internal void ClearReadError(string operation)
        {
            if (!string.Equals(_readErrorOperation, operation, StringComparison.Ordinal)) return;

            _readErrorOperation = string.Empty;
            _readErrorMessage = string.Empty;
        }

        /// <summary>行構造または展開状態の変更を全ページへ知らせる。</summary>
        private static void NotifyStructureChanged()
        {
            unchecked
            {
                _structureVersion++;
            }
        }

        /// <summary>
        /// 保存に使うキーを決める。<see cref="SaveKey"/> が空なら、
        /// 親をたどった経路と表示名から組み立てる。
        /// </summary>
        public string ResolveSaveKey()
        {
            if (!string.IsNullOrEmpty(SaveKey)) return SaveKey;

            using var parts = TempList<string>.Rent();
            for (var node = this; node != null; node = node.Parent) parts.List.Add(node.Label);

            parts.List.Reverse();
            return string.Join("/", parts.List.ToArray());
        }
    }
}
