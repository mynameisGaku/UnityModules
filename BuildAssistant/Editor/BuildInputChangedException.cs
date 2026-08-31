using UnityEditor.Build;

namespace BuildAssistant.Editor
{
    /// <summary>ビルド開始直前に、確認済み入力または予約済み出力先との差異を検出したことを表します。</summary>
    internal sealed class BuildInputChangedException : BuildFailedException
    {
        /// <summary>利用者へ示す管理済みの日本語理由を受け取ります。</summary>
        internal BuildInputChangedException(string message) : this(BuildAssistantError.StalePlan, message)
        {
        }

        /// <summary>定義済みの失敗理由と、利用者へ示す管理済みの日本語理由を受け取ります。</summary>
        internal BuildInputChangedException(BuildAssistantError error, string message) : base(message)
        {
            Error = error == BuildAssistantError.None ? BuildAssistantError.StalePlan : error;
        }

        /// <summary>ビルド開始直前の検査で確定した定義済みの失敗理由を取得します。</summary>
        internal BuildAssistantError Error { get; }
    }
}
