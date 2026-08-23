using System;
using BuildAssistant.Editor;
using NUnit.Framework;
using UnityEditor;

namespace BuildAssistant.Tests
{
    public sealed class BuildAssistantUiTests
    {
        [Test]
        public void WorkflowHeadings_KeepSettingsBeforePreviewAndBuild()
        {
            var headings = new string[BuildAssistantWindow.SectionCardCount];
            for (var sectionIndex = 0; sectionIndex < headings.Length; sectionIndex++)
                headings[sectionIndex] = BuildAssistantWindow.GetSectionHeading(sectionIndex);

            Assert.That(headings, Is.EqualTo(new[]
            {
                "\u2460 Profile",
                "\u2461 Output",
                "\u2462 Preview",
                "\u2463 Confirm",
                "\u2464 Build / Result / Export"
            }));
            Assert.That(BuildAssistantWindow.MinimumWidth, Is.EqualTo(620f));
            Assert.That(BuildAssistantWindow.MinimumHeight, Is.EqualTo(480f));
            Assert.That(BuildAssistantWindow.SectionCardSpacing, Is.GreaterThan(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildAssistantWindow.GetSectionHeading(BuildAssistantWindow.SectionCardCount));
            Assert.That(BuildAssistantMenu.MenuPath, Is.EqualTo("Tools/Build Assistant/Open"));
        }

        [Test]
        public void OutputHelp_StatesLocalDriveOnlyConstraint()
        {
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("local-drive"));
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("UNC"));
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("network"));
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("mapped-drive"));
        }

        [Test]
        public void ProfileHelp_DistinguishesEditorTargetFromCustomProfileAuthority()
        {
            Assert.That(BuildAssistantWindow.EditorTargetLabel, Is.EqualTo("Editor Active Target"));
            Assert.That(BuildAssistantWindow.ProfileHelpText, Does.Contain("custom Build Profile"));
            Assert.That(BuildAssistantWindow.ProfileHelpText, Does.Contain("authoritative profile target"));
            Assert.That(BuildAssistantWindow.ProfileHelpText, Does.Contain("Confirm after Preview"));
            Assert.That(BuildAssistantWindow.InputFingerprintLabel, Is.EqualTo("Captured Input Fingerprint"));
        }

        [Test]
        public void OutputRootChange_InvalidatesPreviewAndConfirmation()
        {
            var readyPlan = CreateReadyPlan("C:\\Builds");
            var presenter = CreatePresenter(_ => readyPlan);
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.SetOutputRoot("C:\\OtherBuilds");

            Assert.That(presenter.Plan, Is.Null);
            Assert.That(presenter.ConfirmationAccepted, Is.False);
            Assert.That(presenter.CanBuild, Is.False);
        }

        [Test]
        public void Build_RequiresConfirmationAndConsumesPlanOnce()
        {
            var readyPlan = CreateReadyPlan("C:\\Builds");
            var buildCount = 0;
            var presenter = CreatePresenter(_ => readyPlan, plan =>
            {
                buildCount++;
                return new BuildAssistantBuildResult(true, true, BuildAssistantError.None, string.Empty, null);
            });
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();

            presenter.Build();
            Assert.That(buildCount, Is.Zero);

            presenter.SetConfirmation(true);
            presenter.Build();

            Assert.That(buildCount, Is.EqualTo(1));
            Assert.That(presenter.Plan, Is.Null);
            Assert.That(presenter.ConfirmationAccepted, Is.False);
            Assert.That(presenter.CanBuild, Is.False);

            presenter.Build();
            Assert.That(buildCount, Is.EqualTo(1));
        }

        [TestCase(BuildAssistantError.None, "Run-state cleanup will be retried.")]
        [TestCase(BuildAssistantError.ReportReadFailed, "Packed analytics could not be reduced.")]
        public void Build_SurfacesWarningsEvenWhenBuildAndHistorySucceeded(BuildAssistantError error, string warning)
        {
            var readyPlan = CreateReadyPlan("C:\\Builds");
            var presenter = CreatePresenter(_ => readyPlan, _ => new BuildAssistantBuildResult(true, true, error, warning, null));
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.Build();

            Assert.That(presenter.Message, Does.Contain("Build completed and history was saved."));
            Assert.That(presenter.Message, Does.Contain(warning));
            if (error != BuildAssistantError.None)
                Assert.That(presenter.Message, Does.Contain(error.ToString()));
        }

        [Test]
        public void FailedPreview_CannotBeConfirmed()
        {
            var failure = PlanFactory.CreateFailure(null, BuildAssistantError.NoEnabledScenes, "No enabled scenes.");
            var presenter = CreatePresenter(_ => failure);
            presenter.SetOutputRoot("C:\\Builds");

            presenter.Preview();
            presenter.SetConfirmation(true);

            Assert.That(presenter.Plan, Is.SameAs(failure));
            Assert.That(presenter.ConfirmationAccepted, Is.False);
            Assert.That(presenter.Message, Does.Contain("NoEnabledScenes"));
        }

        [Test]
        public void Export_UsesSelectedHistoryEntryAndReportsCreateNewFailure()
        {
            var plan = CreateReadyPlan("C:\\Builds");
            var entry = BuildReportReducer.CreateFailure(plan, DateTime.UtcNow, DateTime.UtcNow, BuildAssistantError.BuildInvocationFailed, "Expected test failure.").Entry;
            BuildAssistantHistoryEntry exportedEntry = null;
            string exportedPath = null;
            var presenter = new BuildAssistantPresenter(
                _ => plan,
                _ => null,
                () => new BuildAssistantHistory(new[] { entry }, false, string.Empty),
                (candidate, path) =>
                {
                    exportedEntry = candidate;
                    exportedPath = path;
                    return BuildAssistantError.OutputAlreadyExists;
                });
            presenter.RefreshHistory();

            presenter.Export("C:\\Exports\\result.json");

            Assert.That(exportedEntry, Is.SameAs(entry));
            Assert.That(exportedPath, Is.EqualTo("C:\\Exports\\result.json"));
            Assert.That(presenter.LastExportError, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
            Assert.That(presenter.ExportMessage, Does.Contain("never overwritten"));
        }

        [TestCase(0UL, "0 B")]
        [TestCase(1024UL, "1.00 KB")]
        [TestCase(1572864UL, "1.50 MB")]
        public void FormatBytes_UsesStableBinaryUnits(ulong bytes, string expected)
        {
            Assert.That(BuildAssistantPresenter.FormatBytes(bytes), Is.EqualTo(expected));
        }

        [Test]
        public void FormatDelta_HandlesLongMinimumWithoutOverflow()
        {
            Assert.That(BuildAssistantPresenter.FormatDelta(long.MinValue), Is.EqualTo("-8.00 EB"));
        }

        private static BuildAssistantPresenter CreatePresenter(Func<string, BuildAssistantPlan> preview, Func<BuildAssistantPlan, BuildAssistantBuildResult> build = null)
        {
            return new BuildAssistantPresenter(
                preview,
                build ?? (_ => new BuildAssistantBuildResult(false, false, BuildAssistantError.BuildInvocationFailed, string.Empty, null)),
                () => new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, string.Empty),
                (_, _) => BuildAssistantError.None);
        }

        private static BuildAssistantPlan CreateReadyPlan(string outputRoot)
        {
            var profile = new ProfileSnapshot(BuildAssistantProfileKind.Platform, string.Empty, "Standalone Platform", string.Empty, string.Empty, "platform:Standalone");
            var scenes = new[] { new BuildAssistantScene(0, "scene-guid", "Assets/Main.unity", true, "dependency-hash") };
            var environment = new EnvironmentSnapshot(profile, BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Standalone", 0, ScriptingImplementation.IL2CPP, BuildOptions.DetailedBuildReport, string.Empty, Array.Empty<string>(), new[] { "BUILD_ASSISTANT_TEST" }, scenes);
            var context = new PlanningContext(environment, outputRoot, OutputRootMode.ExistingDirectory, new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc), "1234abcd", false, null);
            return PlanFactory.Create(context);
        }
    }
}
