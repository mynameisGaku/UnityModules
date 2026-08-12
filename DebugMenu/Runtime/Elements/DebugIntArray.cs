using System;
using System.Collections.Generic;

namespace DebugMenu
{
    /// <summary>
    /// 整数リストを展開し、各 index を既存の <see cref="DebugInt"/> で編集する行。
    /// リストの長さが外側で変わっても、古い子行から範囲外へ書き込まない。
    /// </summary>
    public sealed class DebugIntArray : DebugElement
    {
        private readonly IList<int> _values;

        /// <summary>編集するリストを指定して作る。</summary>
        /// <param name="label">表示名。</param>
        /// <param name="values">直接編集する整数リスト。</param>
        public DebugIntArray(string label, IList<int> values) : base(label)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
            IsExpanded = false;
            SyncChildren();
        }

        /// <summary>編集対象のリスト。</summary>
        public IList<int> Values => _values;

        /// <summary>各要素へ設定する下限。</summary>
        public int Min { get; private set; } = int.MinValue;

        /// <summary>各要素へ設定する上限。</summary>
        public int Max { get; private set; } = int.MaxValue;

        /// <summary>各要素の左右操作 1 回あたりの変化量。</summary>
        public int Step { get; private set; } = 1;

        /// <summary>外側のリスト長に合わせて子行が増減したときに呼ばれる。</summary>
        public event Action StructureChanged;

        /// <summary>親行は値を保存しない。各 index の子行だけを保存する。</summary>
        public override bool IsSaveable => false;

        /// <summary>現在のリスト長に合う子行を返す。</summary>
        public override IReadOnlyList<DebugElement> Children
        {
            get
            {
                SyncChildren();
                return base.Children;
            }
        }

        /// <inheritdoc/>
        public override bool IsModified
        {
            get
            {
                SyncChildren();
                var children = base.Children;
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i].IsModified) return true;
                }

                return false;
            }
        }

        /// <summary>各要素の上下限を設定する。</summary>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        public DebugIntArray WithRange(int min, int max)
        {
            Min = Math.Min(min, max);
            Max = Math.Max(min, max);
            ConfigureChildren();
            return this;
        }

        /// <summary>各要素の刻み幅を設定する。</summary>
        /// <param name="step">左右操作 1 回あたりの変化量。</param>
        public DebugIntArray WithStep(int step)
        {
            Step = Math.Max(1, step);
            ConfigureChildren();
            return this;
        }

        /// <summary>外側で変わったリスト長を子行へ反映する。</summary>
        /// <returns>子行を増減したなら true。</returns>
        public bool Refresh() => SyncChildren();

        /// <inheritdoc/>
        public override string GetValueText() => _values.Count.ToString();

        /// <inheritdoc/>
        public override void OnDecide()
        {
            SyncChildren();
            base.OnDecide();
        }

        /// <inheritdoc/>
        public override void Tick(float deltaSeconds) => SyncChildren();

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            SyncChildren();
            var children = base.Children;
            var failed = 0;
            for (var i = 0; i < children.Count; i++)
            {
                if (!children[i].TryResetToDefaultSafely()) failed++;
            }

            if (failed > 0)
            {
                ReportReadError("値設定", new InvalidOperationException($"{failed} 件の配列要素を既定値へ戻せなかった。"));
            }
        }

        private bool SyncChildren()
        {
            var children = base.Children;
            var changed = false;

            while (children.Count > _values.Count)
            {
                Remove(children[children.Count - 1]);
                changed = true;
            }

            while (children.Count < _values.Count)
            {
                var index = children.Count;
                var child = DebugInt.CreateChecked($"[{index}]", () => GetValue(index), value => SetValue(index, value));
                Configure(child);
                Add(child);
                changed = true;
            }

            if (changed) StructureChanged?.Invoke();
            return changed;
        }

        private void ConfigureChildren()
        {
            SyncChildren();
            var children = base.Children;
            for (var i = 0; i < children.Count; i++) Configure((DebugInt)children[i]);
        }

        private void Configure(DebugInt child)
        {
            child.Min = Min;
            child.Max = Max;
            child.Step = Step;
            if (child.TryGetInt(out var value)) child.TrySetInt(value);
        }

        private int GetValue(int index) => index >= 0 && index < _values.Count ? _values[index] : 0;

        private bool SetValue(int index, int value)
        {
            if (index < 0 || index >= _values.Count) return false;
            _values[index] = value;
            return true;
        }
    }
}
