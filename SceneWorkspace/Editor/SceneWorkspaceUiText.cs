using System;
using System.Collections.Generic;

namespace SceneWorkspace.Editor
{
    /// <summary>画面と表示試験で共有する、上から下への固定された操作順を定義します。</summary>
    internal static class SceneWorkspaceUiText
    {
        internal const string Step1 = "\u2460 作業セットを選ぶ";
        internal const string Step2 = "\u2461 シーン構成を設定";
        internal const string Step3 = "\u2462 差分を確認";
        internal const string Step4 = "\u2463 内容を確認";
        internal const string Step5 = "\u2464 作業セットを切り替える";

        /// <summary>五段階の見出しを表示順の読み取り専用一覧で返します。</summary>
        internal static IReadOnlyList<string> OrderedSteps => Array.AsReadOnly(new[] { Step1, Step2, Step3, Step4, Step5 });
    }
}
