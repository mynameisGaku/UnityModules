// SPDX-License-Identifier: MIT

using NUnit.Framework;
using ProjectSetup.Editor;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupLayerCollisionStoreTests
    {
        [Test]
        public void CreateNamedRules_ReturnsStableUnorderedPairsIncludingSelfPairs()
        {
            var layers = new string[ProjectSetupLayerCollisionStore.LayerCount];
            layers[0] = "Default";
            layers[8] = "Gameplay";
            layers[31] = "UI";
            var masks = EmptyMasks();
            EnableCollision(masks, 0, 0);
            EnableCollision(masks, 0, 31);
            EnableCollision(masks, 8, 8);

            var rules = ProjectSetupLayerCollisionStore.CreateNamedRules(layers, masks);

            Assert.That(rules, Has.Length.EqualTo(6));
            Assert.That(Format(rules[0]), Is.EqualTo("Default|Default|True"));
            Assert.That(Format(rules[1]), Is.EqualTo("Default|Gameplay|False"));
            Assert.That(Format(rules[2]), Is.EqualTo("Default|UI|True"));
            Assert.That(Format(rules[3]), Is.EqualTo("Gameplay|Gameplay|True"));
            Assert.That(Format(rules[4]), Is.EqualTo("Gameplay|UI|False"));
            Assert.That(Format(rules[5]), Is.EqualTo("UI|UI|False"));

            layers[0] = "Changed";
            masks[0] = 0;
            Assert.That(Format(rules[2]), Is.EqualTo("Default|UI|True"));
        }

        [Test]
        public void CreateNamedRules_InvalidShapeReturnsEmptyRules()
        {
            Assert.That(ProjectSetupLayerCollisionStore.CreateNamedRules(null, EmptyMasks()), Is.Empty);
            Assert.That(ProjectSetupLayerCollisionStore.CreateNamedRules(new string[31], EmptyMasks()), Is.Empty);
            Assert.That(ProjectSetupLayerCollisionStore.CreateNamedRules(new string[32], new int[31]), Is.Empty);
        }

        [Test]
        public void IsCollisionEnabled_HandlesSignBitAndRejectsInvalidCoordinates()
        {
            var masks = EmptyMasks();
            EnableCollision(masks, 0, 31);

            Assert.That(ProjectSetupLayerCollisionStore.IsCollisionEnabled(masks, 0, 31), Is.True);
            Assert.That(ProjectSetupLayerCollisionStore.IsCollisionEnabled(masks, 31, 0), Is.True);
            Assert.That(ProjectSetupLayerCollisionStore.IsCollisionEnabled(masks, 0, 30), Is.False);
            Assert.That(ProjectSetupLayerCollisionStore.IsCollisionEnabled(masks, -1, 0), Is.False);
            Assert.That(ProjectSetupLayerCollisionStore.IsCollisionEnabled(masks, 0, 32), Is.False);
            Assert.That(ProjectSetupLayerCollisionStore.IsCollisionEnabled(new int[31], 0, 1), Is.False);
        }

        [Test]
        public void IsSymmetric_RequiresExactlyThirtyTwoMirroredRows()
        {
            var masks = EmptyMasks();
            EnableCollision(masks, 0, 31);
            EnableCollision(masks, 8, 12);

            Assert.That(ProjectSetupLayerCollisionStore.IsSymmetric(masks), Is.True);

            masks[31] &= ~(1 << 0);
            Assert.That(ProjectSetupLayerCollisionStore.IsSymmetric(masks), Is.False);
            Assert.That(ProjectSetupLayerCollisionStore.IsSymmetric(new int[31]), Is.False);
            Assert.That(ProjectSetupLayerCollisionStore.IsSymmetric(null), Is.False);
        }

        [Test]
        public void FindLayerIndex_UsesExactOrdinalName()
        {
            var layers = new[] { "Default", "Gameplay", "gameplay" };

            Assert.That(ProjectSetupLayerCollisionStore.FindLayerIndex(layers, "Gameplay"), Is.EqualTo(1));
            Assert.That(ProjectSetupLayerCollisionStore.FindLayerIndex(layers, "gameplay"), Is.EqualTo(2));
            Assert.That(ProjectSetupLayerCollisionStore.FindLayerIndex(layers, "Missing"), Is.EqualTo(-1));
            Assert.That(ProjectSetupLayerCollisionStore.FindLayerIndex(layers, string.Empty), Is.EqualTo(-1));
            Assert.That(ProjectSetupLayerCollisionStore.FindLayerIndex(null, "Gameplay"), Is.EqualTo(-1));
        }

        private static int[] EmptyMasks()
        {
            return new int[ProjectSetupLayerCollisionStore.LayerCount];
        }

        private static void EnableCollision(int[] masks, int first, int second)
        {
            masks[first] |= 1 << second;
            masks[second] |= 1 << first;
        }

        private static string Format(ProjectSetupLayerCollision rule)
        {
            return $"{rule.FirstLayer}|{rule.SecondLayer}|{rule.CollisionsEnabled}";
        }
    }
}
