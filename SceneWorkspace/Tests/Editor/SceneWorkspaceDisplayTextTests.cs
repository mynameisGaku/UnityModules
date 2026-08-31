using System;
using NUnit.Framework;
using SceneWorkspace.Editor;
using UnityEditor;

namespace SceneWorkspace.Editor.Tests
{
    [TestFixture]
    internal sealed class SceneWorkspaceDisplayTextTests
    {
        [TestCase(SceneWorkspaceChangeKind.Keep, "変更なし")]
        [TestCase(SceneWorkspaceChangeKind.Open, "開く")]
        [TestCase(SceneWorkspaceChangeKind.Close, "閉じる")]
        [TestCase(SceneWorkspaceChangeKind.Load, "読み込む")]
        [TestCase(SceneWorkspaceChangeKind.Unload, "読み込みを解除する")]
        [TestCase(SceneWorkspaceChangeKind.Reorder, "並べ替える")]
        [TestCase(SceneWorkspaceChangeKind.SetActive, "使用中にする")]
        [TestCase(SceneWorkspaceChangeKind.ClearActive, "使用中を解除する")]
        public void ChangeKindsHaveJapaneseLabels(SceneWorkspaceChangeKind kind, string expected)
        {
            Assert.That(SceneWorkspaceDisplayText.FormatChangeKind(kind), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownChangeKindKeepsNumericValue()
        {
            var unknown = (SceneWorkspaceChangeKind)9876;

            Assert.That(SceneWorkspaceDisplayText.FormatChangeKind(unknown), Is.EqualTo("不明な変更（9876）"));
        }

        [Test]
        public void EveryKnownErrorHasJapaneseLabel()
        {
            foreach (SceneWorkspaceError error in Enum.GetValues(typeof(SceneWorkspaceError)))
            {
                var label = SceneWorkspaceDisplayText.FormatError(error);
                Assert.That(label, Is.Not.Empty, error.ToString());
                Assert.That(label, Does.Not.Contain(error.ToString()), error.ToString());
            }
        }

        [Test]
        public void UnknownErrorKeepsNumericValueAndNeverLooksSuccessful()
        {
            var unknown = (SceneWorkspaceError)8765;

            Assert.That(SceneWorkspaceDisplayText.FormatError(unknown), Is.EqualTo("不明な問題（8765）"));
            Assert.That(SceneWorkspaceDisplayText.FormatOutcome(unknown, string.Empty), Is.EqualTo("不明な問題（8765）"));
        }

        [Test]
        public void ChangePositionUsesOneBasedJapaneseDisplay()
        {
            var change = new SceneWorkspaceChange(SceneWorkspaceChangeKind.Reorder, "Assets/Scenes/Main.unity", 0, 2, true, true, false, false);

            var text = SceneWorkspaceDisplayText.FormatChange(change);

            Assert.That(text, Does.Contain("変更前：1番"));
            Assert.That(text, Does.Contain("変更後：3番"));
            Assert.That(text, Does.Not.Contain("index"));
        }

        [Test]
        public void MenuUsesJapanesePath()
        {
            var method = typeof(SceneWorkspaceMenu).GetMethod("Open", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var attribute = (MenuItem)Attribute.GetCustomAttribute(method, typeof(MenuItem));

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.menuItem, Is.EqualTo("Tools/シーン作業セット/開く"));
        }
    }
}
