using System;
using System.Collections.Generic;
using System.Reflection;

namespace Inspector.Editor
{
    /// <summary>
    /// 属性に書かれたメンバー名（<c>nameof(_useOverride)</c> など）から実際の値を引く。
    /// <para>
    /// フィールド・プロパティ・引数なしメソッドのどれでも受け付ける。
    /// 呼ぶ側から見れば「その名前の何かの現在値」が欲しいだけで、
    /// それがフィールドなのか計算プロパティなのかを属性に書かせる意味が無いため。
    /// </para>
    /// <para>
    /// private でも、基底クラスで宣言されていても引ける。
    /// <c>BindingFlags.FlattenHierarchy</c> は private な基底メンバーを拾わないので、
    /// 継承の連なりを自前で辿っている。
    /// </para>
    /// </summary>
    public static class MemberResolver
    {
        private const BindingFlags DeclaredMembers =
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> Cache =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();

        /// <summary>
        /// 名前に一致するメンバーを探す。見つからなければ <c>null</c>。
        /// <para>
        /// 同名のものがあった場合はフィールド、プロパティ、メソッドの順で選ぶ。
        /// 派生側の宣言が基底側より優先される。
        /// </para>
        /// </summary>
        public static MemberInfo Find(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            if (!Cache.TryGetValue(type, out var byName))
            {
                byName = new Dictionary<string, MemberInfo>();
                Cache[type] = byName;
            }

            if (byName.TryGetValue(name, out var cached)) return cached;

            var found = Search(type, name);
            byName[name] = found;
            return found;
        }

        /// <summary>
        /// メンバーの現在値を取り出す。
        /// <para>
        /// 取得の途中で例外が出ても投げ返さず <paramref name="error"/> に文言を入れて <c>false</c> を返す。
        /// Inspector の描画中に例外を投げると、その後の全フィールドが描かれなくなり、
        /// 値を直して立て直すことすらできなくなるため。
        /// </para>
        /// </summary>
        public static bool TryGetValue(object target, string name, out object value, out string error)
        {
            value = null;
            error = null;

            if (string.IsNullOrEmpty(name))
            {
                error = "メンバー名が空。";
                return false;
            }

            if (target == null)
            {
                error = "対象が null。";
                return false;
            }

            var member = Find(target.GetType(), name);
            if (member == null)
            {
                error = $"'{name}' という名前のフィールド・プロパティ・引数なしメソッドが {target.GetType().Name} に無い。";
                return false;
            }

            try
            {
                switch (member)
                {
                    case FieldInfo field:
                        value = field.GetValue(field.IsStatic ? null : target);
                        return true;

                    case PropertyInfo property:
                        if (!property.CanRead)
                        {
                            error = $"'{name}' に get が無い。";
                            return false;
                        }

                        value = property.GetValue(property.GetGetMethod(true).IsStatic ? null : target);
                        return true;

                    case MethodInfo method:
                        value = method.Invoke(method.IsStatic ? null : target, null);
                        return true;

                    default:
                        error = $"'{name}' は値を取り出せる種類のメンバーではない。";
                        return false;
                }
            }
            catch (Exception exception)
            {
                var inner = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;

                error = $"'{name}' の取得中に例外が出た: {inner.Message}";
                return false;
            }
        }

        /// <summary>
        /// 名前と引数の数が一致するメソッドを探す。見つからなければ <c>null</c>。
        /// <paramref name="parameterCount"/> に負の値を渡すと引数の数を問わない。
        /// </summary>
        public static MethodInfo FindMethod(Type type, string name, int parameterCount)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var methods = current.GetMethods(DeclaredMembers);

                for (var i = 0; i < methods.Length; i++)
                {
                    var method = methods[i];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal)) continue;
                    if (parameterCount >= 0 && method.GetParameters().Length != parameterCount) continue;

                    return method;
                }
            }

            return null;
        }

        /// <summary>名前が一致するメソッドを全て返す。引数の形が複数許される検査メソッド向け。</summary>
        public static List<MethodInfo> FindMethods(Type type, string name)
        {
            var results = new List<MethodInfo>();
            if (type == null || string.IsNullOrEmpty(name)) return results;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var methods = current.GetMethods(DeclaredMembers);

                for (var i = 0; i < methods.Length; i++)
                {
                    if (string.Equals(methods[i].Name, name, StringComparison.Ordinal)) results.Add(methods[i]);
                }
            }

            return results;
        }

        /// <summary>
        /// 引数なしメソッドを呼ぶ。
        /// <para>
        /// 呼び出し先が例外を投げても投げ返さず、<paramref name="error"/> に文言を入れて <c>false</c> を返す。
        /// </para>
        /// </summary>
        public static bool TryInvoke(object target, string name, out string error)
        {
            error = null;

            if (target == null)
            {
                error = "対象が null。";
                return false;
            }

            var method = FindMethod(target.GetType(), name, 0);
            if (method == null)
            {
                error = $"引数なしメソッド '{name}' が {target.GetType().Name} に無い。";
                return false;
            }

            try
            {
                method.Invoke(method.IsStatic ? null : target, null);
                return true;
            }
            catch (Exception exception)
            {
                var inner = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;

                error = $"'{name}' の呼び出しで例外が出た: {inner.Message}";
                return false;
            }
        }

        private static MemberInfo Search(Type type, string name)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var field = current.GetField(name, DeclaredMembers);
                if (field != null) return field;

                // GetProperty は同名のインデクサがあると AmbiguousMatchException を投げるため、
                // 引数を取らないものだけを自分で拾う。
                var properties = current.GetProperties(DeclaredMembers);
                for (var i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    if (!string.Equals(property.Name, name, StringComparison.Ordinal)) continue;
                    if (!property.CanRead) continue;
                    if (property.GetIndexParameters().Length != 0) continue;

                    return property;
                }

                var methods = current.GetMethods(DeclaredMembers);
                for (var i = 0; i < methods.Length; i++)
                {
                    var method = methods[i];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal)) continue;
                    if (method.GetParameters().Length != 0) continue;
                    if (method.ReturnType == typeof(void)) continue;

                    return method;
                }
            }

            return null;
        }
    }
}
