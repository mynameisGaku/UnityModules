using System;
using System.Collections.Generic;

namespace Inspector.Editor
{
    /// <summary>メンバーを今この瞬間どう扱うか。</summary>
    internal readonly struct MemberState
    {
        internal MemberState(bool visible, bool enabled, bool mixed = false)
        {
            Visible = visible;
            Enabled = enabled;
            Mixed = mixed;
        }

        /// <summary>Inspector に出すか。</summary>
        internal bool Visible { get; }

        /// <summary>編集できるか。<c>false</c> なら灰色で表示だけする。</summary>
        internal bool Enabled { get; }

        /// <summary>複数選択した対象の間で、表示または編集条件が一致していないか。</summary>
        internal bool Mixed { get; }

        internal static MemberState Default => new MemberState(true, true);
    }

    /// <summary>
    /// <see cref="ConditionAttribute"/> と、条件に類する属性をまとめて解く。
    /// <para>
    /// 判定に失敗した条件は <b>その効果を適用せず</b>、代わりに理由を <c>errors</c> に積む。
    /// 名前を打ち間違えたときにフィールドが黙って消えると、
    /// 「そもそも消えていることに気付けない」状態になり原因追跡が難しい。
    /// 出したうえで赤字を添えるほうが早く直せる。
    /// </para>
    /// </summary>
    internal static class ConditionEvaluator
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
        /// 複数選択した全対象の条件を解き、安全にまとめた状態を返す。
        /// <para>
        /// 1 件でも表示対象なら欄は残す。結果が混ざる場合は、条件外の対象まで一括変更しないよう編集を止める。
        /// 全件が非表示の場合だけ欄を隠す。
        /// </para>
        /// </summary>
        /// <param name="targets">条件の参照先。選択対象と同じ順で並べる。</param>
        /// <param name="member">判定するメンバー。</param>
        /// <param name="isPlaying">再生中か。</param>
        /// <param name="errors">設定ミスと混在状態の説明を積む先。</param>
        internal static MemberState ResolveAll(
            IReadOnlyList<object> targets,
            InspectorMember member,
            bool isPlaying,
            List<string> errors)
        {
            if (targets == null || targets.Count == 0) return Resolve((object)null, member, isPlaying, errors);

            var anyVisible = false;
            var allVisible = true;
            var firstEnabled = false;
            var enabledMixed = false;
            var unresolved = false;

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var state = Resolve(target, member, isPlaying, errors);

                if (target == null)
                {
                    // 読めない対象を条件不成立として隠すと、設定ミスそのものを見つけられない。
                    state = new MemberState(true, false);
                    unresolved = true;
                }

                anyVisible |= state.Visible;
                allVisible &= state.Visible;

                if (i == 0) firstEnabled = state.Enabled;
                else enabledMixed |= firstEnabled != state.Enabled;
            }

            if (!anyVisible) return new MemberState(false, false);

            var mixed = unresolved || !allVisible || enabledMixed;
            return new MemberState(true, !mixed && firstEnabled, mixed);
        }

        /// <summary>
        /// 条件 1 つを解いて、成立しているかを返す。
        /// <para>
        /// 設定ミスの場合は、その効果を適用しても表示と編集状態を変えない安全な値を返す。
        /// </para>
        /// </summary>
        /// <param name="target">条件の参照先になるオブジェクト。</param>
        /// <param name="condition">解く条件。</param>
        /// <param name="ownerName">エラー文言に出す、条件が付いているメンバーの名前。</param>
        /// <param name="errors">設定ミスの説明を積む先。<c>null</c> 可。</param>
        public static bool Evaluate(object target, ConditionAttribute condition, string ownerName, List<string> errors)
        {
            if (TryEvaluate(target, condition, ownerName, errors, out var met)) return met;
            if (condition == null) return true;

            return condition.Effect == ConditionEffect.Show || condition.Effect == ConditionEffect.Enable;
        }

        /// <summary>
        /// 条件を解き、設定が正しい場合だけ <paramref name="met"/> に成立状態を返す。
        /// </summary>
        private static bool TryEvaluate(object target, ConditionAttribute condition, string ownerName, List<string> errors, out bool met)
        {
            met = true;
            if (condition == null) return true;

            var members = condition.Members;
            if (members.Length == 0)
            {
                Report(errors, ownerName, "条件に見るべきメンバーが指定されていない。");
                return false;
            }

            // 値との比較は単一メンバーに対してのみ意味を持つ。
            if (condition.Values.Length > 0)
            {
                return TryEvaluateComparison(target, condition, ownerName, errors, out met);
            }

            var all = condition.Operator == ConditionOperator.And;
            var valid = true;
            met = all;

            for (var i = 0; i < members.Length; i++)
            {
                if (!TryEvaluateFlag(target, members[i], ownerName, errors, out var memberMet))
                {
                    valid = false;
                    continue;
                }

                if (all)
                {
                    met &= memberMet;
                }
                else
                {
                    met |= memberMet;
                }
            }

            return valid;
        }

        private static bool TryEvaluateComparison(object target, ConditionAttribute condition, string ownerName, List<string> errors, out bool met)
        {
            met = true;
            var name = condition.Members[0];
            var negate = StartsWithNegation(ref name);

            if (!MemberResolver.TryGetValue(target, name, out var value, out var error))
            {
                Report(errors, ownerName, error);
                return false;
            }

            // よくある書き間違い。[ShowIf(nameof(_a), nameof(_b))] と書くと
            // 「_a の値が文字列 "_b" と等しいか」になってしまい、いつまでも成立しない。
            if (value is bool && AllStrings(condition.Values))
            {
                Report(errors, ownerName,
                    $"'{name}' は bool なのに文字列と比較している。" +
                    " 複数のメンバーを条件にするなら [ShowIf(ConditionOperator.And, ...)] のように演算子を先頭に置く。");
                return false;
            }

            var values = condition.Values;
            for (var i = 0; i < values.Length; i++)
            {
                if (!AreEqual(value, values[i])) continue;

                met = !negate;
                return true;
            }

            met = negate;
            return true;
        }

        /// <summary>
        /// bool のメンバー 1 つを読む。先頭に <c>!</c> が付いていれば反転する。
        /// 読めなかった場合は <c>true</c> を返し、理由を <paramref name="errors"/> に積む。
        /// </summary>
        public static bool EvaluateFlag(object target, string rawName, string ownerName, List<string> errors)
        {
            return !TryEvaluateFlag(target, rawName, ownerName, errors, out var met) || met;
        }

        /// <summary>
        /// bool のメンバーを読み、設定が正しい場合だけ <paramref name="met"/> に成立状態を返す。
        /// </summary>
        private static bool TryEvaluateFlag(object target, string rawName, string ownerName, List<string> errors, out bool met)
        {
            met = true;
            var name = rawName;
            var negate = StartsWithNegation(ref name);

            if (!MemberResolver.TryGetValue(target, name, out var value, out var error))
            {
                Report(errors, ownerName, error);
                return false;
            }

            if (!(value is bool flag))
            {
                Report(errors, ownerName,
                    $"'{name}' が bool ではない（{(value == null ? "null" : value.GetType().Name)}）。" +
                    " 値を比べたいなら [ShowIf(nameof(...), 比べる値)] の形で書く。");
                return false;
            }

            met = negate ? !flag : flag;
            return true;
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
