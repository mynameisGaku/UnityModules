using UnityEngine;

namespace ScreenTransition
{
    /// <summary>現在の要求、段階、進捗、不透明度をまとめた通知用スナップショット。</summary>
    public readonly struct ScreenTransitionStatus
    {
        /// <summary>段階、要求、進捗、不透明度から通知用スナップショットを作る。</summary>
        /// <param name="phase">現在の処理段階。</param>
        /// <param name="request">現在処理している要求。</param>
        /// <param name="progress">時間に基づく進捗。</param>
        /// <param name="opacity">画面へ表示する実際の不透明度。</param>
        internal ScreenTransitionStatus(ScreenTransitionPhase phase, ScreenTransitionRequest request, float progress, float opacity)
        {
            Phase = phase;
            Request = request;
            Progress = IsFinite(progress) ? Mathf.Clamp01(progress) : 0f;
            Opacity = IsFinite(opacity) ? Mathf.Clamp01(opacity) : 0f;
        }

        /// <summary>現在の処理段階。</summary>
        public ScreenTransitionPhase Phase { get; }

        /// <summary>現在処理している要求。Idleでは既定値。</summary>
        public ScreenTransitionRequest Request { get; }

        /// <summary>0以上1以下の時間進捗。変化曲線を適用する前の値。</summary>
        public float Progress { get; }

        /// <summary>0以上1以下の実際の表示不透明度。</summary>
        public float Opacity { get; }

        /// <summary>新しい要求を受け付けない段階ならtrue。</summary>
        public bool IsBusy => Phase != ScreenTransitionPhase.Idle;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
