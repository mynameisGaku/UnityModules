using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Containers
{
    /// <summary>
    /// <typeparamref name="TInterface"/> を実装した Unity オブジェクトだけを受け付ける Inspector フィールド。
    /// <para>
    /// Unity はインターフェース型のフィールドをシリアライズできないので、通常は
    /// <see cref="MonoBehaviour"/> のフィールドで受けて <c>Awake</c> でキャストする ——
    /// つまり<b>配線ミスが実行時まで見つからない</b>。ここでは保存する参照は具体型のまま、
    /// ドロワーがドラッグの時点で実装していないオブジェクトを弾く。
    /// </para>
    /// <code>
    /// [SerializeField] private InterfaceReference&lt;IDamageable&gt; _target;
    /// _target.Value?.TakeDamage(10);
    /// </code>
    /// </summary>
    [Serializable]
    public struct InterfaceReference<TInterface> : IEquatable<InterfaceReference<TInterface>>
        where TInterface : class
    {
        [SerializeField] private Object _underlying;

        /// <summary>参照を指定して作る。実装していないオブジェクトを渡すと例外。</summary>
        /// <param name="underlying">保持する Unity オブジェクト。</param>
        public InterfaceReference(Object underlying)
        {
            if (underlying != null && !(underlying is TInterface))
            {
                throw new ArgumentException(
                    $"{underlying.GetType().Name} は {typeof(TInterface).Name} を実装していない。",
                    nameof(underlying));
            }

            _underlying = underlying;
        }

        /// <summary>
        /// インターフェース。未設定または破棄済みなら null。
        /// Unity の null 判定を通しているのが要点で、破棄済みオブジェクトは
        /// 参照としては null でなくても「無い」として扱う。
        /// </summary>
        public TInterface Value => _underlying == null ? null : _underlying as TInterface;

        /// <summary>保存されているオブジェクトそのもの。具体型が必要なときに使う。</summary>
        public Object UnderlyingObject => _underlying;

        /// <summary>使える参照が入っているか。</summary>
        public bool HasValue => _underlying != null && _underlying is TInterface;

        /// <summary>インターフェースを取り出す。使えなければ false。</summary>
        /// <param name="value">取り出した参照。</param>
        public bool TryGetValue(out TInterface value)
        {
            value = Value;
            return value != null;
        }

        /// <summary>同じオブジェクトを指しているか。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(InterfaceReference<TInterface> other) => Equals(_underlying, other._underlying);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is InterfaceReference<TInterface> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => _underlying == null ? 0 : _underlying.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => _underlying == null ? "<none>" : _underlying.name;

        /// <summary>等値比較。</summary>
        public static bool operator ==(InterfaceReference<TInterface> a, InterfaceReference<TInterface> b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(InterfaceReference<TInterface> a, InterfaceReference<TInterface> b) => !a.Equals(b);

        /// <summary>インターフェースへの暗黙変換。</summary>
        public static implicit operator TInterface(InterfaceReference<TInterface> reference) => reference.Value;
    }
}
