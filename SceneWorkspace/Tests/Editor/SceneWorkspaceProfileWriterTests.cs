using System;
using NUnit.Framework;
using SceneWorkspace.Editor;
using UnityEditor;
using UnityEngine;

namespace SceneWorkspace.Editor.Tests
{
    [TestFixture]
    internal sealed class SceneWorkspaceProfileWriterTests
    {
        // 試験が所有する一時フォルダーを既存資産と区別する接頭辞です。
        private const string TestFolderPrefix = "SceneWorkspaceProfileWriterTests-";

        // 開いているシーンを変更せず参照できる、パッケージ内の試験シーンです。
        private const string TestScenePath = "Packages/com.studiogaku.scene-workspace/Tests/Editor/SceneWorkspaceProfileWriterTestScene.unity";

        // この試験だけが所有する一時フォルダーです。
        private string testFolder;

        // 試験対象の一時設定です。
        private SceneWorkspaceProfile profile;

        // この試験が追加した操作だけを元へ戻すための履歴番号です。
        private int undoGroup = -1;

        [SetUp]
        public void SetUp()
        {
            var testFolderName = TestFolderPrefix + Guid.NewGuid().ToString("N");
            testFolder = "Assets/" + testFolderName;
            var profilePath = testFolder + "/Workspace.asset";
            var folderGuid = AssetDatabase.CreateFolder("Assets", testFolderName);
            Assert.That(folderGuid, Is.Not.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath), Is.Not.Null);

            profile = ScriptableObject.CreateInstance<SceneWorkspaceProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            AssetDatabase.SaveAssetIfDirty(profile);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("現在のシーン構成を設定へ取り込む試験");
        }

        [TearDown]
        public void TearDown()
        {
            if (undoGroup >= 0)
                Undo.RevertAllDownToGroup(undoGroup);
            if (!string.IsNullOrEmpty(testFolder) && AssetDatabase.IsValidFolder(testFolder))
                AssetDatabase.DeleteAsset(testFolder);
            AssetDatabase.Refresh();

            undoGroup = -1;
            profile = null;
            testFolder = null;
        }

        [Test]
        public void CaptureCanBeUndoneAndDoesNotSaveProfileAutomatically()
        {
            var sceneState = new SceneWorkspaceSceneState(0, AssetDatabase.AssetPathToGUID(TestScenePath), TestScenePath, true, true, true, false);
            var capture = new SceneWorkspaceCaptureResult(SceneWorkspaceError.None, string.Empty, "fingerprint", new[] { sceneState });

            var result = SceneWorkspaceProfileWriter.ReplaceFromCapture(profile, capture);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(profile.Entries.Count, Is.EqualTo(1));
            Assert.That(EditorUtility.IsDirty(profile), Is.True);
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(undoGroup);
            undoGroup = -1;
            Assert.That(profile.Entries, Is.Empty);
        }
    }
}
