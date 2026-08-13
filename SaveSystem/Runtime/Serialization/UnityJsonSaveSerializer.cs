using System;
using System.Collections;
using UnityEngine;

namespace SaveSystem
{
    /// <summary>
    /// <see cref="JsonUtility"/> を使う、外部パッケージ不要の保存変換。
    /// 保存ルートには、宣言型と実行時型が一致する <see cref="SerializableAttribute"/> 付きの具象クラスまたは構造体を要求する。
    /// </summary>
    public sealed class UnityJsonSaveSerializer : ISaveSerializer
    {
        /// <inheritdoc/>
        public string Serialize<T>(T value)
        {
            if (ReferenceEquals(value, null)) throw new ArgumentNullException(nameof(value));

            var declaredType = typeof(T);
            ValidateRootType(declaredType);
            var runtimeType = value.GetType();
            if (runtimeType != declaredType)
            {
                throw new NotSupportedException($"宣言型 {declaredType.FullName} と実行時型 {runtimeType.FullName} が一致しません。派生型のフィールド欠落を防ぐため、実際の型を T に指定してください。");
            }

            var serialized = JsonUtility.ToJson(value);
            if (string.IsNullOrEmpty(serialized)) throw new InvalidOperationException("保存データを JSON に変換できませんでした。");
            return serialized;
        }

        /// <inheritdoc/>
        public T Deserialize<T>(string serialized)
        {
            if (string.IsNullOrEmpty(serialized)) throw new ArgumentException("読み込む JSON が空です。", nameof(serialized));

            ValidateRootType(typeof(T));
            var value = JsonUtility.FromJson<T>(serialized);
            if (ReferenceEquals(value, null)) throw new InvalidOperationException($"JSON を {typeof(T).FullName} に変換できませんでした。");
            return value;
        }

        private static void ValidateRootType(Type type)
        {
            var isCollection = typeof(IEnumerable).IsAssignableFrom(type);
            var isUnityObject = typeof(UnityEngine.Object).IsAssignableFrom(type);
            var isRuntimeType = type.Assembly == typeof(string).Assembly;
            var isNullable = Nullable.GetUnderlyingType(type) != null;
            var isUnsupportedShape = type == typeof(string) || type == typeof(object) || type.IsPrimitive || type.IsEnum || type.IsArray || type.IsInterface || type.IsAbstract || type.IsPointer || type.IsByRef || type.ContainsGenericParameters || typeof(Delegate).IsAssignableFrom(type);

            if (isUnsupportedShape || isCollection || isUnityObject || isRuntimeType || isNullable)
            {
                throw new NotSupportedException($"{type.FullName} は Unity JSON の保存ルートに使用できません。[Serializable] を付けた具象クラスまたは構造体で包んでください。");
            }

            if (!Attribute.IsDefined(type, typeof(SerializableAttribute), false))
            {
                throw new NotSupportedException($"保存ルート型 {type.FullName} に [Serializable] がありません。");
            }
        }
    }
}
