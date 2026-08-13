using System;
using System.Collections.Generic;
using System.Reflection;

namespace Inspector.Editor
{
    /// <summary>
    /// 保存プロパティの所有者を根オブジェクトから辿り、条件参照やメソッド呼び出しへ渡す。
    /// 配列要素は型だけでは位置を決められないため対象外とし、単一の Serializable フィールドだけを扱う。
    /// </summary>
    internal static class InspectorOwnerResolver
    {
        private const BindingFlags DeclaredFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// <paramref name="ownerPath"/> のフィールドを順に辿る。
        /// 対象が失われた、途中が null、フィールドが無い場合は理由を返す。
        /// </summary>
        internal static bool TryGet(object root, string ownerPath, out object owner, out string error)
        {
            return TryResolve(root, ownerPath, out owner, out _, out _, out error);
        }

        /// <summary>
        /// 入れ子の所有者にある引数なしメソッドを呼ぶ。
        /// 所有者が struct の場合は、呼び出し後の boxed 値を根まで書き戻す。
        /// </summary>
        internal static bool TryInvoke(
            object root,
            string ownerPath,
            Type declaredOwnerType,
            string methodName,
            out string error)
        {
            error = null;
            var methodType = declaredOwnerType ?? root?.GetType();
            var method = MemberResolver.FindMethod(methodType, methodName, 0);

            if (method == null)
            {
                error = $"引数なしメソッド '{methodName}' が {methodType?.Name ?? "対象の型"} に無い。";
                return false;
            }

            if (method.IsStatic) return TryInvokeMethod(method, null, out error);

            if (!TryResolve(root, ownerPath, out var owner, out var containers, out var fields, out error)) return false;
            if (!TryInvokeMethod(method, owner, out error)) return false;
            if (owner == null || !owner.GetType().IsValueType) return true;

            try
            {
                object value = owner;

                for (var i = fields.Count - 1; i >= 0; i--)
                {
                    fields[i].SetValue(containers[i], value);
                    value = containers[i];
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"'{methodName}' の結果を保存先へ戻せない: {exception.Message}";
                return false;
            }
        }

        /// <summary>指定した所有者型のメソッドが static か調べる。見つからない場合は偽。</summary>
        internal static bool IsStaticMethod(Type ownerType, string methodName)
        {
            return MemberResolver.FindMethod(ownerType, methodName, 0)?.IsStatic == true;
        }

        private static bool TryResolve(
            object root,
            string ownerPath,
            out object owner,
            out List<object> containers,
            out List<FieldInfo> fields,
            out string error)
        {
            owner = root;
            containers = new List<object>();
            fields = new List<FieldInfo>();
            error = null;

            if (root == null)
            {
                error = "対象が null。";
                return false;
            }

            if (string.IsNullOrEmpty(ownerPath)) return true;
            if (ownerPath.IndexOf("Array.data[", StringComparison.Ordinal) >= 0)
            {
                error = $"配列要素の所有者パス '{ownerPath}' は扱えない。";
                return false;
            }

            var segments = ownerPath.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                if (owner == null)
                {
                    error = $"'{string.Join(".", segments, 0, i)}' が null。";
                    return false;
                }

                var field = FindField(owner.GetType(), segments[i]);
                if (field == null)
                {
                    error = $"所有者パス '{ownerPath}' の '{segments[i]}' が {owner.GetType().Name} に無い。";
                    return false;
                }

                containers.Add(owner);
                fields.Add(field);

                try
                {
                    owner = field.GetValue(owner);
                }
                catch (Exception exception)
                {
                    error = $"所有者パス '{ownerPath}' の取得中に例外が出た: {exception.Message}";
                    return false;
                }
            }

            if (owner != null) return true;

            error = $"所有者パス '{ownerPath}' が null。";
            return false;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var field = current.GetField(name, DeclaredFields);
                if (field != null) return field;
            }

            return null;
        }

        private static bool TryInvokeMethod(MethodInfo method, object target, out string error)
        {
            error = null;

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

                error = $"'{method.Name}' の呼び出しで例外が出た: {inner.Message}";
                return false;
            }
        }
    }
}
