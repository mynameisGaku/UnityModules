using UnityEngine;

namespace ScreenTransition
{
    /// <summary>1件の画面遷移操作、表示色、所要時間、変化曲線を表す不変の要求。</summary>
    public readonly struct ScreenTransitionRequest
    {
        /// <summary>画面遷移要求を作る。</summary>
        /// <param name="operation">画面を覆う向き。</param>
        /// <param name="color">表示する色。alphaは最大の不透明度として使う。</param>
        /// <param name="duration">完了までの秒数。0以上3600以下。0なら直ちに完了する。</param>
        /// <param name="easing">進捗へ適用する変化曲線。</param>
        public ScreenTransitionRequest(ScreenTransitionOperation operation, Color color, float duration, ScreenTransitionEasing easing = ScreenTransitionEasing.EaseInOut)
        {
            Operation = operation;
            Color = color;
            Duration = duration;
            Easing = easing;
        }

        /// <summary>画面を覆う向き。</summary>
        public ScreenTransitionOperation Operation { get; }

        /// <summary>表示する色。alphaは最大の不透明度として使う。</summary>
        public Color Color { get; }

        /// <summary>完了までの秒数。実行時は0以上3600以下を受け付ける。</summary>
        public float Duration { get; }

        /// <summary>進捗へ適用する変化曲線。</summary>
        public ScreenTransitionEasing Easing { get; }

        /// <summary>透明な状態から指定色で画面を覆う要求を作る。</summary>
        /// <param name="color">表示する色。</param>
        /// <param name="duration">完了までの秒数。0以上3600以下。</param>
        /// <param name="easing">進捗へ適用する変化曲線。</param>
        /// <returns>画面を覆う要求。</returns>
        public static ScreenTransitionRequest Cover(Color color, float duration, ScreenTransitionEasing easing = ScreenTransitionEasing.EaseInOut) =>
            new ScreenTransitionRequest(ScreenTransitionOperation.Cover, color, duration, easing);

        /// <summary>指定色で覆われた状態から画面を見せる要求を作る。</summary>
        /// <param name="color">開始時に表示する色。</param>
        /// <param name="duration">完了までの秒数。0以上3600以下。</param>
        /// <param name="easing">進捗へ適用する変化曲線。</param>
        /// <returns>画面を見せる要求。</returns>
        public static ScreenTransitionRequest Reveal(Color color, float duration, ScreenTransitionEasing easing = ScreenTransitionEasing.EaseInOut) =>
            new ScreenTransitionRequest(ScreenTransitionOperation.Reveal, color, duration, easing);

        /// <summary>ログ表示に使える要求内容を返す。</summary>
        /// <returns>操作、色、時間、変化曲線。</returns>
        public override string ToString() => $"{Operation}: {Color}, {Duration}s, {Easing}";
    }
}
