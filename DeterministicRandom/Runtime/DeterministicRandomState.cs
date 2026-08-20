using System;

namespace DeterministicRandom
{
    /// <summary>同じ乱数位置を再構築するalgorithm version付き256-bit状態。</summary>
    public readonly struct DeterministicRandomState : IEquatable<DeterministicRandomState>
    {
        /// <summary>保存済み乱数状態を作る。妥当性はstreamのTryCreateまたはResetで検証する。</summary>
        /// <param name="algorithmVersion">状態を解釈するalgorithm version。</param>
        /// <param name="word0">状態word 0。</param>
        /// <param name="word1">状態word 1。</param>
        /// <param name="word2">状態word 2。</param>
        /// <param name="word3">状態word 3。</param>
        public DeterministicRandomState(int algorithmVersion, ulong word0, ulong word1, ulong word2, ulong word3)
        {
            AlgorithmVersion = algorithmVersion;
            Word0 = word0;
            Word1 = word1;
            Word2 = word2;
            Word3 = word3;
        }

        /// <summary>状態を解釈するalgorithm version。</summary>
        public int AlgorithmVersion { get; }

        /// <summary>状態word 0。</summary>
        public ulong Word0 { get; }

        /// <summary>状態word 1。</summary>
        public ulong Word1 { get; }

        /// <summary>状態word 2。</summary>
        public ulong Word2 { get; }

        /// <summary>状態word 3。</summary>
        public ulong Word3 { get; }

        /// <summary>versionと4つのwordがすべて等しい場合にtrue。</summary>
        public bool Equals(DeterministicRandomState other) => AlgorithmVersion == other.AlgorithmVersion && Word0 == other.Word0 && Word1 == other.Word1 && Word2 == other.Word2 && Word3 == other.Word3;

        /// <summary>同じ型でversionと4つのwordが等しい場合にtrue。</summary>
        public override bool Equals(object obj) => obj is DeterministicRandomState other && Equals(other);

        /// <summary>versionと4つのwordからhash codeを作る。</summary>
        public override int GetHashCode() => HashCode.Combine(AlgorithmVersion, Word0, Word1, Word2, Word3);

        /// <summary>2つの状態が等しい場合にtrue。</summary>
        public static bool operator ==(DeterministicRandomState left, DeterministicRandomState right) => left.Equals(right);

        /// <summary>2つの状態が異なる場合にtrue。</summary>
        public static bool operator !=(DeterministicRandomState left, DeterministicRandomState right) => !left.Equals(right);
    }
}
