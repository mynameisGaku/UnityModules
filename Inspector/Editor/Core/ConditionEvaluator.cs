using System;
using System.Collections.Generic;

namespace Inspector.Editor
{
    /// <summary>メンバーを今この瞬間どう扱うか。</summary>
    public readonly struct MemberState
    {
        public MemberState(bool visible, bool enabled)
        {
            Visible = visible;
            Enabled = enabled;
        }

        /// <summary>Inspector に出すか。</summary>
        public bool Visible { get; }

        /// <summary>編集できるか。<c>false</c> なら灰色で表示だけする。</summary>
        public bool Enabled { get; }

        public static MemberState Default => new MemberState(true, true);
    }

    /// <summary>
    /// <see cref="ConditionAttribute"/> と、条件に類する属性をまとめて解く。
    /// <para>
    /// 判定に失敗した条件は <b>「成立した」ものとして扱い</b>、代わりに理由を <c>errors</c> に積む。
    /// 名前を打ち間違えたときにフィールドが黙って消えると、
    /// 「そもそも消えていることに気付けない」状態になり原因追跡が難しい。
    /// 出したうえで赤字を添えるほうが早く直せる。
    /// </para>
    /// </summary>
    public static class ConditionEvaluator
    {
        /// <summary>
        /// メンバーに付いた条件系の属性を全て適用した結果を返す。
        /// </summary>
        /// <param name="target">条件の参照先になるオブジェクト。</param>
        /// <param name="member">対象のメンバー。</param>
        /// <param name="isPlaying">再生中か。<see cref="ShowInPlayModeAttribute"/> の判定に使う。</param>
        /// <param name="errors">設定ミスの説明を積む先。<c>null</c> 可。</param>
        public static MemberState Resolve(object target, InspectorMember member, bool isPlaying, List<string> errors)
        {
            if (member == null) return MemberState.Default;

            var visible = true;
            var enabled = true;

            var attributes = member.Attributes;
            for (var i = 0; i < attributes.Length; i++)
            {
                switch (attributes[i])
                {
                    case ConditionAttribute condition:
                    {
                        var met = Evaluate(target, condition, member.Name, errors);

                        switch (condition.Effect)
                        {
                            case ConditionEffect.Show: visible &= met; break;
                            case ConditionEffect.Hide: visible &= !met; break;
                            case ConditionEffect.Enable: enabled &= met; break;
                            case ConditionEffect.Disable: enabled &= !met; break;
                        }

                        break;
                    }

                    case ReadOnlyAttribute _:
                        enabled = false;
                        break;

                    case ShowInPlayModeAttribute _:
                        visible &= isPlaying;
                        break;

                    case HideInPlayModeAttribute _:
                        visible &= !isPlaying;
                        break;
                }
            }

            return new MemberState(visible, enabled);
        }

        /// <summary>
        /// 条件 1 つを解いて、成立しているかを返す。
        /// <para>
        /// 効果（表示するのか隠すのか）はここでは見ない。呼び出し側で解釈する。
        /// </para>
        /// </summary>
        /// <param name="target">条件の参照先になるオブジェクト。</param>
        /// <param name="condition">解く条件。</param>
        /// <param name="ownerName">エラー文言に出す、条件が付いているメンバーの名前。</param>
        /// <param name="errors">設定ミスの説明を積む先。<c>null</c> 可。</param>
        public static bool Evaluate(object target, ConditionAttribute condition, string ownerName, List<string> errors)
        {
            if (condition == null) return true;

            var members = condition.Members;
            if (members.Length == 0)
            {
                Report(errors, ownerName, "条件に見るべきメンバーが指定されていない。");
                return true;
            }

            // 値との比較は単一メンバーに対してのみ意味を持つ。
            if (condition.Values.Length > 0)
            {
                return EvaluateComparison(target, condition, ownerName, errors);
            }

            var all = condition.Operator == ConditionOperator.And;

            for (var i = 0; i < members.Length; i++)
            {
                var met = EvaluateFlag(target, members[i], ownerName, errors);

                if (all)
                {
                    if (!met) return false;
                }
                else if (met)
                {
                    return true;
                }
            }

            return all;
        }

