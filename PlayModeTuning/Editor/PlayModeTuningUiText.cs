using System;
using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>編集画面と検査で共有する、上から下への固定手順を定義します。</summary>
    internal static class PlayModeTuningUiText
    {
        internal const string Step1 = "\u2460 対象を選ぶ";
        internal const string Step2 = "\u2461 再生中の値を記録する";
        internal const string Step3 = "\u2462 再生終了後に差分を見る";
        internal const string Step4 = "\u2463 変更内容を確認する";
        internal const string Step5 = "\u2464 変更を反映して結果を見る";

        /// <summary>画面へ表示する手順を処理順に返します。</summary>
        internal static IReadOnlyList<string> OrderedSteps => Array.AsReadOnly(new[] { Step1, Step2, Step3, Step4, Step5 });
    }
}
