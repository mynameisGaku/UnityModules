using Inspector.Editor;
using NUnit.Framework;

namespace Inspector.Tests
{
    /// <summary>
    /// グループパスの整形。属性の引数は人が手で書くので、
    /// 表記ゆれで別グループに割れないことをここで押さえる。
    /// </summary>
    public sealed class GroupPathUtilityTests
    {
        [TestCase("表示", "表示")]
        [TestCase(" 表示 ", "表示")]
        [TestCase("表示/戦闘", "表示/戦闘")]
        [TestCase("表示 / 戦闘", "表示/戦闘")]
        [TestCase("/表示/", "表示")]
        [TestCase("表示//戦闘", "表示/戦闘")]
        public void Normalize_CollapsesSeparatorsAndTrims(string input, string expected)
        {
            Assert.AreEqual(expected, GroupPathUtility.Normalize(input));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("/")]
        [TestCase("//")]
        public void Normalize_ReturnsNullWhenNothingRemains(string input)
        {
            Assert.IsNull(GroupPathUtility.Normalize(input), "中身の無いパスはグループ無しと同じ扱いにする");
        }

        [Test]
        public void SplitParentLeaf_AgreeOnTheSameSegments()
        {
            var path = " 上級者向け / 物理 / 摩擦 ";

            CollectionAssert.AreEqual(new[] { "上級者向け", "物理", "摩擦" }, GroupPathUtility.Split(path));
            Assert.AreEqual(3, GroupPathUtility.Depth(path));
            Assert.AreEqual("上級者向け/物理", GroupPathUtility.Parent(path));
            Assert.AreEqual("摩擦", GroupPathUtility.Leaf(path));
        }

        [Test]
        public void Parent_IsNullAtTheTopLevel()
        {
            Assert.IsNull(GroupPathUtility.Parent("表示"));
            Assert.IsNull(GroupPathUtility.Parent(""));
        }

        [Test]
        public void Depth_IsZeroForEmptyPaths()
        {
            Assert.AreEqual(0, GroupPathUtility.Depth(null));
            Assert.AreEqual(0, GroupPathUtility.Depth("///"));
        }
    }
}
