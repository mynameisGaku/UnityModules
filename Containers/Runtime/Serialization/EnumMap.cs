using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Containers
{
    /// <summary>
    /// enum のメンバーごとに値を 1 つ持つ配列。Inspector には列挙名がラベルとして並ぶので、
    /// 設定し忘れが実行時ではなく編集時に見える。
    /// <para>
    /// もう一つの理由は実測できるコスト差。Mono の <c>Dictionary&lt;SomeEnum, T&gt;</c> は
    /// 専用の比較子を書かない限り<b>キー比較のたびにボクシングする</b>。ここでは
    /// <see cref="UnsafeUtility.EnumToInt{T}"/> で enum を直接 int として読むため、
    /// 引き当てはただの配列アクセスになり確保が一切起きない。
    /// </para>
    /// <para>
    /// Unity にシリアライズさせるには具体型で継承する：
    /// <code>
    /// [Serializable] public sealed class DamageMultipliers : EnumMap&lt;DamageType, float&gt; { }
    /// </code>
    /// </para>
    /// </summary>
    [Serializable]
    public class EnumMap<TEnum, TValue> : ISerializationCallbackReceiver, IEnumerable<KeyValuePair<TEnum, TValue>>
        where TEnum : struct, Enum
    {
        private static readonly TEnum[] Members = (TEnum[])Enum.GetValues(typeof(TEnum));
        private static readonly int[] MemberValues = BuildMemberValues();
        private static readonly string[] MemberNames = Enum.GetNames(typeof(TEnum));

        /// <summary>宣言順の値がそのまま添字に使えるか（0,1,2,… と並んでいるか）。</summary>
        private static readonly bool IsContiguous = CheckContiguous();

        [SerializeField] private TValue[] _values = new TValue[Members.Length];

        /// <summary>宣言順に並んだ enum メンバー。添字の並びと一致する。</summary>
        public static IReadOnlyList<TEnum> Keys => Members;

        /// <summary>宣言順に並んだ enum メンバー名。ドロワーのラベル用。</summary>
        public static IReadOnlyList<string> KeyNames => MemberNames;

        /// <summary>enum のメンバー数。配列長でもある。</summary>
        public int Count => Members.Length;

        /// <summary>enum をキーにした値。</summary>
        public TValue this[TEnum key]
        {
            get => _values[IndexOf(key)];
            set => _values[IndexOf(key)] = value;
        }

        /// <summary>宣言順の位置で参照を取る。まとめて書き換えるときやドロワーから使う。</summary>
        public ref TValue ByIndex(int index)
        {
            if ((uint)index >= (uint)_values.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return ref _values[index];
        }

        /// <summary>宣言順の位置に対応する enum メンバー。</summary>
        public TEnum KeyAt(int index) => Members[index];

        /// <summary>実体の配列。<see cref="Keys"/> と同じ並び。</summary>
        public TValue[] Values => _values;

        /// <summary>全メンバーに同じ値を入れる。</summary>
        public void SetAll(TValue value)
        {
            for (var i = 0; i < _values.Length; i++) _values[i] = value;
        }

        /// <summary>全メンバーを既定値に戻す。</summary>
        public void Clear() => Array.Clear(_values, 0, _values.Length);

        /// <summary>
        /// enum メンバーの宣言順の位置。値が 0,1,2,… なら添字そのもの、
        /// 飛び値や負値を含む enum では線形探索に落ちる。
        /// </summary>
        private static int IndexOf(TEnum key)
        {
            var value = UnsafeUtility.EnumToInt(key);

            if (IsContiguous)
            {
                if ((uint)value >= (uint)MemberValues.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(key), $"{key} は {typeof(TEnum).Name} のメンバーではない。");
                }

                return value;
            }

            for (var i = 0; i < MemberValues.Length; i++)
            {
                if (MemberValues[i] == value) return i;
            }

            throw new ArgumentOutOfRangeException(nameof(key), $"{key} は {typeof(TEnum).Name} のメンバーではない。");
        }

        private static int[] BuildMemberValues()
        {
            var values = new int[Members.Length];
            for (var i = 0; i < Members.Length; i++) values[i] = UnsafeUtility.EnumToInt(Members[i]);
            return values;
        }

        private static bool CheckContiguous()
        {
            for (var i = 0; i < MemberValues.Length; i++)
            {
                if (MemberValues[i] != i) return false;
            }

            return true;
        }

        /// <summary>enum にメンバーが増減しても配列長が追従するようにする。</summary>
        public void OnBeforeSerialize() => ResizeToMembers();

        /// <inheritdoc cref="OnBeforeSerialize"/>
        public void OnAfterDeserialize() => ResizeToMembers();

        private void ResizeToMembers()
        {
            if (_values == null) _values = new TValue[Members.Length];
            else if (_values.Length != Members.Length) Array.Resize(ref _values, Members.Length);
        }

        /// <summary>確保無しで (メンバー, 値) を列挙する。</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<KeyValuePair<TEnum, TValue>> IEnumerable<KeyValuePair<TEnum, TValue>>.GetEnumerator()
        {
            for (var i = 0; i < Members.Length; i++)
            {
                yield return new KeyValuePair<TEnum, TValue>(Members[i], _values[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TEnum, TValue>>)this).GetEnumerator();

        /// <summary><see cref="EnumMap{TEnum,TValue}"/> の構造体列挙子。</summary>
        public struct Enumerator
        {
            private readonly EnumMap<TEnum, TValue> _map;
            private int _index;

            internal Enumerator(EnumMap<TEnum, TValue> map)
            {
                _map = map;
                _index = -1;
            }

            /// <summary>今の enum メンバー。</summary>
            public TEnum CurrentKey => Members[_index];

            /// <summary>今の値への参照。走査しながら書き換えられる。</summary>
            public ref TValue CurrentValue => ref _map._values[_index];

            /// <summary>今の (メンバー, 値) の組。</summary>
            public KeyValuePair<TEnum, TValue> Current => new KeyValuePair<TEnum, TValue>(Members[_index], _map._values[_index]);

            /// <summary>次のメンバーへ進む。</summary>
            public bool MoveNext() => ++_index < Members.Length;

            /// <summary>先頭に戻す。</summary>
            public void Reset() => _index = -1;
        }
    }
}
