using System;
using System.IO;
using BuildAssistant.Editor;
using NUnit.Framework;

namespace BuildAssistant.Tests
{
    public sealed class BuildInputGuardTests
    {
        [Test]
        public void InactivePreprocessor_DoesNotCaptureOrAffectAnOrdinaryUnityBuild()
        {
            var captureCount = 0;

            Assert.DoesNotThrow(() => BuildInputPreprocessor.Validate(() =>
            {
                captureCount++;
                return BuildAssistantTestData.Environment();
            }));
            Assert.That(captureCount, Is.Zero);
            Assert.That(new BuildInputPreprocessor().callbackOrder, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ActivePreprocessor_AcceptsAnExactRecapture()
        {
            var environment = BuildAssistantTestData.Environment();
            var plan = BuildAssistantTestData.Plan(environment);

            using (BuildInputGuard.Begin(plan))
                Assert.DoesNotThrow(() => BuildInputPreprocessor.Validate(() => environment));
        }

        [Test]
        public void ActivePreprocessor_RejectsAChangedInputWithJapaneseGuidance()
        {
            var plan = BuildAssistantTestData.Plan();

            using (BuildInputGuard.Begin(plan))
            {
                var exception = Assert.Throws<BuildInputChangedException>(() => BuildInputPreprocessor.Validate(() => BuildAssistantTestData.Environment(profileHash: "changed")));
                Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.StalePlan));
                Assert.That(exception.Message, Does.Contain("ビルド前処理中"));
                Assert.That(exception.Message, Does.Contain("計画を作り直してください"));
                Assert.Throws<BuildInputChangedException>(() => BuildInputGuard.ThrowIfRejected());
            }
        }

        [Test]
        public void ActivePreprocessor_HidesAnUnexpectedCaptureException()
        {
            var plan = BuildAssistantTestData.Plan();

            using (BuildInputGuard.Begin(plan))
            {
                var exception = Assert.Throws<BuildInputChangedException>(() => BuildInputPreprocessor.Validate(() => throw new InvalidOperationException("Legacy English diagnostic.")));
                Assert.That(exception.Message, Does.Contain("入力を再確認できなかった"));
                Assert.That(exception.Message, Does.Not.Contain("Legacy English"));
            }
        }

        [Test]
        public void ActivePreprocessor_RejectsOutputAddedByAnEarlierBuildPreprocessor()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var plan = BuildAssistantTestData.Plan();
            using (var reservation = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake).Reserve(plan))
            using (BuildInputGuard.Begin(plan, reservation))
            {
                fake.SetFile(Path.Combine(plan.RunDirectory, "前処理が追加した内容.txt"), "unexpected");

                var exception = Assert.Throws<BuildInputChangedException>(() => BuildInputPreprocessor.Validate(() => BuildAssistantTestData.Environment()));

                Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
                Assert.That(exception.Message, Does.Contain("内容が追加"));
                var recorded = Assert.Throws<BuildInputChangedException>(() => BuildInputGuard.ThrowIfRejected());
                Assert.That(recorded.Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
            }
        }

        [Test]
        public void ActivePreprocessor_ClassifiesAnUnexpectedOutputCheckFailure()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var plan = BuildAssistantTestData.Plan();
            using (var reservation = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake).Reserve(plan))
            using (BuildInputGuard.Begin(plan, reservation))
            {
                fake.FileExistsException = new InvalidOperationException("Legacy English diagnostic.");

                var exception = Assert.Throws<BuildInputChangedException>(() => BuildInputPreprocessor.Validate(() => BuildAssistantTestData.Environment()));

                Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.OutputReservationFailed));
                Assert.That(exception.Message, Does.Contain("出力先の予約状態"));
                Assert.That(exception.Message, Does.Not.Contain("Legacy English"));
            }
        }

        [Test]
        public void Guard_RejectsReentryAndReleasesOnlyItsOwnLease()
        {
            var first = BuildInputGuard.Begin(BuildAssistantTestData.Plan());
            Assert.Throws<InvalidOperationException>(() => BuildInputGuard.Begin(BuildAssistantTestData.Plan(entropy: "87654321")));
            first.Dispose();

            var second = BuildInputGuard.Begin(BuildAssistantTestData.Plan(entropy: "87654321"));
            first.Dispose();
            Assert.Throws<InvalidOperationException>(() => BuildInputGuard.Begin(BuildAssistantTestData.Plan(entropy: "abcdef12")));
            second.Dispose();

            Assert.DoesNotThrow(() => BuildInputGuard.Begin(BuildAssistantTestData.Plan(entropy: "abcdef12")).Dispose());
        }

        [Test]
        public void Executor_ConvertsARealPreprocessorRejectionAndReleasesTheGuard()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var plan = BuildAssistantTestData.Plan();
            using (var reservation = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake).Reserve(plan))
            {
                var executor = new UnityBuildExecutor(activePlan =>
                {
                    fake.SetFile(Path.Combine(activePlan.RunDirectory, "先行前処理の内容.txt"), "unexpected");
                    BuildInputPreprocessor.Validate(() => BuildAssistantTestData.Environment());
                    return null;
                });

                var exception = Assert.Throws<BuildInputChangedException>(() => executor.Execute(plan, reservation));

                Assert.That(exception.Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
            }

            Assert.DoesNotThrow(() => BuildInputGuard.Begin(BuildAssistantTestData.Plan(entropy: "87654321")).Dispose());
        }
    }
}
