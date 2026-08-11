using System;
using System.Globalization;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// 成分が複数ある数値の行。展開すると成分ごとの行が出る。
    /// <para>
    /// 成分を子行として持つのは、上下限も刻み幅も成分ごとに違うことがあるため。
    /// 親側は表示と一括のリセットだけを受け持つ。
    /// </para>
    /// </summary>
    public sealed class DebugVector : DebugElement
    {
        private static readonly string[] ComponentNames = { "X", "Y", "Z", "W" };

        private readonly Func<Vector4> _getter;
        private readonly Action<Vector4> _setter;
        private readonly Vector4 _defaultValue;
        private readonly int _componentCount;
        private readonly DebugFloat[] _components;

        private Vector4 _stored;

        /// <summary>成分数と読み書きの関数を指定して作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="componentCount">成分の数（2〜4）。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugVector(string label, int componentCount, Func<Vector4> getter, Action<Vector4> setter) : base(label)
        {
            if (componentCount < 2 || componentCount > 4) throw new ArgumentOutOfRangeException(nameof(componentCount));

            _componentCount = componentCount;
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _defaultValue = getter();

            _components = BuildComponents();
        }

        /// <summary>Vector3 を読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugVector Of(string label, Func<Vector3> getter, Action<Vector3> setter)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if (setter == null) throw new ArgumentNullException(nameof(setter));

            return new DebugVector(label, 3, () => getter(), value => setter(value));
        }

        /// <summary>Vector2 を読み書きする行を作る。</summary>
        /// <param name="label">左カラムへ出す表示名。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public static DebugVector Of(string label, Func<Vector2> getter, Action<Vector2> setter)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if (setter == null) throw new ArgumentNullException(nameof(setter));

            return new DebugVector(label, 2, () => getter(), value => setter(value));
        }

        /// <summary>成分の数。</summary>
        public int ComponentCount => _componentCount;

        /// <summary>成分ごとの行。上下限や刻み幅はここへ設定する。</summary>
        public DebugFloat GetComponent(int index) => _components[index];

        /// <summary>現在値。使わない成分は 0 として扱う。</summary>
        public Vector4 Value
        {
            get => _getter != null ? _getter() : _stored;
            set
            {
                if (Value == value) return;

                if (_setter != null) _setter(value);
                else _stored = value;

                NotifyChanged();
            }
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Vector;

        /// <inheritdoc/>
        public override bool IsModified => Value != _defaultValue;

        /// <summary>右カラムには成分を並べて出す。</summary>
        public override string GetValueText()
        {
            var value = Value;
            switch (_componentCount)
            {
                case 2:
                    return string.Format(CultureInfo.InvariantCulture, "{0:F2}, {1:F2}", value.x, value.y);
                case 3:
                    return string.Format(CultureInfo.InvariantCulture, "{0:F2}, {1:F2}, {2:F2}", value.x, value.y, value.z);
                default:
                    return string.Format(CultureInfo.InvariantCulture, "{0:F2}, {1:F2}, {2:F2}, {3:F2}", value.x, value.y, value.z, value.w);
            }
        }

        /// <summary>成分ごとにまとめて既定値へ戻す。</summary>
        public override void ResetToDefault() => Value = _defaultValue;

        /// <summary>まとめて打ち込める。成分を 1 つずつ辿るより速い場面があるため。</summary>
        public override bool CanTypeValue => true;

        /// <summary>行の決定は成分の開閉に使う。値欄のダブルクリックなら一括入力できる。</summary>
        public override bool PrefersDecide => true;

        /// <summary>
        /// 打ち込みには丸めていない値をカンマ区切りで出す。
        /// 保存・復元もこの文字列を経由するので、桁を落とすと値が変質する。
        /// </summary>
        public override string GetEditText()
        {
            var value = Value;
            var parts = new string[_componentCount];
            for (var i = 0; i < _componentCount; i++) parts[i] = value[i].ToString("R", CultureInfo.InvariantCulture);

            return string.Join(", ", parts);
        }

        /// <summary>カンマ区切りの文字列を受け取る。成分数が合わなければ拒否する。</summary>
        /// <param name="text">打ち終えた文字列。</param>
        public override bool CommitEditText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split(',');
            if (parts.Length != _componentCount) return false;

            var next = Value;
            for (var i = 0; i < _componentCount; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var component)) return false;
                if (float.IsNaN(component)) return false;

                next[i] = component;
            }

            Value = next;
            return true;
        }

        private DebugFloat[] BuildComponents()
        {
            var components = new DebugFloat[_componentCount];

            for (var i = 0; i < _componentCount; i++)
            {
                // ローカルへ写さないと、全ての子行が最後の添字を掴む。
                var axis = i;

                var component = new DebugFloat(
                    ComponentNames[axis],
                    () => Value[axis],
                    v =>
                    {
                        var next = Value;
                        next[axis] = v;
                        Value = next;
                    });

                components[axis] = Add(component);
            }

            return components;
        }
    }
}
