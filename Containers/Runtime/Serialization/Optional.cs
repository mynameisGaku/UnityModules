using System;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// 「設定されているか／いないか」を持つシリアライズ可能な値。Inspector では
    /// チェックボックスと値が 1 行に並ぶ。
    /// <para>
    /// 上書き設定を正直に表現するための型。<c>Nullable&lt;T&gt;</c> は Unity がシリアライズできず、
    /// <c>-1</c> のような番兵は<b>いつかその値が正当な入力になった日に壊れ</b>、
    /// 別々の <c>bool useOverride</c> フィールドは守るべき値から離れていく。
    /// </para>
    /// <code>
    /// [SerializeField] private Optional&lt;float&gt; _speedOverride;
    /// var speed = _speedOverride.GetValueOrDefault(_defaults.Speed);
    /// </code>
    /// </summary>
    [Serializable]
    public struct Optional<T> : IEquatable<Optional<T>>
    {
        [SerializeField] private bool _hasValue;
        [SerializeField] private T _value;

        /// <summary>値ありの状態で作る。</summary>
        /// <param name="value">保持する値。</param>
        public Optional(T value)
        {
            _hasValue = true;
            _value = value;
        }

        /// <summary>値なしの状態。</summary>
        public static Optional<T> None => default;

        /// <summary>値ありの状態を作る。</summary>
        /// <param name="value">保持する値。</param>
        public static Optional<T> Some(T value) => new Optional<T>(value);

        /// <summary>値が設定されているか。</summary>
        public bool HasValue => _hasValue;

        /// <summary>値。未設定なら例外。可能性があるなら <see cref="TryGetValue"/> を使う。</summary>
        public T Value => _hasValue
            ? _value
            : throw new InvalidOperationException($"Optional<{typeof(T).Name}> に値が入っていない。");

        /// <summary>
        /// <see cref="HasValue"/> に関わらず保持している値。
        /// チェックが外れていてもフィールドを描き続ける必要があるドロワー専用。
        /// </summary>
        internal T RawValue => _value;

        /// <summary>値を取り出す。未設定なら false。</summary>
        /// <param name="value">保持している値。</param>
        public bool TryGetValue(out T value)
        {
            value = _value;
            return _hasValue;
        }

        /// <summary>値を取り出す。未設定なら <paramref name="fallback"/>。</summary>
        /// <param name="fallback">未設定のときに返す値。</param>
        public T GetValueOrDefault(T fallback = default) => _hasValue ? _value : fallback;

        /// <summary>値があるときだけ変換する。無ければ未設定のまま。</summary>
        /// <param name="transform">変換関数。</param>
        public Optional<TResult> Map<TResult>(Func<T, TResult> transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            return _hasValue ? new Optional<TResult>(transform(_value)) : default;
        }

        /// <summary>値があるときだけ実行する。呼び出し側が読みやすくなる。</summary>
        /// <param name="action">実行する処理。</param>
        public void IfSet(Action<T> action)
        {
            if (_hasValue) action?.Invoke(_value);
        }

        /// <summary>状態と値の両方が一致するか。両方とも未設定なら等しい。</summary>
        /// <param name="other">比較対象。</param>
        public bool Equals(Optional<T> other)
        {
            if (_hasValue != other._hasValue) return false;
            return !_hasValue || System.Collections.Generic.EqualityComparer<T>.Default.Equals(_value, other._value);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Optional<T> other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            _hasValue ? System.Collections.Generic.EqualityComparer<T>.Default.GetHashCode(_value) : 0;

        /// <inheritdoc/>
        public override string ToString() => _hasValue ? $"Some({_value})" : "None";

        /// <summary>等値比較。</summary>
        public static bool operator ==(Optional<T> a, Optional<T> b) => a.Equals(b);

        /// <summary>非等値比較。</summary>
        public static bool operator !=(Optional<T> a, Optional<T> b) => !a.Equals(b);

        /// <summary>値から「設定済み」への暗黙変換。</summary>
        public static implicit operator Optional<T>(T value) => new Optional<T>(value);

        /// <summary>値への明示変換。未設定なら例外。</summary>
        public static explicit operator T(Optional<T> optional) => optional.Value;
    }
}
