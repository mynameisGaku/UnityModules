// SPDX-License-Identifier: MIT

using System;

namespace PlayerOptions
{
    /// <summary>保存・適用・変更通知を一つの単位にする変更不能なplayer option。</summary>
    public readonly struct PlayerOptionsState : IEquatable<PlayerOptionsState>
    {
        /// <summary>全てのplayer optionを一つのsnapshotとして作る。</summary>
        /// <param name="display">画面表示設定。</param>
        /// <param name="targetFrameRate">-1または正数の目標frame rate。</param>
        /// <param name="masterVolume">0以上1以下のmaster volume。</param>
        /// <param name="quality">品質levelのindexと名前。</param>
        public PlayerOptionsState(
            PlayerDisplayOptions display,
            int targetFrameRate,
            float masterVolume,
            PlayerQualityOptions quality)
        {
            Display = display;
            TargetFrameRate = targetFrameRate;
            MasterVolume = masterVolume;
            Quality = quality;
        }

        /// <summary>画面表示設定。</summary>
        public PlayerDisplayOptions Display { get; }

        /// <summary>-1または正数の目標frame rate。</summary>
        public int TargetFrameRate { get; }

        /// <summary>0以上1以下のmaster volume。</summary>
        public float MasterVolume { get; }

        /// <summary>品質levelのindexと名前。</summary>
        public PlayerQualityOptions Quality { get; }

        /// <summary>全てのplayer optionが等しい場合はtrueを返す。</summary>
        /// <param name="other">比較するoption snapshot。</param>
        /// <returns>全ての値が等しい場合はtrue。</returns>
        public bool Equals(PlayerOptionsState other)
        {
            return Display.Equals(other.Display) &&
                   TargetFrameRate == other.TargetFrameRate &&
                   MasterVolume.Equals(other.MasterVolume) &&
                   Quality.Equals(other.Quality);
        }

        /// <summary>指定objectが同じoption snapshotならtrueを返す。</summary>
        /// <param name="obj">比較するobject。</param>
        /// <returns>同じoption snapshotならtrue。</returns>
        public override bool Equals(object obj) => obj is PlayerOptionsState other && Equals(other);

        /// <summary>全てのplayer optionからhash値を返す。</summary>
        /// <returns>option snapshotのhash値。</returns>
        public override int GetHashCode() => HashCode.Combine(Display, TargetFrameRate, MasterVolume, Quality);

        /// <summary>左右のoption snapshotが等しい場合はtrueを返す。</summary>
        /// <param name="left">左側のoption snapshot。</param>
        /// <param name="right">右側のoption snapshot。</param>
        /// <returns>左右が等しい場合はtrue。</returns>
        public static bool operator ==(PlayerOptionsState left, PlayerOptionsState right) => left.Equals(right);

        /// <summary>左右のoption snapshotが異なる場合はtrueを返す。</summary>
        /// <param name="left">左側のoption snapshot。</param>
        /// <param name="right">右側のoption snapshot。</param>
        /// <returns>左右が異なる場合はtrue。</returns>
        public static bool operator !=(PlayerOptionsState left, PlayerOptionsState right) => !left.Equals(right);
    }
}
