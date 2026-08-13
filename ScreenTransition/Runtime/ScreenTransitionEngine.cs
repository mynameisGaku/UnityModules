using System;
using UnityEngine;

namespace ScreenTransition
{
    /// <summary>Unityのフレーム時間と描画先に依存せず、1件の不透明度変化を進める。</summary>
    internal sealed class ScreenTransitionEngine
    {
        internal const float MaximumDuration = 3600f;

        private ScreenTransitionRequest _request;
        private ScreenTransitionStatus _status;
        private double _elapsed;
        private bool _active;

        /// <summary>要求を処理していない状態でengineを作る。</summary>
        internal ScreenTransitionEngine()
        {
            _status = new ScreenTransitionStatus(ScreenTransitionPhase.Idle, default, 0f, 0f);
        }

        /// <summary>現在の計算済み状態。</summary>
        internal ScreenTransitionStatus Status => _status;

        /// <summary>時間を受け取れる実行中状態ならtrue。</summary>
        internal bool IsActive => _active;

        /// <summary>要求の値域と列挙値を検査する。</summary>
        /// <param name="request">検査する画面遷移要求。</param>
        /// <returns>使用可能なら成功、そうでなければInvalidRequest。</returns>
        internal static ScreenTransitionResult Validate(ScreenTransitionRequest request)
        {
            if (!Enum.IsDefined(typeof(ScreenTransitionOperation), request.Operation))
            {
                return ScreenTransitionResult.Failure(request, ScreenTransitionError.InvalidRequest, "画面遷移操作の種類が不正です。");
            }

            if (!Enum.IsDefined(typeof(ScreenTransitionEasing), request.Easing))
            {
                return ScreenTransitionResult.Failure(request, ScreenTransitionError.InvalidRequest, "変化曲線の種類が不正です。");
            }

            if (!IsFinite(request.Duration) || request.Duration < 0f || request.Duration > MaximumDuration)
            {
                return ScreenTransitionResult.Failure(request, ScreenTransitionError.InvalidRequest, $"所要時間には0以上{MaximumDuration}以下の有限秒を指定してください。");
            }

            if (!IsFinite(request.Color.r) || !IsFinite(request.Color.g) || !IsFinite(request.Color.b) || !IsFinite(request.Color.a) || request.Color.a < 0f || request.Color.a > 1f)
            {
                return ScreenTransitionResult.Failure(request, ScreenTransitionError.InvalidRequest, "色の各成分には有限値を、alphaには0以上1以下を指定してください。");
            }

            return ScreenTransitionResult.Success(request);
        }

        /// <summary>検査済みの要求を開始する。</summary>
        /// <param name="request">開始する要求。</param>
        /// <exception cref="InvalidOperationException">既に要求を処理している場合。</exception>
        internal void Start(ScreenTransitionRequest request)
        {
            if (_active) throw new InvalidOperationException("別の画面遷移要求を処理しています。");

            _request = request;
            _elapsed = 0f;
            _active = request.Duration > 0f;
            SetProgress(request.Duration == 0f ? 1f : 0f);
        }

        /// <summary>実時間の経過秒数で実行中要求を進める。</summary>
        /// <param name="unscaledDeltaTime">timeScaleの影響を受けない経過秒数。</param>
        internal void Tick(float unscaledDeltaTime)
        {
            if (!_active) return;
            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime), "経過秒数には0以上の有限値を指定してください。");

            _elapsed = Math.Min(_request.Duration, _elapsed + unscaledDeltaTime);
            SetProgress((float)(_elapsed / _request.Duration));
        }

        /// <summary>失敗段階へ移し、現在の進捗を保つ。</summary>
        internal void Fail()
        {
            _active = false;
            _status = new ScreenTransitionStatus(ScreenTransitionPhase.Failed, _request, _status.Progress, _status.Opacity);
        }

        /// <summary>次の要求を受け付ける待機状態へ戻す。</summary>
        /// <param name="opacity">待機中に実表示と一致させる不透明度。</param>
        internal void Reset(float opacity)
        {
            _request = default;
            _elapsed = 0f;
            _active = false;
            _status = new ScreenTransitionStatus(ScreenTransitionPhase.Idle, default, 0f, opacity);
        }

        private void SetProgress(float progress)
        {
            var clampedProgress = Mathf.Clamp01(progress);
            var easedProgress = ScreenTransitionEasingUtility.Evaluate(_request.Easing, clampedProgress);
            var opacity = _request.Operation == ScreenTransitionOperation.Cover
                ? _request.Color.a * easedProgress
                : _request.Color.a * (1f - easedProgress);
            var phase = clampedProgress >= 1f ? ScreenTransitionPhase.Completed : ScreenTransitionPhase.Transitioning;
            _active = phase == ScreenTransitionPhase.Transitioning;
            _status = new ScreenTransitionStatus(phase, _request, clampedProgress, opacity);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
