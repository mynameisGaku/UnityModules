// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// プレハブ構造差分を取得できなかった1件のシーンを表します。
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideReviewFailure
    {
        internal BuildGuardPrefabOverrideReviewFailure(
            string scenePath,
            BuildGuardPrefabOverrideScanError error,
            string message)
        {
            ScenePath = scenePath ?? string.Empty;
            Error = error;
            Message = message ?? string.Empty;
        }

        /// <summary>検査に失敗したシーンのアセットパスです。</summary>
        internal string ScenePath { get; }

        /// <summary>検査に失敗した原因の種類です。</summary>
        internal BuildGuardPrefabOverrideScanError Error { get; }

        /// <summary>利用者へ提示する日本語の失敗理由です。</summary>
        internal string Message { get; }
    }
}
