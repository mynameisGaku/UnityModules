// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 前回のビルド内容が再利用される前に、今回のプレイヤービルド対象シーンをすべて検査します。
    /// </summary>
    internal sealed class BuildGuardPreflightProcessor : BuildPlayerProcessor
    {
        /// <summary>
        /// ビルド前検査を早期に実行する処理順を返します。
        /// </summary>
        public override int callbackOrder => BuildGuardSceneProcessor.CallbackOrder;

        /// <summary>
        /// Unityが今回のプレイヤービルドへ設定したシーンのパスを検査します。
        /// </summary>
        /// <param name="buildPlayerContext">Unityが準備したプレイヤービルドの処理情報です。</param>
        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            if (buildPlayerContext == null)
            {
                throw new ArgumentNullException(nameof(buildPlayerContext));
            }

            ValidateScenePaths(buildPlayerContext.BuildPlayerOptions.scenes);
        }

        /// <summary>
        /// 元の読込状態とアクティブシーンを保ちながら、指定シーンを順番に開いて検査します。
        /// </summary>
        /// <param name="scenePaths">プレイヤービルドへ渡されたシーンアセットのパスです。</param>
        internal static void ValidateScenePaths(IReadOnlyList<string> scenePaths)
        {
            BuildGuardScenePathVisitor.Visit(
                scenePaths,
                null,
                BuildGuardSceneProcessor.ValidateScene,
                out _);
        }
    }
}
