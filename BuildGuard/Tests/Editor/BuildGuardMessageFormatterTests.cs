// SPDX-License-Identifier: MIT

using System;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// 複数種の問題をまとめたビルド失敗文が決定論的であることを検証します。
    /// </summary>
    internal sealed class BuildGuardMessageFormatterTests
    {
        /// <summary>混在する問題の並び、件数、改行、末尾案内を診断文全体で検証します。</summary>
        [Test]
        public void Format_MixedFindings_IsSortedAndComplete()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Message Scene";
            var scripts = new[]
            {
                new MissingScriptFinding("Root[1]", 2),
                new MissingScriptFinding("Root[0]", 1)
            };
            var references = new[]
            {
                new MissingObjectReferenceFinding("Root[1]", "Example.Second", 2, "m_Z"),
                new MissingObjectReferenceFinding("Root[0]", "Example.First", 1, "m_A")
            };

            var message = BuildGuardMessageFormatter.Format(scene, scripts, references);

            Assert.That(message, Is.EqualTo(
                "プレイヤービルド対象のシーンで、ビルドを停止する問題が見つかりました。\n" +
                "シーン: <未保存:Message Scene>\n" +
                "欠落スクリプト: 3\n" +
                "- Root[0]: 1\n" +
                "- Root[1]: 2\n" +
                "欠落オブジェクト参照: 2\n" +
                "- Root[0] :: Example.First[1].m_A\n" +
                "- Root[1] :: Example.Second[2].m_Z\n" +
                "再度ビルドする前に、一覧の欠落スクリプトまたはオブジェクト参照を修復するか、該当箇所を削除してください。"));
        }

        /// <summary>問題がない入力を日本語の理由付きで拒否することを検証します。</summary>
        [Test]
        public void Format_NoFindings_ThrowsArgumentException()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var exception = Assert.Throws<ArgumentException>(() => BuildGuardMessageFormatter.Format(
                scene,
                Array.Empty<MissingScriptFinding>(),
                Array.Empty<MissingObjectReferenceFinding>()));

            Assert.That(exception.Message, Is.EqualTo("ビルドを停止する問題が1件以上必要です。"));
        }
    }
}
