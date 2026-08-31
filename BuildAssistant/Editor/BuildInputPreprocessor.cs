using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BuildAssistant.Editor
{
    /// <summary>他のビルド前処理が終わった後に、確認済み入力と予約済み出力先を再検査します。</summary>
    internal sealed class BuildInputPreprocessor : IPreprocessBuildWithReport
    {
        /// <summary>同種の前処理のうち、可能な限り最後に実行します。</summary>
        public int callbackOrder => int.MaxValue;

        /// <summary>本モジュールのビルド中だけ現在入力と出力予約を再検査し、差異があれば中止します。</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            Validate(() => new UnityBuildEnvironment().Capture());
        }

        /// <summary>試験可能な取得処理を使って最終入力・出力検査を実行します。</summary>
        internal static void Validate(Func<EnvironmentSnapshot> capture)
        {
            if (!BuildInputGuard.Validate(capture, out var error, out var message))
                throw new BuildInputChangedException(error, message);
        }
    }
}
