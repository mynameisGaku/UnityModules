// SPDX-License-Identifier: MIT

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Haptics
{
    /// <summary>
    /// 振動step列の不変ラッパー。最大<see cref="MaxStepCount"/> stepを防御コピーで所有する。
    /// 不正値はconstructorで即時に例外として報告し、生成済みinstanceは常に検証済みである。
    /// </summary>
    public sealed class HapticsPattern
    {
        /// <summary>許容する最大step数。</summary>
        public const int MaxStepCount = 64;

        private readonly HapticsStep[] _steps;
        private readonly ReadOnlyCollection<HapticsStep> _stepsView;

        /// <summary>保持しているstep列。外部から変更できない。</summary>
        public IReadOnlyList<HapticsStep> Steps => _stepsView;

        /// <summary>全step durationの合計。</summary>
        public int TotalDurationMilliseconds { get; }

        /// <summary>step列を検証してpatternを作る。</summary>
        /// <param name="steps">1〜64個の有効なstep。null要素は不可。</param>
        /// <exception cref="ArgumentNullException">stepsがnull。</exception>
        /// <exception cref="ArgumentOutOfRangeException">空配列、65step以上、範囲外duration、範囲外amplitude。</exception>
        public HapticsPattern(params HapticsStep[] steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));

            var isValid = ValidateSteps(steps, out var error);
            if (!isValid)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), steps.Length, Describe(error));
            }

            _steps = new HapticsStep[steps.Length];
            Array.Copy(steps, _steps, steps.Length);
            _stepsView = new ReadOnlyCollection<HapticsStep>(_steps);

            var total = 0;
            for (var index = 0; index < _steps.Length; index++)
            {
                total += _steps[index].DurationMilliseconds;
            }

            TotalDurationMilliseconds = total;
        }

        /// <summary>保持step列を内部検証する。生成済みinstanceでは常に成功する防御検証。</summary>
        /// <param name="error">失敗reason。成功時は<see cref="HapticsError.None"/>。</param>
        /// <returns>有効な場合はtrue。</returns>
        internal bool TryValidate(out HapticsError error)
        {
            return ValidateSteps(_steps, out error);
        }

        /// <summary>step列を検証し、失敗reasonを分類する。</summary>
        /// <param name="steps">検証対象のstep列。</param>
        /// <param name="error">失敗reason。成功時は<see cref="HapticsError.None"/>。</param>
        /// <returns>有効な場合はtrue。</returns>
        internal static bool ValidateSteps(HapticsStep[] steps, out HapticsError error)
        {
            if (steps == null)
            {
                error = HapticsError.NullPattern;
                return false;
            }

            if (steps.Length == 0)
            {
                error = HapticsError.EmptyPattern;
                return false;
            }

            if (steps.Length > MaxStepCount)
            {
                error = HapticsError.PatternTooLong;
                return false;
            }

            for (var index = 0; index < steps.Length; index++)
            {
                if (steps[index].DurationMilliseconds < HapticsStep.MinDurationMilliseconds ||
                    steps[index].DurationMilliseconds > HapticsStep.MaxDurationMilliseconds)
                {
                    error = HapticsError.InvalidDuration;
                    return false;
                }

                if (float.IsNaN(steps[index].Amplitude) || float.IsInfinity(steps[index].Amplitude) ||
                    steps[index].Amplitude < 0f || steps[index].Amplitude > 1f)
                {
                    error = HapticsError.InvalidAmplitude;
                    return false;
                }
            }

            error = HapticsError.None;
            return true;
        }

        private static string Describe(HapticsError error)
        {
            switch (error)
            {
                case HapticsError.EmptyPattern:
                    return "patternには1つ以上のstepが必要です。";
                case HapticsError.PatternTooLong:
                    return $"patternは最大{MaxStepCount}stepまでです。";
                case HapticsError.InvalidDuration:
                    return $"step durationは{HapticsStep.MinDurationMilliseconds}〜{HapticsStep.MaxDurationMilliseconds}msで指定してください。";
                case HapticsError.InvalidAmplitude:
                    return "step amplitudeは0以上1以下の有限値で指定してください。";
                default:
                    return "patternが不正です。";
            }
        }

        /// <summary>各intent向けの決定論的な標準patternを提供する静的class。</summary>
        public static class Presets
        {
            /// <summary><see cref="HapticsIntent.SelectionTick"/>向け標準pattern。</summary>
            public static HapticsPattern SelectionTick { get; } =
                new HapticsPattern(new HapticsStep(15, 0.3f));

            /// <summary><see cref="HapticsIntent.ImpactLight"/>向け標準pattern。短め20msの高amplitude。</summary>
            public static HapticsPattern ImpactLight { get; } =
                new HapticsPattern(new HapticsStep(20, 0.7f));

            /// <summary><see cref="HapticsIntent.ImpactMedium"/>向け標準pattern。</summary>
            public static HapticsPattern ImpactMedium { get; } =
                new HapticsPattern(new HapticsStep(35, 0.85f));

            /// <summary><see cref="HapticsIntent.ImpactHeavy"/>向け標準pattern。</summary>
            public static HapticsPattern ImpactHeavy { get; } =
                new HapticsPattern(new HapticsStep(50, 1f));

            /// <summary><see cref="HapticsIntent.NotificationSuccess"/>向け標準pattern。2打の上昇確認音。</summary>
            public static HapticsPattern NotificationSuccess { get; } =
                new HapticsPattern(
                    new HapticsStep(30, 0.9f),
                    new HapticsStep(60, 0f),
                    new HapticsStep(30, 0.6f));

            /// <summary><see cref="HapticsIntent.NotificationWarning"/>向け標準pattern。間隔の長い2打。</summary>
            public static HapticsPattern NotificationWarning { get; } =
                new HapticsPattern(
                    new HapticsStep(40, 0.8f),
                    new HapticsStep(80, 0f),
                    new HapticsStep(40, 0.8f));

            /// <summary><see cref="HapticsIntent.NotificationError"/>向け標準pattern。3打の警告。</summary>
            public static HapticsPattern NotificationError { get; } =
                new HapticsPattern(
                    new HapticsStep(60, 1f),
                    new HapticsStep(100, 0f),
                    new HapticsStep(60, 1f),
                    new HapticsStep(100, 0f),
                    new HapticsStep(60, 1f));

            /// <summary>intentへ対応する標準patternを返す。</summary>
            /// <param name="intent">定義済み7種のいずれか。</param>
            /// <returns>対応するpreset pattern。呼出し間で同じinstanceを共有する不変object。</returns>
            /// <exception cref="ArgumentOutOfRangeException">定義外のenum値。</exception>
            public static HapticsPattern Get(HapticsIntent intent)
            {
                switch (intent)
                {
                    case HapticsIntent.SelectionTick: return SelectionTick;
                    case HapticsIntent.ImpactLight: return ImpactLight;
                    case HapticsIntent.ImpactMedium: return ImpactMedium;
                    case HapticsIntent.ImpactHeavy: return ImpactHeavy;
                    case HapticsIntent.NotificationSuccess: return NotificationSuccess;
                    case HapticsIntent.NotificationWarning: return NotificationWarning;
                    case HapticsIntent.NotificationError: return NotificationError;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(intent),
                            intent,
                            "定義されていないHapticsIntentです。");
                }
            }
        }
    }
}
