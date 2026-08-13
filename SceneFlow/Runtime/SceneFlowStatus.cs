using UnityEngine;

namespace SceneFlow
{
    /// <summary>現在の要求、段階、読込進捗をまとめた通知用スナップショット。</summary>
    public readonly struct SceneFlowStatus
    {
        /// <summary>要求、段階、正規化前の進捗から通知用スナップショットを作る。</summary>
        /// <param name="phase">現在の処理段階。</param>
        /// <param name="request">現在処理している要求。</param>
        /// <param name="progress">通知する進捗。非有限値は0へ補正する。</param>
        internal SceneFlowStatus(SceneFlowPhase phase, SceneFlowRequest request, float progress)
        {
            Phase = phase;
            Request = request;
            Progress = float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : Mathf.Clamp01(progress);
        }

        /// <summary>現在の処理段階。</summary>
        public SceneFlowPhase Phase { get; }

        /// <summary>現在処理している要求。Idleでは既定値。</summary>
        public SceneFlowRequest Request { get; }

        /// <summary>0以上1以下の進捗。読込以外は段階の開始時0、成功時1。</summary>
        public float Progress { get; }

        /// <summary>新しい要求を受け付けない段階ならtrue。</summary>
        public bool IsBusy => Phase != SceneFlowPhase.Idle;
    }
}
