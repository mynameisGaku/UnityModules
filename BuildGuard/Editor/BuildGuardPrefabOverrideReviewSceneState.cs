// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 検査前後の読込済みシーンの並び、アクティブシーン、未保存状態を正確に保持します。
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideReviewSceneState
    {
        internal BuildGuardPrefabOverrideReviewSceneState(
            IReadOnlyList<SceneEntry> scenes,
            ulong activeSceneHandle,
            string activeScenePath)
        {
            if (scenes == null)
            {
                throw new ArgumentNullException(nameof(scenes));
            }

            var snapshot = new SceneEntry[scenes.Count];
            for (var index = 0; index < scenes.Count; index++)
            {
                snapshot[index] = scenes[index];
            }

            Scenes = Array.AsReadOnly(snapshot);
            ActiveSceneHandle = activeSceneHandle;
            ActiveScenePath = activeScenePath ?? string.Empty;
        }

        /// <summary>読込順を保ったシーン状態の不変な一覧です。</summary>
        internal IReadOnlyList<SceneEntry> Scenes { get; }

        /// <summary>検査前後で照合するアクティブシーンの識別子です。</summary>
        internal ulong ActiveSceneHandle { get; }

        /// <summary>検査前後で照合するアクティブシーンのアセットパスです。</summary>
        internal string ActiveScenePath { get; }

        /// <summary>Unityの状態を読み書きせず、取得済みの2状態を比較します。</summary>
        internal static bool TryValidate(
            BuildGuardPrefabOverrideReviewSceneState expected,
            BuildGuardPrefabOverrideReviewSceneState current,
            out string message)
        {
            if (expected.Scenes == null || current.Scenes == null)
            {
                message = "読込済みシーンの状態取得が不完全です。";
                return false;
            }

            if (expected.Scenes.Count != current.Scenes.Count)
            {
                message = $"読込済みシーンの数が{expected.Scenes.Count}件から{current.Scenes.Count}件へ変化しました。";
                return false;
            }

            for (var index = 0; index < expected.Scenes.Count; index++)
            {
                var expectedScene = expected.Scenes[index];
                var currentScene = current.Scenes[index];
                if (expectedScene.Handle != currentScene.Handle)
                {
                    message = $"読込済みシーンの識別子または並び順が位置{index}で変化しました。";
                    return false;
                }

                if (!string.Equals(expectedScene.Path, currentScene.Path, StringComparison.Ordinal))
                {
                    message = $"読込済みシーンのパスが位置{index}で変化しました。";
                    return false;
                }

                if (expectedScene.IsDirty != currentScene.IsDirty)
                {
                    var identity = string.IsNullOrEmpty(expectedScene.Path)
                        ? $"識別子 {expectedScene.Handle}"
                        : expectedScene.Path;
                    message = $"{identity} の未保存状態が変化しました。";
                    return false;
                }
            }

            if (expected.ActiveSceneHandle != current.ActiveSceneHandle
                || !string.Equals(
                    expected.ActiveScenePath,
                    current.ActiveScenePath,
                    StringComparison.Ordinal))
            {
                message = "検査中にアクティブシーンが変化しました。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>読込済みシーン1件の識別情報と未保存状態を保持します。</summary>
        internal readonly struct SceneEntry
        {
            internal SceneEntry(ulong handle, string path, bool isDirty)
            {
                Handle = handle;
                Path = path ?? string.Empty;
                IsDirty = isDirty;
            }

            internal ulong Handle { get; }

            internal string Path { get; }

            internal bool IsDirty { get; }
        }
    }
}
