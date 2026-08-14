using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DiagnosticsContext.Samples.Tests.PlayMode
{
    /// <summary>Import済みBasics sampleの実Button、report、thread capture、表示寸法を検証する。</summary>
    [Parallelizable(ParallelScope.None)]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
    public sealed class DiagnosticsContextBasicsButtonTests
    {
        /// <summary>公開sampleと同じ横長画面を再現する幅。</summary>
        private const int TargetWidth = 960;

        /// <summary>公開sampleと同じ横長画面を再現する高さ。</summary>
        private const int TargetHeight = 600;

        /// <summary>低く狭い実用画面を再現する幅。</summary>
        private const int NarrowTargetWidth = 640;

        /// <summary>低く狭い実用画面を再現する高さ。</summary>
        private const int NarrowTargetHeight = 360;

        /// <summary>描画計算の小数誤差として許容するpixel数。</summary>
        private const float GeometryTolerance = 0.75f;

        /// <summary>textとButtonをcard端から離す最低検証余白。</summary>
        private const float MinimumCardInset = 4f;

        /// <summary>Package配置とimport後のAssets配置の両方から配布PanelSettingsを特定するGUID。</summary>
        private const string PanelSettingsAssetGuid = "157f79a149bf445c9c522f6c74d751dc";

        /// <summary>UIDocumentとsample controllerを同じ寿命で所有するGameObject。</summary>
        private GameObject _host;

        /// <summary>安定した名前で実Buttonと表示Labelを取得するUIDocument。</summary>
        private UIDocument _document;

        /// <summary>Button callbackとService寿命を所有するsample controller。</summary>
        private DiagnosticsContextBasicsController _sample;

        /// <summary>UIDocumentへ実panelを割り当てるtest用設定。</summary>
        private PanelSettings _panelSettings;

        /// <summary>画面寸法を固定して実panelを描画するtest用texture。</summary>
        private RenderTexture _targetTexture;

        /// <summary>testが今回生成し、終了時にだけ削除するreport path。</summary>
        private readonly List<string> _createdReportPaths = new List<string>();

        /// <summary>test開始前から存在した一時fileを誤って今回の残骸と扱わない集合。</summary>
        private HashSet<string> _temporaryFilesBefore;

        /// <summary>手動書出し前から存在したreportを自動生成と誤判定しない集合。</summary>
        private HashSet<string> _reportFilesBefore;

        /// <summary>実panelとsample画面を作り、responsive callbackを2回描画させる。</summary>
        [UnitySetUp]
        public IEnumerator CreateSampleView()
        {
            _createdReportPaths.Clear();
            _temporaryFilesBefore = FindTemporaryFiles();
            _reportFilesBefore = FindReportFiles();
            _targetTexture = new RenderTexture(TargetWidth, TargetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "Diagnostics Context Basics Button Tests Target" };
            Assert.That(_targetTexture.Create(), Is.True, "PlayMode検証用RenderTextureを作れません。");

            _panelSettings = InstantiateShippedPanelSettings();
            _panelSettings.targetTexture = _targetTexture;
            _host = new GameObject("Diagnostics Context Basics Button Tests");
            _host.SetActive(false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _sample = _host.AddComponent<DiagnosticsContextBasicsController>();
            _host.SetActive(true);

            yield return WaitUntil(
                () => _sample.ServiceAvailable && FindElement<VisualElement>(DiagnosticsContextBasicsController.ReadyElementName) is { } ready && ready.worldBound.width > 0f && Mathf.Abs(_document.rootVisualElement.contentRect.width - TargetWidth) <= GeometryTolerance && Mathf.Abs(_document.rootVisualElement.contentRect.height - TargetHeight) <= GeometryTolerance,
                3d,
                "960x600の実RenderTexture panelとServiceが3秒以内に準備されませんでした。");
            yield return null;
            yield return null;
        }

        /// <summary>ServiceとGameObjectを終了し、今回成功したreportだけを削除する。</summary>
        [UnityTearDown]
        public IEnumerator DestroySampleView()
        {
            if (_host != null) UnityEngine.Object.Destroy(_host);
            yield return null;

            for (var index = 0; index < _createdReportPaths.Count; index++)
            {
                var reportPath = _createdReportPaths[index];
                if (!string.IsNullOrEmpty(reportPath) && File.Exists(reportPath)) File.Delete(reportPath);
            }

            if (_panelSettings != null) UnityEngine.Object.DestroyImmediate(_panelSettings);
            if (_targetTexture != null)
            {
                _targetTexture.Release();
                UnityEngine.Object.DestroyImmediate(_targetTexture);
            }

            _host = null;
            _document = null;
            _sample = null;
            _panelSettings = null;
            _targetTexture = null;
            _createdReportPaths.Clear();
            _temporaryFilesBefore = null;
            _reportFilesBefore = null;
        }

        /// <summary>privacy、manual境界、件数、結果、path、全Buttonが安定名で存在することを確かめる。</summary>
        [UnityTest]
        public IEnumerator ReadyView_ContainsStableControlsAndExplicitBoundaries()
        {
            Assert.That(FindElement<Button>(DiagnosticsContextBasicsController.AddContextButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(DiagnosticsContextBasicsController.AddBreadcrumbButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(DiagnosticsContextBasicsController.EmitWarningButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(DiagnosticsContextBasicsController.WriteReportButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Button>(DiagnosticsContextBasicsController.RecreateButtonElementName), Is.Not.Null);
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.TitleElementName), Is.Not.Null);
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.BadgeElementName), Is.Not.Null);
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.DescriptionElementName), Is.Not.Null);

            var privacy = FindElement<Label>(DiagnosticsContextBasicsController.PrivacyElementName);
            var manualBoundary = FindElement<Label>(DiagnosticsContextBasicsController.ManualBoundaryElementName);
            var status = FindElement<Label>(DiagnosticsContextBasicsController.StatusElementName);
            var result = FindElement<Label>(DiagnosticsContextBasicsController.ResultElementName);
            var path = FindElement<Label>(DiagnosticsContextBasicsController.ReportPathElementName);
            Assert.That(privacy.text, Does.Contain("opt-in"));
            Assert.That(privacy.text, Does.Contain("自動追加しません"));
            Assert.That(privacy.text, Does.Contain("token"));
            Assert.That(manualBoundary.text, Does.Contain("crash後の生存保証なし"));
            Assert.That(manualBoundary.text, Does.Contain("uploadなし"));
            Assert.That(status.text, Does.Contain("Owner: active"));
            Assert.That(result.text, Does.Contain("TryCreate: None"));
            Assert.That(path.text, Does.Contain("未作成"));
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.ContextCountElementName).text, Does.EndWith("0"));
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.BreadcrumbCountElementName).text, Does.EndWith("0"));
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.LogCountElementName).text, Does.EndWith("0"));
            yield break;
        }

        /// <summary>実Buttonで情報とworker Warningを追加し、手動reportのJSONと保存境界を確かめる。</summary>
        [UnityTest]
        public IEnumerator Buttons_WriteParseableContainedReport_WithoutTemporaryFile()
        {
            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.AddContextButtonElementName));
            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.AddBreadcrumbButtonElementName));
            Assert.That(_sample.ContextEntryCount, Is.EqualTo(1));
            Assert.That(_sample.BreadcrumbCount, Is.EqualTo(1));

            var mainWarning = DiagnosticsContextBasicsController.SampleWarningPrefix + "01";
            var logsBeforeMainWarning = _sample.CapturedLogCount;
            LogAssert.Expect(LogType.Warning, mainWarning);
            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.EmitWarningButtonElementName));
            yield return WaitUntil(() => _sample.CapturedLogCount >= logsBeforeMainWarning + 1, 3d, "main thread Warningがlive subscriptionへ記録されませんでした。");

            var workerWarning = "[Diagnostics Context Basics Tests] Worker warning";
            var logsBeforeWorkerWarning = _sample.CapturedLogCount;
            Exception workerException = null;
            LogAssert.Expect(LogType.Warning, workerWarning);
            var worker = new Thread(() =>
            {
                try
                {
                    Debug.LogWarning(workerWarning);
                }
                catch (Exception exception)
                {
                    workerException = exception;
                }
            });
            worker.Start();
            yield return WaitUntil(() => !worker.IsAlive, 3d, "worker threadのWarning発行が3秒以内に完了しませんでした。");
            Assert.That(workerException, Is.Null, workerException?.ToString());
            Assert.That(_sample.CapturedLogCount, Is.GreaterThanOrEqualTo(logsBeforeWorkerWarning + 1), "既にcallbackへ届いたworker Warningが同期的に記録されませんでした。");
            Assert.That(FindReportFiles().SetEquals(_reportFilesBefore), Is.True, "context、breadcrumb、log取得だけで手動要求前にreportが作成されました。");

            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.WriteReportButtonElementName));
            yield return WaitUntil(() => !string.IsNullOrEmpty(_sample.LastReportPath) && File.Exists(_sample.LastReportPath), 3d, "Write Report Buttonが3秒以内にreportを保存しませんでした。");

            var reportPath = Path.GetFullPath(_sample.LastReportPath);
            _createdReportPaths.Add(reportPath);
            var diagnosticsDirectory = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "DiagnosticsContext"));
            var diagnosticsPrefix = diagnosticsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Assert.That(reportPath.StartsWith(diagnosticsPrefix, comparison), Is.True, $"reportが専用directory外へ出ています: {reportPath}");
            Assert.That(Path.GetFileName(reportPath).IndexOf("reason-only-7f3b", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1), "reason固有値がfile名へ使われています。");
            Assert.That(Path.GetExtension(reportPath), Is.EqualTo(".json"));

            var json = File.ReadAllText(reportPath);
            Assert.That(json, Does.Not.Contain(Path.GetFileName(reportPath)), "画面へ表示する保存pathがreport JSON自身へ混入しています。");
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.ReportPathElementName).text, Does.Contain(reportPath), "成功したreport pathが画面へ反映されていません。");
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.ResultElementName).text, Does.Contain("Write Report:").And.Contain("bytes"), "成功したreport byte数が直近結果へ反映されていません。");
            var report = JsonUtility.FromJson<ReportDocument>(json);
            Assert.That(report, Is.Not.Null);
            Assert.That(report.schemaVersion, Is.EqualTo(1));
            Assert.That(report.reason, Is.EqualTo(DiagnosticsContextBasicsController.SampleReportReason));
            Assert.That(report.createdUtc, Is.Not.Empty);
            Assert.That(DateTimeOffset.TryParse(report.createdUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdUtc), Is.True);
            Assert.That(createdUtc.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(report.context, Has.Length.EqualTo(1));
            Assert.That(report.context[0].key, Is.EqualTo("sample.context.01"));
            Assert.That(report.context[0].value, Is.EqualTo("opt-in-value-01"));
            Assert.That(report.breadcrumbs, Has.Length.EqualTo(1));
            Assert.That(report.breadcrumbs[0].message, Is.EqualTo("Sample action 01"));
            Assert.That(report.logs.Any(item => item.message == mainWarning && item.type == "Warning"), Is.True, "main thread Warningがreportにありません。");
            Assert.That(report.logs.Any(item => item.message == workerWarning && item.type == "Warning"), Is.True, "worker Warningがreportにありません。");

            var newTemporaryFiles = FindTemporaryFiles().Except(_temporaryFilesBefore, StringComparer.OrdinalIgnoreCase).ToArray();
            Assert.That(newTemporaryFiles, Is.Empty, $"成功後に一時fileが残っています: {string.Join(", ", newTemporaryFiles)}");
        }

        /// <summary>Dispose / Recreate後は件数を引継がず、新Serviceの実Buttonが利用できることを確かめる。</summary>
        [UnityTest]
        public IEnumerator RecreateButton_EndsOldOwner_AndStartsEmptyUsableOwner()
        {
            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.AddContextButtonElementName));
            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.AddBreadcrumbButtonElementName));
            Assert.That(_sample.ContextEntryCount, Is.EqualTo(1));
            Assert.That(_sample.BreadcrumbCount, Is.EqualTo(1));

            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.RecreateButtonElementName));
            Assert.That(_sample.ServiceAvailable, Is.True);
            Assert.That(_sample.ContextEntryCount, Is.Zero);
            Assert.That(_sample.BreadcrumbCount, Is.Zero);
            Assert.That(_sample.CapturedLogCount, Is.Zero);
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.ResultElementName).text, Does.Contain("Dispose -> TryCreate: None"));

            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.AddContextButtonElementName));
            Assert.That(_sample.ContextEntryCount, Is.EqualTo(1));
            Assert.That(FindElement<Label>(DiagnosticsContextBasicsController.ContextCountElementName).text, Does.EndWith("1"));
            var warningAfterRecreate = DiagnosticsContextBasicsController.SampleWarningPrefix + "01";
            LogAssert.Expect(LogType.Warning, warningAfterRecreate);
            InvokeBoundClick(FindElement<Button>(DiagnosticsContextBasicsController.EmitWarningButtonElementName));
            Assert.That(_sample.CapturedLogCount, Is.EqualTo(1), "再作成後の1 Warningが重複subscriptionで複数回取得されました。");
            yield break;
        }

        /// <summary>960x600と640x360で全Buttonとtextがcard内へ収まり、同種要素が重ならないことを確かめる。</summary>
        [UnityTest]
        public IEnumerator ReadyView_KeepsInteractiveAndTextGeometryInsideCardAtWideAndCompactSizes()
        {
            yield return WaitForResolvedGeometry(TargetWidth, TargetHeight);
            AssertAllGeometry(TargetWidth, TargetHeight, true);

            ReplaceTargetTexture(NarrowTargetWidth, NarrowTargetHeight);
            yield return WaitForResolvedGeometry(NarrowTargetWidth, NarrowTargetHeight);
            AssertAllGeometry(NarrowTargetWidth, NarrowTargetHeight, false);
        }

        /// <summary>安定した要素名を使ってUIDocumentから指定型を取得する。</summary>
        /// <typeparam name="T">Button、Label、VisualElementのいずれか。</typeparam>
        /// <param name="elementName">sample controllerが公開する安定した要素名。</param>
        /// <returns>一致した要素。見つからない場合はnull。</returns>
        private T FindElement<T>(string elementName) where T : VisualElement => _document?.rootVisualElement?.Q<T>(elementName);

        /// <summary>画面上の並び順で5つの操作Buttonを取得する。</summary>
        /// <returns>Add ContextからDispose / RecreateまでのButton。</returns>
        private Button[] FindButtons() => new[] { FindElement<Button>(DiagnosticsContextBasicsController.AddContextButtonElementName), FindElement<Button>(DiagnosticsContextBasicsController.AddBreadcrumbButtonElementName), FindElement<Button>(DiagnosticsContextBasicsController.EmitWarningButtonElementName), FindElement<Button>(DiagnosticsContextBasicsController.WriteReportButtonElementName), FindElement<Button>(DiagnosticsContextBasicsController.RecreateButtonElementName) };

        /// <summary>2 panel更新後にcard、Button、LabelのworldBoundが正になるまで待つ。</summary>
        /// <returns>PlayModeのframeごとに進むcoroutine。</returns>
        /// <param name="expectedWidth">実RenderTexture panelの期待幅。</param>
        /// <param name="expectedHeight">実RenderTexture panelの期待高さ。</param>
        private IEnumerator WaitForResolvedGeometry(float expectedWidth, float expectedHeight)
        {
            yield return null;
            yield return null;
            yield return WaitUntil(
                () => Mathf.Abs(_document.rootVisualElement.contentRect.width - expectedWidth) <= GeometryTolerance && Mathf.Abs(_document.rootVisualElement.contentRect.height - expectedHeight) <= GeometryTolerance && FindElement<VisualElement>(DiagnosticsContextBasicsController.CardElementName) is { } card && card.worldBound.width > 0f && FindButtons().All(button => button != null && button.worldBound.width > 0f && button.worldBound.height > 0f),
                3d,
                "Diagnostics Context Basicsの表示寸法が3秒以内に確定しませんでした。");
            yield return null;
            yield return null;
        }

        /// <summary>指定画面でcardと全操作・text要素のcontainmentと重なりを確かめる。</summary>
        /// <param name="expectedWidth">rootの期待幅。</param>
        /// <param name="expectedHeight">rootの期待高さ。</param>
        /// <param name="requiresSingleButtonRow">Buttonが1行である必要がある場合はtrue。</param>
        private void AssertAllGeometry(float expectedWidth, float expectedHeight, bool requiresSingleButtonRow)
        {
            var documentRoot = _document.rootVisualElement;
            var card = FindElement<VisualElement>(DiagnosticsContextBasicsController.CardElementName);
            var buttonRow = FindElement<VisualElement>(DiagnosticsContextBasicsController.ButtonRowElementName);
            var buttons = FindButtons();
            var labels = card.Query<Label>().ToList().ToArray();
            Assert.That(documentRoot.contentRect.width, Is.EqualTo(expectedWidth).Within(GeometryTolerance));
            Assert.That(documentRoot.contentRect.height, Is.EqualTo(expectedHeight).Within(GeometryTolerance));
            Assert.That(_panelSettings.targetTexture.width, Is.EqualTo((int)expectedWidth));
            Assert.That(_panelSettings.targetTexture.height, Is.EqualTo((int)expectedHeight));
            Assert.That(card.worldBound.xMin, Is.GreaterThanOrEqualTo(documentRoot.worldBound.xMin - GeometryTolerance));
            Assert.That(card.worldBound.xMax, Is.LessThanOrEqualTo(documentRoot.worldBound.xMax + GeometryTolerance));
            Assert.That(card.worldBound.yMin, Is.GreaterThanOrEqualTo(documentRoot.worldBound.yMin - GeometryTolerance));
            Assert.That(card.worldBound.yMax, Is.LessThanOrEqualTo(documentRoot.worldBound.yMax + GeometryTolerance));
            Assert.That(buttonRow.worldBound.height, Is.GreaterThan(0f));

            var safeBounds = new Rect(card.worldBound.xMin + MinimumCardInset, card.worldBound.yMin + MinimumCardInset, card.worldBound.width - (MinimumCardInset * 2f), card.worldBound.height - (MinimumCardInset * 2f));
            AssertContainedAndPositive(buttons.Cast<VisualElement>().ToArray(), safeBounds, "Button");
            AssertContainedAndPositive(labels.Cast<VisualElement>().ToArray(), safeBounds, "Label");
            AssertNoOverlap(buttons.Cast<VisualElement>().ToArray(), "Button");
            AssertNoOverlap(labels.Cast<VisualElement>().ToArray(), "Label");
            AssertCrossTypeNoOverlap(buttons.Cast<VisualElement>().ToArray(), labels.Cast<VisualElement>().ToArray());
            AssertVerticalSectionOrder(buttonRow);

            var firstButtonY = buttons[0].worldBound.yMin;
            if (requiresSingleButtonRow)
            {
                for (var index = 1; index < buttons.Length; index++) Assert.That(buttons[index].worldBound.yMin, Is.EqualTo(firstButtonY).Within(GeometryTolerance), $"{buttons[index].name}が960x600の操作行から外れています。");
            }
            else
            {
                Assert.That(buttons.Any(button => button.worldBound.yMin > firstButtonY + GeometryTolerance), Is.True, "640x360でButtonが読みやすい複数行へ折り返されませんでした。");
            }
        }

        /// <summary>対象要素が正の寸法を持ち、card安全領域から出ないことを確かめる。</summary>
        /// <param name="elements">検証するButtonまたはLabel。</param>
        /// <param name="safeBounds">card端から最低余白を除いた領域。</param>
        /// <param name="kind">失敗messageへ含める要素種類。</param>
        private static void AssertContainedAndPositive(VisualElement[] elements, Rect safeBounds, string kind)
        {
            for (var index = 0; index < elements.Length; index++)
            {
                var bounds = elements[index].worldBound;
                var description = DescribeElement(elements[index]);
                Assert.That(bounds.width, Is.GreaterThan(0f), $"{kind} {description}の幅が確定していません。");
                Assert.That(bounds.height, Is.GreaterThan(0f), $"{kind} {description}の高さが確定していません。");
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safeBounds.xMin - GeometryTolerance), $"{kind} {description}がcard左端を越えています。safe={FormatRect(safeBounds)}");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safeBounds.xMax + GeometryTolerance), $"{kind} {description}がcard右端を越えています。safe={FormatRect(safeBounds)}");
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(safeBounds.yMin - GeometryTolerance), $"{kind} {description}がcard上端を越えています。safe={FormatRect(safeBounds)}");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safeBounds.yMax + GeometryTolerance), $"{kind} {description}がcard下端を越えています。safe={FormatRect(safeBounds)}");
            }
        }

        /// <summary>同種要素が表示上の面積を共有していないことを確かめる。</summary>
        /// <param name="elements">同じ種類の表示要素。</param>
        /// <param name="kind">失敗messageへ含める要素種類。</param>
        private static void AssertNoOverlap(VisualElement[] elements, string kind)
        {
            for (var index = 0; index < elements.Length; index++)
            {
                for (var otherIndex = index + 1; otherIndex < elements.Length; otherIndex++) Assert.That(elements[index].worldBound.Overlaps(elements[otherIndex].worldBound), Is.False, $"{kind} {DescribeElement(elements[index])}と{DescribeElement(elements[otherIndex])}が重なっています。");
            }
        }

        /// <summary>操作Buttonと文字Labelが互いの表示面積へ重ならないことを確かめる。</summary>
        /// <param name="buttons">すべての操作Button。</param>
        /// <param name="labels">card内のすべてのLabel。</param>
        private static void AssertCrossTypeNoOverlap(VisualElement[] buttons, VisualElement[] labels)
        {
            for (var buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
            {
                for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++) Assert.That(buttons[buttonIndex].worldBound.Overlaps(labels[labelIndex].worldBound), Is.False, $"Button {DescribeElement(buttons[buttonIndex])}とLabel {DescribeElement(labels[labelIndex])}が重なっています。");
            }
        }

        /// <summary>PanelSettingsが参照する実RenderTextureを指定viewport寸法へ置き換える。</summary>
        /// <param name="width">新しいpanel幅。</param>
        /// <param name="height">新しいpanel高さ。</param>
        private void ReplaceTargetTexture(int width, int height)
        {
            var replacement = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = $"Diagnostics Context Basics {width}x{height} Target" };
            Assert.That(replacement.Create(), Is.True, $"{width}x{height}のPlayMode検証用RenderTextureを作れません。");
            var previous = _targetTexture;
            _targetTexture = replacement;
            _panelSettings.targetTexture = replacement;
            if (previous == null) return;
            previous.Release();
            UnityEngine.Object.DestroyImmediate(previous);
        }

        /// <summary>配布sampleと同じtheme・scale設定を持つPanelSettings複製を作る。</summary>
        /// <returns>testが終了時に破棄するPanelSettings複製。</returns>
        private static PanelSettings InstantiateShippedPanelSettings()
        {
#if UNITY_EDITOR
            var panelSettingsPath = AssetDatabase.GUIDToAssetPath(PanelSettingsAssetGuid);
            Assert.That(panelSettingsPath, Is.Not.Empty, "Diagnostics Context BasicsのPanelSettingsをGUIDから解決できません。");
            var shippedPanelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
            Assert.That(shippedPanelSettings, Is.Not.Null, $"Diagnostics Context BasicsのPanelSettingsを読込めません: {panelSettingsPath}");
            Assert.That(shippedPanelSettings.themeStyleSheet, Is.Not.Null, $"配布PanelSettingsにTheme Style Sheetがありません: {panelSettingsPath}");
            var panelSettingsClone = UnityEngine.Object.Instantiate(shippedPanelSettings);
            Assert.That(panelSettingsClone, Is.Not.Null, "Diagnostics Context BasicsのPanelSettingsを複製できません。");
            Assert.That(panelSettingsClone.themeStyleSheet, Is.Not.Null, "複製したPanelSettingsにTheme Style Sheetがありません。");
            Assert.That(panelSettingsClone.scaleMode, Is.EqualTo(PanelScaleMode.ConstantPixelSize), "配布PanelSettingsがConstant Pixel Sizeではありません。");
            return panelSettingsClone;
#else
            Assert.Fail("Diagnostics Context Basicsのsample PlayModeテストはUnity Editor内でだけ実行できます。");
            return null;
#endif
        }

        /// <summary>要素名、表示文字、実測worldBoundを失敗message向けに整える。</summary>
        /// <param name="element">ButtonまたはLabel。</param>
        /// <returns>要素を一意に判断できる短い説明。</returns>
        private static string DescribeElement(VisualElement element)
        {
            var text = element is TextElement textElement ? textElement.text : string.Empty;
            if (text.Length > 48) text = text.Substring(0, 48);
            return string.Format(CultureInfo.InvariantCulture, "{0} text=\"{1}\" bounds={2}", element.name, text.Replace('\n', ' '), FormatRect(element.worldBound));
        }

        /// <summary>矩形をculture非依存の端座標へ整える。</summary>
        /// <param name="rect">表示または安全領域の矩形。</param>
        /// <returns>左、上、右、下を含む文字列。</returns>
        private static string FormatRect(Rect rect) => string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1}..{2:F1},{3:F1}]", rect.xMin, rect.yMin, rect.xMax, rect.yMax);

        /// <summary>titleからmanual境界までの全sectionが上から順に分離されることを確かめる。</summary>
        /// <param name="buttonRow">5つの操作Buttonを含むsection。</param>
        private void AssertVerticalSectionOrder(VisualElement buttonRow)
        {
            var title = FindElement<Label>(DiagnosticsContextBasicsController.TitleElementName);
            var badge = FindElement<Label>(DiagnosticsContextBasicsController.BadgeElementName);
            var description = FindElement<Label>(DiagnosticsContextBasicsController.DescriptionElementName);
            var privacy = FindElement<Label>(DiagnosticsContextBasicsController.PrivacyElementName);
            var contextMetric = FindElement<Label>(DiagnosticsContextBasicsController.ContextCountElementName);
            var status = FindElement<Label>(DiagnosticsContextBasicsController.StatusElementName);
            var result = FindElement<Label>(DiagnosticsContextBasicsController.ResultElementName);
            var reportPath = FindElement<Label>(DiagnosticsContextBasicsController.ReportPathElementName);
            var manualBoundary = FindElement<Label>(DiagnosticsContextBasicsController.ManualBoundaryElementName);
            var titleRowBottom = Mathf.Max(title.worldBound.yMax, badge.worldBound.yMax);
            Assert.That(description.worldBound.yMin, Is.GreaterThanOrEqualTo(titleRowBottom - GeometryTolerance), "機能説明がtitle行へ重なっています。");
            Assert.That(privacy.worldBound.yMin, Is.GreaterThanOrEqualTo(description.worldBound.yMax - GeometryTolerance), "privacy表示が機能説明へ重なっています。");
            Assert.That(contextMetric.worldBound.yMin, Is.GreaterThanOrEqualTo(privacy.worldBound.yMax - GeometryTolerance), "件数sectionがprivacy表示へ重なっています。");
            Assert.That(status.worldBound.yMin, Is.GreaterThanOrEqualTo(contextMetric.worldBound.yMax - GeometryTolerance), "状態表示が件数sectionへ重なっています。");
            Assert.That(result.worldBound.yMin, Is.GreaterThanOrEqualTo(status.worldBound.yMax - GeometryTolerance), "結果表示が状態表示へ重なっています。");
            Assert.That(reportPath.worldBound.yMin, Is.GreaterThanOrEqualTo(result.worldBound.yMax - GeometryTolerance), "report pathが結果表示へ重なっています。");
            Assert.That(buttonRow.worldBound.yMin, Is.GreaterThanOrEqualTo(reportPath.worldBound.yMax - GeometryTolerance), "操作Buttonがreport pathへ重なっています。");
            Assert.That(manualBoundary.worldBound.yMin, Is.GreaterThanOrEqualTo(buttonRow.worldBound.yMax - GeometryTolerance), "manual境界表示が操作Buttonへ重なっています。");
        }

        /// <summary>Buttonが保持する実callbackをUI ToolkitのClick入口から呼ぶ。</summary>
        /// <param name="button">有効状態を確認済みのsample Button。</param>
        private static void InvokeBoundClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True, $"{button.name} Buttonが無効です。");
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "UI ToolkitのButton callback入口を取得できません。");
            invoke.Invoke(button.clickable, new object[] { null });
        }

        /// <summary>専用directoryにある一時file候補を正規化した集合で返す。</summary>
        /// <returns>`.tmp`を名前に含むfileの絶対path集合。</returns>
        private static HashSet<string> FindTemporaryFiles()
        {
            var diagnosticsDirectory = Path.Combine(Application.persistentDataPath, "DiagnosticsContext");
            if (!Directory.Exists(diagnosticsDirectory)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var temporaryFiles = Directory.GetFiles(diagnosticsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).IndexOf(".tmp", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(Path.GetFullPath);
            return new HashSet<string>(temporaryFiles, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>専用directoryにある最終JSON reportを正規化した集合で返す。</summary>
        /// <returns>`.json`最終fileの絶対path集合。</returns>
        private static HashSet<string> FindReportFiles()
        {
            var diagnosticsDirectory = Path.Combine(Application.persistentDataPath, "DiagnosticsContext");
            if (!Directory.Exists(diagnosticsDirectory)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var reportFiles = Directory.GetFiles(diagnosticsDirectory, "*.json", SearchOption.TopDirectoryOnly).Select(Path.GetFullPath);
            return new HashSet<string>(reportFiles, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>実時間deadlineまで指定条件をframeごとに確認する。</summary>
        /// <param name="condition">成功時にtrueとなる条件。</param>
        /// <param name="timeoutSeconds">deadlineまでの実時間秒数。</param>
        /// <param name="failureMessage">deadline超過時の失敗説明。</param>
        /// <returns>PlayModeのframeごとに進むcoroutine。</returns>
        private static IEnumerator WaitUntil(Func<bool> condition, double timeoutSeconds, string failureMessage)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) Assert.Fail(failureMessage);
                yield return null;
            }
        }

        /// <summary>report JSONのtop-level fieldをJsonUtilityでparseするtest用型。</summary>
        [Serializable]
        private sealed class ReportDocument
        {
            /// <summary>report readerが解釈するschema番号。</summary>
            public int schemaVersion = 0;

            /// <summary>Invariant CultureのUTC round-trip timestamp。</summary>
            public string createdUtc = string.Empty;

            /// <summary>file名へ使わない手動書出し理由。</summary>
            public string reason = string.Empty;

            /// <summary>保持上限から追い出したbreadcrumb件数。</summary>
            public long droppedBreadcrumbCount = 0;

            /// <summary>保持上限から追い出したlog件数。</summary>
            public long droppedLogCount = 0;

            /// <summary>keyのordinal順に並ぶcontext。</summary>
            public ContextItem[] context = Array.Empty<ContextItem>();

            /// <summary>sequence昇順に並ぶbreadcrumb。</summary>
            public BreadcrumbItem[] breadcrumbs = Array.Empty<BreadcrumbItem>();

            /// <summary>sequence昇順に並ぶcaptured log。</summary>
            public LogItem[] logs = Array.Empty<LogItem>();
        }

        /// <summary>report内の明示的なcontext 1件をparseするtest用型。</summary>
        [Serializable]
        private sealed class ContextItem
        {
            /// <summary>利用側が選んだcontext key。</summary>
            public string key = string.Empty;

            /// <summary>利用側が選んだcontext value。</summary>
            public string value = string.Empty;
        }

        /// <summary>report内のbreadcrumb 1件をparseするtest用型。</summary>
        [Serializable]
        private sealed class BreadcrumbItem
        {
            /// <summary>追加順を表す単調増加番号。</summary>
            public long sequence = 0;

            /// <summary>利用側が明示した短い出来事。</summary>
            public string message = string.Empty;
        }

        /// <summary>report内のcaptured log 1件をparseするtest用型。</summary>
        [Serializable]
        private sealed class LogItem
        {
            /// <summary>取得順を表す単調増加番号。</summary>
            public long sequence = 0;

            /// <summary>Unityが通知したLogType名。</summary>
            public string type = string.Empty;

            /// <summary>上限内へ収めたlog本文。</summary>
            public string message = string.Empty;

            /// <summary>上限内へ収めたstack情報。</summary>
            public string stackTrace = string.Empty;
        }
    }
}
