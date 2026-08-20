using System;

namespace AudioControl
{
    /// <summary>1つのAudioClipを再生する方法を表す変更不能な要求です。</summary>
    public readonly struct AudioPlayRequest
    {
        /// <summary>指定できる最大fade時間です。</summary>
        public const float MaximumFadeDuration = 60f;

        /// <summary>最も高い再生priorityです。</summary>
        public const int HighestPriority = 0;

        /// <summary>最も低い再生priorityです。</summary>
        public const int LowestPriority = 255;

        /// <summary>標準の音量、pitch、非loop、fadeなし、priority 128、steal許可の要求を返します。</summary>
        public static AudioPlayRequest Default => new AudioPlayRequest(1f, 1f, false, 0f, 128, true);

        /// <summary>最終的にAudioSourceへ適用する音量を取得します。</summary>
        public float Volume { get; }

        /// <summary>AudioSourceへ適用するpitchを取得します。</summary>
        public float Pitch { get; }

        /// <summary>clipをloop再生するかを取得します。</summary>
        public bool Loop { get; }

        /// <summary>非スケール時間で音量0から指定音量へ到達する秒数を取得します。</summary>
        public float FadeInSeconds { get; }

        /// <summary>0を最高、255を最低とするpriorityを取得します。</summary>
        public int Priority { get; }

        /// <summary>voice上限時に同等以下のpriorityを持つ最古voiceを停止できるかを取得します。</summary>
        public bool AllowSteal { get; }

        /// <summary>再生要求を作成します。</summary>
        /// <param name="volume">0以上1以下の最終音量です。</param>
        /// <param name="pitch">0.0001以上3以下の再生速度です。</param>
        /// <param name="loop">loop再生する場合はtrueです。</param>
        /// <param name="fadeInSeconds">0以上60以下の非スケールfade秒数です。</param>
        /// <param name="priority">0を最高、255を最低とするpriorityです。</param>
        /// <param name="allowSteal">voice上限時の決定論的stealを許可する場合はtrueです。</param>
        public AudioPlayRequest(float volume, float pitch, bool loop, float fadeInSeconds, int priority, bool allowSteal)
        {
            Volume = volume;
            Pitch = pitch;
            Loop = loop;
            FadeInSeconds = fadeInSeconds;
            Priority = priority;
            AllowSteal = allowSteal;
        }

        internal bool IsValid()
        {
            return IsFinite(Volume) && Volume >= 0f && Volume <= 1f &&
                   IsFinite(Pitch) && Pitch >= 0.0001f && Pitch <= 3f &&
                   IsFinite(FadeInSeconds) && FadeInSeconds >= 0f && FadeInSeconds <= MaximumFadeDuration &&
                   Priority >= HighestPriority && Priority <= LowestPriority;
        }

        internal static bool IsValidFadeDuration(float seconds)
        {
            return IsFinite(seconds) && seconds >= 0f && seconds <= MaximumFadeDuration;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
