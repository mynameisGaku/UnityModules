using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiagnosticsContext.Tests.PlayMode
{
    /// <summary>実Unity log subscription、worker callback、report path、owner終了をPlayModeで確認する。</summary>
    public sealed class DiagnosticsContextLifecycleTests
    {
        /// <summary>Warning callbackを取得してreportへ保存し、Dispose後のWarningを取得しない。</summary>
        [UnityTest]
        public IEnumerator TryCreate_WarningAndDispose_CapturesOnlyOwnerLifetime()
        {
            DiagnosticsContextService service = null;
            string reportPath = null;
            var temporaryFilesBefore = FindTemporaryFiles();
            try
            {
                Assert.That(DiagnosticsContextService.TryCreate(out service, out var error), Is.True);
                Assert.That(error, Is.EqualTo(DiagnosticsError.None));

                LogAssert.Expect(LogType.Warning, "diagnostics-context-playmode-warning");
                Debug.LogWarning("diagnostics-context-playmode-warning");
                yield return null;
                Assert.That(service.CapturedLogCount, Is.EqualTo(1));

                var result = service.WriteReport("playmode report");
                reportPath = result.ReportPath;
                Assert.That(result.Succeeded, Is.True);
                Assert.That(File.Exists(reportPath), Is.True);
                var expectedReportDirectory = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "DiagnosticsContext"));
                var actualReportDirectory = Path.GetFullPath(Path.GetDirectoryName(reportPath));
                var comparison = GetPathComparison();
                Assert.That(string.Equals(actualReportDirectory, expectedReportDirectory, comparison), Is.True, $"report directoryが一致しません。 expected: {expectedReportDirectory}, actual: {actualReportDirectory}");

                service.Dispose();
                LogAssert.Expect(LogType.Warning, "diagnostics-context-after-dispose");
                Debug.LogWarning("diagnostics-context-after-dispose");
                yield return null;
                Assert.That(service.CapturedLogCount, Is.Zero);
            }
            finally
            {
                try
                {
                    service?.Dispose();
                }
                finally
                {
                    TryDeleteFile(reportPath);
                    DeleteNewTemporaryFiles(temporaryFilesBefore);
                }
            }
        }

        /// <summary>worker threadからの作成要求はUnity pathやeventへ触れずMainThreadRequiredになる。</summary>
        [UnityTest]
        public IEnumerator TryCreate_WorkerThread_ReturnsMainThreadRequired()
        {
            DiagnosticsContextService workerService = null;
            var workerError = DiagnosticsError.None;
            var worker = new Thread(() => DiagnosticsContextService.TryCreate(out workerService, out workerError));
            worker.Start();
            Assert.That(worker.Join(5000), Is.True);

            Assert.That(workerService, Is.Null);
            Assert.That(workerError, Is.EqualTo(DiagnosticsError.MainThreadRequired));
            yield return null;
        }

        /// <summary>実行環境のfile systemに合わせたpath比較方法を返す。</summary>
        /// <returns>Windowsでは大文字小文字を区別せず、それ以外では区別する比較方法。</returns>
        private static StringComparison GetPathComparison()
        {
            return Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        /// <summary>test開始前から存在したfileを除き、今回残った一時fileだけを削除する。</summary>
        /// <param name="temporaryFilesBefore">test開始前から存在した一時fileの絶対path集合。</param>
        private static void DeleteNewTemporaryFiles(HashSet<string> temporaryFilesBefore)
        {
            var temporaryFilesAfter = FindTemporaryFiles();
            foreach (var temporaryFile in temporaryFilesAfter)
            {
                if (!temporaryFilesBefore.Contains(temporaryFile)) TryDeleteFile(temporaryFile);
            }
        }

        /// <summary>専用directoryにある一時file候補を正規化した集合で返す。</summary>
        /// <returns>`.tmp`を名前に含むfileの絶対path集合。</returns>
        private static HashSet<string> FindTemporaryFiles()
        {
            var comparer = GetPathComparison() == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var temporaryFiles = new HashSet<string>(comparer);
            var diagnosticsDirectory = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "DiagnosticsContext"));
            if (!Directory.Exists(diagnosticsDirectory)) return temporaryFiles;

            var candidates = Directory.GetFiles(diagnosticsDirectory, "*.tmp", SearchOption.TopDirectoryOnly);
            for (var index = 0; index < candidates.Length; index++) temporaryFiles.Add(Path.GetFullPath(candidates[index]));
            return temporaryFiles;
        }

        /// <summary>失敗時の元の検証結果を隠さず、今回所有するfileの削除を試みる。</summary>
        /// <param name="path">削除するfile path。未設定または存在しない場合は何もしない。</param>
        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException)
            {
            }
        }
    }
}
