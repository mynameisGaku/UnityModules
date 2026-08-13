using System;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// Unity がシリアライズできる <see cref="System.Type"/>。
    /// アセンブリ修飾名で保存し、必要になったときに解決する。
    /// <para>
    /// 狙いはデータ駆動の生成。どの振る舞い・状態・効果クラスを作るかを
    /// ScriptableObject 側に持たせ、文字列を手で書く代わりにドロップダウンで選ばせる。
    /// </para>
    /// </summary>
    [Serializable]
    public struct SerializableType : ISerializationCallbackReceiver, IEquatable<SerializableType>
    {
        [SerializeField] private string _assemblyQualifiedName;

        [NonSerialized] private Type _type;
        [NonSerialized] private bool _resolved;

        /// <summary>型を指定して作る。</summary>
        /// <param name="type">保持する型。null 可。</param>
        public SerializableType(Type type)
        {
            _type = type;
            _assemblyQualifiedName = type?.AssemblyQualifiedName ?? string.Empty;
            _resolved = true;
        }

        /// <summary>
        /// 解決した型。未設定のとき、および保存された型がもう存在しないとき
        /// （クラス名を変えた、アセンブリを消した）は null。呼び出し側で null を扱うこと。
        /// </summary>
        public Type Type
        {
            get
            {
                if (_resolved) return _type;

                _type = string.IsNullOrEmpty(_assemblyQualifiedName)
                    ? null
                    : Type.GetType(_assemblyQualifiedName, throwOnError: false);
                _resolved = true;
                return _type;
            }
        }

        /// <summary>何か型名が保存されているか。</summary>
        public bool IsAssigned => !string.IsNullOrEmpty(_assemblyQualifiedName);

        /// <summary>型名は保存されているのに解決できない状態か。改名や削除の検出に使う。</summary>
        public bool IsMissing => IsAssigned && Type == null;

        /// <summary>保存されているアセンブリ修飾名。</summary>
        public string AssemblyQualifiedName => _assemblyQualifiedName;

        /// <summary>この型のインスタンスを作る。解決できなければ null。</summary>
        public T CreateInstance<T>() where T : class
        {
            var type = Type;
            return type == null ? null : Activator.CreateInstance(type) as T;
        }

        /// <summary>保持している型から名前を書き戻す。</summary>
        public void OnBeforeSerialize()
        {
            if (_resolved && _type != null) _assemblyQualifiedName = _type.AssemblyQualifiedName;
        }

        /// <summary>解決結果を捨て、次のアクセス時に引き直させる。</summary>
        public void OnAfterDeserialize()
        {
            _resolved = false;
            _type = null;
        }

        /// <summary>保存されている型名が一致するか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(SerializableType other) =>
            string.Equals(_assemblyQualifiedName, other._assemblyQualifiedName, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SerializableType other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => _assemblyQualifiedName?.GetHashCode() ?? 0;

        /// <inheritdoc/>
        public override string ToString() => Type?.Name ?? (IsAssigned ? $"<missing: {_assemblyQualifiedName}>" : "<none>");

        /// <summary>等値比較。</summary>
        public static bool operator ==(SerializableType a, SerializableType b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(SerializableType a, SerializableType b) => !a.Equals(b);

        /// <summary><see cref="Type"/> への暗黙変換。</summary>
        public static implicit operator Type(SerializableType serializable) => serializable.Type;

        /// <summary><see cref="Type"/> からの暗黙変換。</summary>
        public static implicit operator SerializableType(Type type) => new SerializableType(type);
    }

    /// <summary>
    /// <see cref="SerializableType"/> フィールドのドロップダウンを、
    /// <see cref="BaseType"/> の派生型だけに絞る。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TypeFilterAttribute : PropertyAttribute
    {
        /// <summary>絞り込みの基底型を指定する。</summary>
        /// <param name="baseType">この型の派生だけを候補にする。</param>
        public TypeFilterAttribute(Type baseType) => BaseType = baseType;

        /// <summary>候補の基底型。</summary>
        public Type BaseType { get; }

        /// <summary>抽象クラスとインターフェースも候補に含める。既定は false。</summary>
        public bool AllowAbstract { get; set; }
    }
}
