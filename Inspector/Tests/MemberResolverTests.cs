using System;
using Inspector.Editor;
using NUnit.Framework;

namespace Inspector.Tests
{
    /// <summary>
    /// 属性に書かれた名前から値を引く部分。
    /// private も基底クラスも引けること、失敗しても投げずに理由を返すことを確かめる。
    /// </summary>
    public sealed class MemberResolverTests
    {
        private class Base
        {
            private bool _inheritedPrivate = true;

            protected int Protected => 7;

            public bool ReadInheritedPrivate() => _inheritedPrivate;
        }

        private sealed class Derived : Base
        {
            private readonly string _name = "derived";

            public bool PublicFlag = true;

            public static int StaticNumber = 42;

            public float Computed => 1.5f;

            public bool NoArguments() => true;

            public bool WithArguments(int _) => true;

            public string Name => _name;

            public int Throws => throw new InvalidOperationException("壊れている");
        }

        [Test]
        public void TryGetValue_ReadsPrivateFieldsIncludingInheritedOnes()
        {
            var target = new Derived();

            Assert.IsTrue(MemberResolver.TryGetValue(target, "_name", out var name, out _));
            Assert.AreEqual("derived", name);

            Assert.IsTrue(MemberResolver.TryGetValue(target, "_inheritedPrivate", out var inherited, out _),
                "BindingFlags.FlattenHierarchy は基底の private を拾わないため、自前で辿れているかを見る");
            Assert.AreEqual(true, inherited);

            Assert.IsTrue(MemberResolver.TryGetValue(target, "Protected", out var protectedValue, out _));
            Assert.AreEqual(7, protectedValue);
        }

        [Test]
        public void TryGetValue_ReadsPropertiesAndParameterlessMethods()
        {
            var target = new Derived();

            Assert.IsTrue(MemberResolver.TryGetValue(target, "Computed", out var computed, out _));
            Assert.AreEqual(1.5f, computed);

            Assert.IsTrue(MemberResolver.TryGetValue(target, "NoArguments", out var invoked, out _));
            Assert.AreEqual(true, invoked);
        }

        [Test]
        public void TryGetValue_ReadsStaticMembers()
        {
            Assert.IsTrue(MemberResolver.TryGetValue(new Derived(), "StaticNumber", out var value, out _));
            Assert.AreEqual(42, value);
        }

        [Test]
        public void TryGetValue_IgnoresMethodsThatNeedArguments()
        {
            var found = MemberResolver.TryGetValue(new Derived(), "WithArguments", out _, out var error);

            Assert.IsFalse(found, "引数の要るメソッドは値として引けない");
            StringAssert.Contains("WithArguments", error);
        }

        [Test]
        public void TryGetValue_ExplainsMissingMembersInsteadOfThrowing()
        {
            var found = MemberResolver.TryGetValue(new Derived(), "_typo", out var value, out var error);

            Assert.IsFalse(found);
            Assert.IsNull(value);
            StringAssert.Contains("_typo", error);
            StringAssert.Contains(nameof(Derived), error);
        }

        [Test]
        public void TryGetValue_CatchesExceptionsFromGetters()
        {
            // Inspector の描画中に投げ返すと、以降のフィールドが 1 つも描かれなくなる。
            var found = MemberResolver.TryGetValue(new Derived(), "Throws", out _, out var error);

            Assert.IsFalse(found);
            StringAssert.Contains("壊れている", error);
        }

        [Test]
        public void FindMethod_MatchesByParameterCount()
        {
            Assert.IsNotNull(MemberResolver.FindMethod(typeof(Derived), nameof(Derived.NoArguments), 0));
            Assert.IsNull(MemberResolver.FindMethod(typeof(Derived), nameof(Derived.NoArguments), 1));
            Assert.IsNotNull(MemberResolver.FindMethod(typeof(Derived), nameof(Derived.WithArguments), 1));
            Assert.IsNotNull(MemberResolver.FindMethod(typeof(Derived), nameof(Derived.ReadInheritedPrivate), 0),
                "基底クラスのメソッドも見つかること");
        }

        [Test]
        public void TryInvoke_ReportsMissingMethods()
        {
            Assert.IsFalse(MemberResolver.TryInvoke(new Derived(), "NotThere", out var error));
            StringAssert.Contains("NotThere", error);
        }
    }
}