        private static bool EvaluateComparison(object target, ConditionAttribute condition, string ownerName, List<string> errors)
        {
            var name = condition.Members[0];
            var negate = StartsWithNegation(ref name);

            if (!MemberResolver.TryGetValue(target, name, out var value, out var error))
            {
                Report(errors, ownerName, error);
                return true;
            }

            // よくある書き間違い。[ShowIf(nameof(_a), nameof(_b))] と書くと
            // 「_a の値が文字列 "_b" と等しいか」になってしまい、いつまでも成立しない。
            if (value is bool && AllStrings(condition.Values))
            {
                Report(errors, ownerName,
                    $"'{name}' は bool なのに文字列と比較している。" +
                    " 複数のメンバーを条件にするなら [ShowIf(ConditionOperator.And, ...)] のように演算子を先頭に置く。");
                return true;
            }

            var values = condition.Values;
            for (var i = 0; i < values.Length; i++)
            {
                if (!AreEqual(value, values[i])) continue;

                return !negate;
            }

            return negate;
        }

        /// <summary>
        /// bool のメンバー 1 つを読む。先頭に <c>!</c> が付いていれば反転する。
        /// 読めなかった場合は <c>true</c> を返し、理由を <paramref name="errors"/> に積む。
        /// </summary>
        public static bool EvaluateFlag(object target, string rawName, string ownerName, List<string> errors)
        {
            var name = rawName;
            var negate = StartsWithNegation(ref name);

            if (!MemberResolver.TryGetValue(target, name, out var value, out var error))
            {
                Report(errors, ownerName, error);
                return true;
            }

            if (!(value is bool flag))
            {
                Report(errors, ownerName,
                    $"'{name}' が bool ではない（{(value == null ? "null" : value.GetType().Name)}）。" +
                    " 値を比べたいなら [ShowIf(nameof(...), 比べる値)] の形で書く。");
                return true;
            }

            return negate ? !flag : flag;
        }

        /// <summary>先頭の <c>!</c> を取り除き、反転が指定されていたかを返す。</summary>
        private static bool StartsWithNegation(ref string name)
        {
            if (string.IsNullOrEmpty(name) || name[0] != '!') return false;

            name = name.Substring(1).Trim();
            return true;
        }

        private static bool AllStrings(object[] values)
        {
            if (values.Length == 0) return false;

            for (var i = 0; i < values.Length; i++)
            {
                if (!(values[i] is string)) return false;
            }

            return true;
        }

        /// <summary>
        /// 属性に書ける値は限られた定数だけなので、そのままの型では一致しないことがある。
        /// enum を <c>int</c> で書いた、<c>float</c> のフィールドを <c>1</c> と比べた、といった場合を数値として揃えて比べる。
        /// </summary>
        public static bool AreEqual(object left, object right)
        {
            if (left == null || right == null) return ReferenceEquals(left, right);
            if (left.Equals(right)) return true;

            if (TryToDouble(left, out var leftNumber) && TryToDouble(right, out var rightNumber))
            {
                return leftNumber.Equals(rightNumber);
            }

            return false;
        }

        private static bool TryToDouble(object value, out double number)
        {
            number = 0d;

            switch (value)
            {
                case bool _:
                case string _:
                    return false;

                case Enum enumValue:
                    number = Convert.ToDouble(Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType())));
                    return true;
            }

            if (!(value is IConvertible)) return false;

            try
            {
                number = Convert.ToDouble(value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Report(List<string> errors, string ownerName, string message)
        {
            if (errors == null || message == null) return;

            var text = string.IsNullOrEmpty(ownerName) ? message : $"{ownerName}: {message}";
            if (errors.Contains(text)) return;

            errors.Add(text);
        }
    }
}
