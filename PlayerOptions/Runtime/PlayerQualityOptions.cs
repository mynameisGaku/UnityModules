// SPDX-License-Identifier: MIT

using System;

namespace PlayerOptions
{
    /// <summary>Unity Quality Settingsのindexと名前を組にした変更不能な品質設定。</summary>
    public readonly struct PlayerQualityOptions : IEquatable<PlayerQualityOptions>
    {
        /// <summary>品質levelのindexと名前を作る。現在projectとの一致はserviceが検証する。</summary>
        /// <param name="levelIndex">Quality Settings内のindex。</param>
        /// <param name="levelName">Quality Settings内の大文字小文字を区別する名前。</param>
        public PlayerQualityOptions(int levelIndex, string levelName)
        {
            LevelIndex = levelIndex;
            LevelName = levelName ?? string.Empty;
        }

        /// <summary>Quality Settings内のindex。</summary>
        public int LevelIndex { get; }

        /// <summary>Quality Settings内の大文字小文字を区別する名前。</summary>
        public string LevelName { get; }

        /// <summary>indexと名前が等しい場合はtrueを返す。</summary>
        /// <param name="other">比較する品質設定。</param>
        /// <returns>indexと名前が等しい場合はtrue。</returns>
        public bool Equals(PlayerQualityOptions other)
        {
            return LevelIndex == other.LevelIndex &&
                   string.Equals(LevelName, other.LevelName, StringComparison.Ordinal);
        }

        /// <summary>指定objectが同じ品質設定ならtrueを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じ品質設定ならtrue。</returns>
        public override bool Equals(object obj) => obj is PlayerQualityOptions other && Equals(other);

        /// <summary>indexと名前からhash値を返す。</summary>
        /// <returns>品質設定のhash値。</returns>
        public override int GetHashCode() => HashCode.Combine(LevelIndex, LevelName);

        /// <summary>左右の品質設定が等しい場合はtrueを返す。</summary>
        /// <param name="left">左側の品質設定。</param>
        /// <param name="right">右側の品質設定。</param>
        /// <returns>左右が等しい場合はtrue。</returns>
        public static bool operator ==(PlayerQualityOptions left, PlayerQualityOptions right) => left.Equals(right);

        /// <summary>左右の品質設定が異なる場合はtrueを返す。</summary>
        /// <param name="left">左側の品質設定。</param>
        /// <param name="right">右側の品質設定。</param>
        /// <returns>左右が異なる場合はtrue。</returns>
        public static bool operator !=(PlayerQualityOptions left, PlayerQualityOptions right) => !left.Equals(right);
    }
}
