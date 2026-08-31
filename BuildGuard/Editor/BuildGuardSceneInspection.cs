// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 読込済みシーン1件から集めた、ビルドを停止する問題を保持します。
    /// </summary>
    internal readonly struct BuildGuardSceneInspection
    {
        /// <summary>欠落スクリプトと欠落オブジェクト参照の検査結果を作成します。</summary>
        internal BuildGuardSceneInspection(
            IReadOnlyList<MissingScriptFinding> missingScripts,
            IReadOnlyList<MissingObjectReferenceFinding> missingObjectReferences)
        {
            MissingScripts = missingScripts;
            MissingObjectReferences = missingObjectReferences;
        }

        /// <summary>シーンで見つかった欠落スクリプトの一覧です。</summary>
        internal IReadOnlyList<MissingScriptFinding> MissingScripts { get; }

        /// <summary>シーンで見つかった欠落オブジェクト参照の一覧です。</summary>
        internal IReadOnlyList<MissingObjectReferenceFinding> MissingObjectReferences { get; }

        /// <summary>ビルドを停止する問題が1件以上あるかを表します。</summary>
        internal bool HasFindings => MissingScripts.Count > 0 || MissingObjectReferences.Count > 0;
    }
}
