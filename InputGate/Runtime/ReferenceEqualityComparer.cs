using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace InputGate
{
    /// <summary>Action Mapの名前やGuidではなく、実行中instanceの参照で所有権を分離する。</summary>
    /// <typeparam name="T">参照で比較する型。</typeparam>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        /// <summary>共有する比較器。</summary>
        internal static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        /// <summary>同じinstanceを参照している場合だけtrue。</summary>
        /// <param name="x">左の参照。</param>
        /// <param name="y">右の参照。</param>
        /// <returns>参照が同一ならtrue。</returns>
        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        /// <summary>参照identityからhash codeを返す。</summary>
        /// <param name="obj">hash対象。</param>
        /// <returns>instanceに対応するhash code。</returns>
        public int GetHashCode(T obj) => obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }
}
