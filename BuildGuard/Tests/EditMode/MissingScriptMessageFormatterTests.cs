// SPDX-License-Identifier: MIT

using System;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// build失敗messageの順序と内容を検証します。
    /// </summary>
    internal sealed class MissingScriptMessageFormatterTests
    {
        /// <summary>
        /// 入力順に依存せず階層path順の同一messageを生成することを検証します。
        /// </summary>
        [Test]
        public void Format_UnsortedFindings_ProducesDeterministicMessage()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Unsaved/Scene";
            var findings = new[]
            {
                new MissingScriptFinding("Root[1]", 2),
                new MissingScriptFinding("Root[0]", 1)
            };

            var message = MissingScriptMessageFormatter.Format(scene, findings);

            Assert.That(message, Is.EqualTo(
                "Build GuardがPlayer build対象Scene内のMissing Scriptを検出しました。\n" +
                "Scene: <unsaved:Unsaved/Scene>\n" +
                "- Root[0]: 1\n" +
                "- Root[1]: 2\n" +
                "合計: 3\n" +
                "Missing MonoBehaviourを修復または削除してからbuildを再実行してください。"));
        }

        /// <summary>
        /// 検出結果が空の場合に誤った失敗messageを作らないことを検証します。
        /// </summary>
        [Test]
        public void Format_EmptyFindings_ThrowsArgumentException()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.Throws<ArgumentException>(() => MissingScriptMessageFormatter.Format(scene, Array.Empty<MissingScriptFinding>()));
        }
    }
}
