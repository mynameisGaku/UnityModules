using System;
using System.Collections.Generic;

namespace DebugMenu
{
    /// <summary>
    /// 決められた候補から 1 つ選ぶ行。左右キーで送り、決定で候補一覧を開く。
    /// <para>
    /// enum 型に縛らず候補の配列として扱うのは、難易度やステージ名のように
    /// enum ではないものも同じ見た目で選ばせたいため。
    /// <see cref="OfEnum{TEnum}(string,Func{TEnum},Action{TEnum})"/> を使えば
    /// enum からは自動で候補が組み上がる。
    /// </para>
    /// </summary>
    public sealed class DebugEnum : DebugElement
    {
        private readonly string[] _options;
        private readonly Func<int> _getter;
        private readonly Action<int> _setter;
        private readonly int _defaultIndex;

        private int _stored;

        /// <summary>候補と読み書きの関数を指定して作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="options">候補の表示名。</param>
        /// <param name="getter">現在の選択位置を返す関数。</param>
        /// <param name="setter">選択位置を書き込む関数。</param>
        public DebugEnum(string label, string[] options, Func<int> getter, Action<int> setter) : base(label)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.Length == 0) throw new ArgumentException("候補が 1 つも無い。", nameof(options));

            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _defaultIndex = Wrap(getter());
            AddOptions();
        }

        /// <summary>候補を指定し、この行が選択位置を抱える形で作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="options">候補の表示名。</param>
        /// <param name="initialIndex">初期の選択位置。</param>
        public DebugEnum(string label, string[] options, int initialIndex = 0) : base(label)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.Length == 0) throw new ArgumentException("候補が 1 つも無い。", nameof(options));

            _stored = Wrap(initialIndex);
            _defaultIndex = _stored;
            AddOptions();
        }

        /// <summary>
        /// enum 型から候補を組み立てて作る。
        /// <para>
        /// 選択位置は宣言順であって enum の値ではない。飛び値の enum でも
        /// 候補の並びと 1 対 1 に対応させるため。
        /// </para>
        /// </summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugEnum OfEnum<TEnum>(string label, Func<TEnum> getter, Action<TEnum> setter)
            where TEnum : struct, Enum
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if (setter == null) throw new ArgumentNullException(nameof(setter));

            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            var names = Enum.GetNames(typeof(TEnum));

            return new DebugEnum(
                label,
                names,
                () => Array.IndexOf(values, getter()),
                index => setter(values[index]));
        }

        /// <summary>候補の表示名。</summary>
        public IReadOnlyList<string> Options => _options;

        /// <summary>現在の選択位置。範囲外は端へ丸められる。</summary>
        public int Index
        {
            get => Wrap(_getter != null ? _getter() : _stored);
            set
            {
                var wrapped = Wrap(value);
                if (Index == wrapped) return;

                if (_setter != null) _setter(wrapped);
                else _stored = wrapped;

                NotifyChanged();
            }
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Enum;

        /// <inheritdoc/>
        public override bool IsAdjustable => true;

        /// <inheritdoc/>
        public override bool IsModified => Index != _defaultIndex;

        /// <inheritdoc/>
        public override string GetValueText() => _options[Index];

        /// <summary>左右キーで候補を送る。端では折り返す（候補は環状に扱う方が速い）。</summary>
        /// <param name="delta">左で -1、右で +1。</param>
        public override void OnAdjust(int delta)
        {
            var next = (Index + delta) % _options.Length;
            Index = next < 0 ? next + _options.Length : next;
        }

        /// <summary>決定で候補一覧を開閉する。</summary>
        public override void OnDecide() => base.OnDecide();

        /// <inheritdoc/>
        public override void ResetToDefault() => Index = _defaultIndex;

        /// <inheritdoc/>
        public override bool TryGetSelection(out int index, out int count)
        {
            index = Index;
            count = _options.Length;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetInt(out int value)
        {
            value = Index;
            return true;
        }

        /// <inheritdoc/>
        public override bool TrySetInt(int value)
        {
            Index = value;
            return true;
        }

        /// <summary>親の値を映す候補行を構築する。</summary>
        private void AddOptions()
        {
            for (var i = 0; i < _options.Length; i++) Add(new DebugEnumOption(this, i, _options[i]));
        }

        private int Wrap(int index) => index < 0 ? 0 : index >= _options.Length ? _options.Length - 1 : index;
    }

    /// <summary>候補一覧の 1 行。値は親が持つため、保存対象にはしない。</summary>
    internal sealed class DebugEnumOption : DebugElement
    {
        private readonly DebugEnum _owner;
        private readonly int _index;

        /// <summary>親と候補位置を指定して作る。</summary>
        /// <param name="owner">選択値を持つ親。</param>
        /// <param name="index">この行が表す候補位置。</param>
        /// <param name="label">候補の表示名。</param>
        public DebugEnumOption(DebugEnum owner, int index, string label) : base(label)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _index = index;
            IsExpandable = false;
            MarkerVisibility = DebugMarkerVisibility.Never;
        }

        /// <inheritdoc/>
        public override bool IsSaveable => false;

        /// <inheritdoc/>
        public override string GetValueText() => _owner.Index == _index ? "Selected" : string.Empty;

        /// <summary>この候補を親へ設定し、一覧を閉じる。</summary>
        public override void OnDecide()
        {
            _owner.Index = _index;
            _owner.IsExpanded = false;
        }
    }
}
