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
                "\u2460 ビルド設定",
                "\u2461 出力先",
                "\u2462 計画作成",
                "\u2463 実行確認",
                "\u2464 実行結果と書き出し"
            }));
            Assert.That(BuildAssistantWindow.MinimumWidth, Is.EqualTo(620f));
            Assert.That(BuildAssistantWindow.MinimumHeight, Is.EqualTo(480f));
            Assert.That(BuildAssistantWindow.SectionCardSpacing, Is.GreaterThan(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildAssistantWindow.GetSectionHeading(BuildAssistantWindow.SectionCardCount));
            Assert.That(BuildAssistantWindow.WindowTitle, Is.EqualTo("ビルド実行アシスタント"));
            Assert.That(BuildAssistantMenu.MenuPath, Is.EqualTo("Tools/ビルド実行アシスタント/開く"));
        }

        [Test]
        public void OutputHelp_StatesLocalDriveOnlyConstraint()
        {
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("ローカルドライブ"));
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("UNC"));
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("ネットワーク"));
            Assert.That(BuildAssistantWindow.OutputHelpText, Does.Contain("割り当てドライブ"));
        }

        [Test]
        public void ProfileHelp_DistinguishesEditorTargetFromCustomProfileAuthority()
        {
            Assert.That(BuildAssistantWindow.EditorTargetLabel, Is.EqualTo("エディターで選択中の対象機種"));
            Assert.That(BuildAssistantWindow.ProfileHelpText, Does.Contain("独自のビルドプロファイル"));
            Assert.That(BuildAssistantWindow.ProfileHelpText, Does.Contain("一致している必要があります"));
            Assert.That(BuildAssistantWindow.ProfileHelpText, Does.Contain("コンパイル完了後"));
            Assert.That(BuildAssistantWindow.ProfileHelpText, Does.Contain("切り替えません"));
            Assert.That(BuildAssistantWindow.InputFingerprintLabel, Is.EqualTo("取得した入力照合値"));
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

        [Test]
        public void Build_UnexpectedExceptionDoesNotExposeItsEnglishDiagnostic()
        {
            var readyPlan = CreateReadyPlan("C:\\Builds");
            var presenter = CreatePresenter(_ => readyPlan, _ => throw new InvalidOperationException("Legacy English diagnostic."));
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.Build();

            Assert.That(presenter.Result.Error, Is.EqualTo(BuildAssistantError.BuildInvocationFailed));
            Assert.That(presenter.Result.Message, Does.Contain("予期しない問題"));
            Assert.That(presenter.Result.Message, Does.Not.Contain("Legacy English"));
            Assert.That(presenter.Message, Does.Not.Contain("Legacy English"));
        }

        [TestCase(BuildAssistantError.None, "実行状態の後片付けを次回に再試行します。")]
        [TestCase(BuildAssistantError.ReportReadFailed, "格納容量の集計を完了できませんでした。")]
        public void Build_SurfacesWarningsEvenWhenBuildAndHistorySucceeded(BuildAssistantError error, string warning)
        {
            var readyPlan = CreateReadyPlan("C:\\Builds");
            var presenter = CreatePresenter(_ => readyPlan, _ => new BuildAssistantBuildResult(true, true, error, warning, null));
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.Build();

            Assert.That(presenter.Message, Does.Contain("ビルドが完了し、履歴を保存しました"));
            Assert.That(presenter.Message, Does.Not.Contain(warning));
            Assert.That(presenter.Message, Does.Not.Contain(error.ToString()));
            Assert.That(presenter.Message, Does.Contain(error == BuildAssistantError.None ? "注意事項" : "集計情報"));
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
            Assert.That(presenter.Message, Does.Contain("有効なシーン"));
            Assert.That(presenter.Message, Does.Not.Contain("NoEnabledScenes"));
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
            Assert.That(presenter.ExportMessage, Does.Contain("既存ファイルは上書きしません"));
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

        [Test]
        public void VisibleEnumValues_AreFormattedInJapaneseWithoutRawNames()
        {
            Assert.That(BuildAssistantPresenter.FormatProfileKind(BuildAssistantProfileKind.Platform), Is.EqualTo("プラットフォーム設定"));
            Assert.That(BuildAssistantPresenter.FormatTarget(BuildTarget.StandaloneWindows64), Is.EqualTo("Windows 64ビット"));
            Assert.That(BuildAssistantPresenter.FormatScriptingBackend(ScriptingImplementation.IL2CPP), Is.EqualTo("IL2CPP"));
            Assert.That(BuildAssistantPresenter.FormatBuildOptions(BuildOptions.Development | BuildOptions.DetailedBuildReport), Is.EqualTo("詳細報告、開発用"));
            Assert.That(BuildAssistantPresenter.FormatHistoryStatus(BuildAssistantHistoryStatus.Interrupted), Is.EqualTo("中断"));
        }

        [Test]
        public void HistoryMessage_HidesLegacyEnglishDiagnostic()
        {
            var entry = BuildReportReducer.CreateFailure(CreateReadyPlan("C:\\Builds"), DateTime.UtcNow, DateTime.UtcNow, BuildAssistantError.BuildInvocationFailed, "Legacy English diagnostic.").Entry;

            var message = BuildAssistantPresenter.FormatHistoryMessage(entry);

            Assert.That(message, Does.Contain("Unityがプレイヤービルドを完了できませんでした"));
            Assert.That(message, Does.Not.Contain("Legacy English diagnostic"));
        }

        [Test]
        public void UnsavedCurrentResult_RemainsPrimaryWhenHistoryContainsTheSameRunId()
        {
            var plan = CreateReadyPlan("C:\\Builds");
            var terminalEntry = BuildAssistantTestData.Entry(plan.RunId, BuildAssistantHistoryStatus.Succeeded);
            var staleHistoryEntry = BuildAssistantTestData.Entry(plan.RunId, BuildAssistantHistoryStatus.Interrupted);
            var result = new BuildAssistantBuildResult(true, false, BuildAssistantError.HistoryWriteFailed, "履歴保存失敗", terminalEntry);
            var presenter = new BuildAssistantPresenter(
                _ => plan,
                _ => result,
                () => new BuildAssistantHistory(new[] { staleHistoryEntry }, false, string.Empty),
                (_, _) => BuildAssistantError.None);
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.Build();

            Assert.That(presenter.SelectedHistoryIndex, Is.EqualTo(-1));
            Assert.That(presenter.ExportEntry, Is.SameAs(terminalEntry));
            presenter.RefreshHistory();
            Assert.That(presenter.SelectedHistoryIndex, Is.EqualTo(-1));
            Assert.That(presenter.ExportEntry, Is.SameAs(terminalEntry));
            Assert.That(BuildAssistantWindow.IsCurrentResultNotSaved(result), Is.True);
            Assert.That(BuildAssistantWindow.IsPersistedResultSelected(result, staleHistoryEntry), Is.False);
        }

        [Test]
        public void UnsavedCurrentResult_RemainsPrimaryWhenPackedMetricsDiffer()
        {
            var plan = CreateReadyPlan("C:\\Builds");
            var terminalEntry = BuildAssistantTestData.Entry(plan.RunId, BuildAssistantHistoryStatus.Succeeded, packedBytes: 80);
            var differentHistoryEntry = BuildAssistantTestData.Entry(plan.RunId, BuildAssistantHistoryStatus.Succeeded, packedBytes: 79);
            var result = new BuildAssistantBuildResult(true, false, BuildAssistantError.HistoryWriteFailed, "履歴保存失敗", terminalEntry);
            var presenter = new BuildAssistantPresenter(
                _ => plan,
                _ => result,
                () => new BuildAssistantHistory(new[] { differentHistoryEntry }, false, string.Empty),
                (_, _) => BuildAssistantError.None);
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.Build();

            Assert.That(BuildAssistantPresenter.IsSameTerminalResult(differentHistoryEntry, terminalEntry), Is.False);
            Assert.That(presenter.Result.HistoryPersisted, Is.False);
            Assert.That(presenter.SelectedHistoryIndex, Is.EqualTo(-1));
            Assert.That(presenter.ExportEntry, Is.SameAs(terminalEntry));
        }

        [Test]
        public void RecoveredTerminalResult_IsRecognizedAsPersistedAfterHistoryRefresh()
        {
            var plan = CreateReadyPlan("C:\\Builds");
            var terminalEntry = BuildAssistantTestData.Entry(plan.RunId, BuildAssistantHistoryStatus.Succeeded);
            var initialResult = new BuildAssistantBuildResult(true, false, BuildAssistantError.HistoryWriteFailed, "履歴保存失敗", terminalEntry);
            var presenter = new BuildAssistantPresenter(
                _ => plan,
                _ => initialResult,
                () => new BuildAssistantHistory(new[] { terminalEntry }, false, string.Empty),
                (_, _) => BuildAssistantError.None);
            presenter.SetOutputRoot("C:\\Builds");
            presenter.Preview();
            presenter.SetConfirmation(true);

            presenter.Build();

            Assert.That(presenter.Result.HistoryPersisted, Is.True);
            Assert.That(presenter.SelectedHistoryIndex, Is.EqualTo(0));
            Assert.That(presenter.Message, Does.Contain("履歴を保存しました"));
            Assert.That(BuildAssistantWindow.IsCurrentResultNotSaved(presenter.Result), Is.False);
            Assert.That(BuildAssistantWindow.IsPersistedResultSelected(presenter.Result, terminalEntry), Is.True);
        }

        [Test]
        public void HistoryNotice_HidesStoredDiagnosticText()
        {
            var history = new BuildAssistantHistory(Array.Empty<BuildAssistantHistoryEntry>(), false, "Legacy English history diagnostic.");

            var notice = BuildAssistantPresenter.FormatHistoryNotice(history);

            Assert.That(notice, Does.Contain("履歴の読み込み"));
            Assert.That(notice, Does.Not.Contain("Legacy English"));
        }

        [Test]
        public void TargetMismatchGuidance_ExplainsTheRequiredSwitchAndRecompile()
        {
            var message = BuildAssistantPresenter.FormatError(BuildAssistantError.BuildTargetMismatch, string.Empty);

            Assert.That(message, Does.Contain("同じ対象へ切り替え"));
            Assert.That(message, Does.Contain("コンパイル完了後"));
            Assert.That(message, Does.Not.Contain(nameof(BuildAssistantError.BuildTargetMismatch)));
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
            var profile = new ProfileSnapshot(BuildAssistantProfileKind.Platform, string.Empty, "デスクトップ向けプラットフォーム設定", string.Empty, string.Empty, "platform:Standalone");
            var scenes = new[] { new BuildAssistantScene(0, "scene-guid", "Assets/Main.unity", true, "dependency-hash") };
            var environment = new EnvironmentSnapshot(profile, BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Standalone", 0, ScriptingImplementation.IL2CPP, BuildOptions.DetailedBuildReport, string.Empty, Array.Empty<string>(), new[] { "BUILD_ASSISTANT_TEST" }, scenes);
            var context = new PlanningContext(environment, outputRoot, OutputRootMode.ExistingDirectory, new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc), "1234abcd", false, null);
            return PlanFactory.Create(context);
        }
    }
}
