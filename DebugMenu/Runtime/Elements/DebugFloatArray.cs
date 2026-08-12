using System;
using System.Collections.Generic;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 小数リストを展開し、各 index を既存の <see cref="DebugFloat"/> で編集する行。
    /// リストの長さが外側で変わっても、古い子行から範囲外へ書き込まない。
    /// </summary>
    public sealed class DebugFloatArray : DebugElement
    {
        private readonly IList<float> _values;

        /// <summary>編集するリストを指定して作る。</summary>
        /// <param name="label">表示名。</param>
        /// <param name="values">直接編集する小数リスト。</param>
        public DebugFloatArray(string label, IList<float> values) : base(label)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
            IsExpanded = false;
            SyncChildren();
        }

        /// <summary>編集対象のリスト。</summary>
        public IList<float> Values => _values;

        /// <summary>各要素へ設定する下限。</summary>
        public float Min { get; private set; } = float.NegativeInfinity;

        /// <summary>各要素へ設定する上限。</summary>
        public float Max { get; private set; } = float.PositiveInfinity;

        /// <summary>各要素の左右操作 1 回あたりの変化量。</summary>
        public float Step { get; private set; } = 0.1f;

        /// <summary>各要素を表示する小数点以下の桁数。</summary>
        public int Digits { get; private set; } = 2;

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
        public DebugFloatArray WithRange(float min, float max)
        {
            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);
            ConfigureChildren();
            return this;
        }

        /// <summary>各要素の刻み幅を設定する。</summary>
        /// <param name="step">左右操作 1 回あたりの変化量。</param>
        public DebugFloatArray WithStep(float step)
        {
            Step = Mathf.Abs(step);
            ConfigureChildren();
            return this;
        }

        /// <summary>各要素の表示桁数を設定する。</summary>
        /// <param name="digits">小数点以下の桁数。</param>
        public DebugFloatArray WithDigits(int digits)
        {
            Digits = Mathf.Clamp(digits, 0, 9);
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
                var child = DebugFloat.CreateChecked($"[{index}]", () => GetValue(index), value => SetValue(index, value));
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
            for (var i = 0; i < children.Count; i++) Configure((DebugFloat)children[i]);
        }

        private void Configure(DebugFloat child)
        {
            child.Min = Min;
            child.Max = Max;
            child.Step = Step;
            child.Digits = Digits;
            if (child.TryGetFloat(out var value)) child.TrySetFloat(value);
        }

        private float GetValue(int index) => index >= 0 && index < _values.Count ? _values[index] : 0f;

        private bool SetValue(int index, float value)
        {
            if (index < 0 || index >= _values.Count) return false;
            _values[index] = value;
            return true;
        }
    }
}
