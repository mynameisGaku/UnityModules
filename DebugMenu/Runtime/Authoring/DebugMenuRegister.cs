using System;
using System.Reflection;
using Containers;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>
    /// メニューへページを足す静的メソッドに付ける印。
    /// <para>
    /// 付けたメソッドは <see cref="DebugMenuAutoRegistrar.Populate"/> から呼ばれる。
    /// 各システムが「自分の分だけ」を自分の近くに書けるので、
    /// メニューの組み立てが 1 箇所の巨大な初期化処理にならずに済む。
    /// </para>
    /// <code>
    /// [DebugMenuRegister(Order = 10)]
    /// private static void RegisterPlayerDebug(DebugMenuRoot menu)
    /// {
    ///     var page = menu.AddPage("Player");
    ///     page.Bool("無敵", () => Player.Invincible, v => Player.Invincible = v);
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DebugMenuRegisterAttribute : Attribute
    {
        /// <summary>呼ばれる順。小さいほど先。ページの並び順を決めるのに使う。</summary>
        public int Order { get; set; }
    }

    /// <summary>
    /// <see cref="DebugMenuRegisterAttribute"/> の付いたメソッドを探して呼ぶ。
    /// <para>
    /// 走査は起動時に 1 回だけ。<b>全アセンブリを舐めるのは高くつく</b>ので、
    /// このモジュールを参照していないアセンブリは名前で先に落としている。
    /// </para>
    /// </summary>
    public static class DebugMenuAutoRegistrar
    {
        /// <summary>
        /// 印の付いたメソッドを全て呼び、メニューを組み立てる。
        /// <para>
        /// 1 つのメソッドが落ちても残りは呼ぶ。デバッグメニューの一部が未完成でも、
        /// 他の部分は使えた方がよいため。
        /// </para>
        /// </summary>
        /// <param name="menu">組み立て先のメニュー。</param>
        /// <returns>呼び出しに成功したメソッドの数。</returns>
        public static int Populate(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            using var found = TempList<(MethodInfo Method, int Order)>.Rent();
            Collect(found.List);

            // Order の昇順。同値は見つかった順を保つため、安定な挿入ソートにする。
            for (var i = 1; i < found.List.Count; i++)
            {
                var current = found.List[i];
                var j = i - 1;

                while (j >= 0 && found.List[j].Order > current.Order)
                {
                    found.List[j + 1] = found.List[j];
                    j--;
                }

                found.List[j + 1] = current;
            }

            var invoked = 0;
            for (var i = 0; i < found.List.Count; i++)
            {
                var method = found.List[i].Method;

                try
                {
                    method.Invoke(null, new object[] { menu });
                    invoked++;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[DebugMenu] {method.DeclaringType?.FullName}.{method.Name} の登録に失敗した。");
                    Debug.LogException(exception.InnerException ?? exception);
                }
            }

            return invoked;
        }

        private static void Collect(FastList<(MethodInfo Method, int Order)> results)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                if (ShouldSkip(assembly)) continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    // 一部の型が読めなくても、読めた分だけで続ける。
                    types = exception.Types;
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null) continue;

                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                    foreach (var method in methods)
                    {
                        var attribute = method.GetCustomAttribute<DebugMenuRegisterAttribute>();
                        if (attribute == null) continue;

                        if (!IsValidSignature(method))
                        {
                            Debug.LogError(
                                $"[DebugMenu] {type.FullName}.{method.Name} の形が合わない。" +
                                " static void Method(DebugMenuRoot) である必要がある。");
                            continue;
                        }

                        results.Add((method, attribute.Order));
                    }
                }
            }
        }

        private static bool IsValidSignature(MethodInfo method)
        {
            if (method.ReturnType != typeof(void)) return false;

            var parameters = method.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(DebugMenuRoot);
        }

        private static bool ShouldSkip(Assembly assembly)
        {
            if (assembly.IsDynamic) return true;

            // このモジュール自身には登録メソッドを置かない。
            var runtimeAssembly = typeof(DebugMenuRegisterAttribute).Assembly;
            if (ReferenceEquals(assembly, runtimeAssembly)) return true;

            // 属性を使うアセンブリは DebugMenu.Runtime を直接参照している。
            // 名前で System.* や Unity.* を落とすと、同じ接頭辞を使うゲーム側 asmdef まで
            // 黙って除外してしまうため、実際の参照関係だけを見る。
            var runtimeName = runtimeAssembly.GetName().Name;
            try
            {
                var references = assembly.GetReferencedAssemblies();
                for (var i = 0; i < references.Length; i++)
                {
                    if (string.Equals(references[i].Name, runtimeName, StringComparison.Ordinal)) return false;
                }
            }
            catch (Exception)
            {
                return true;
            }

            return true;
        }
    }
}
