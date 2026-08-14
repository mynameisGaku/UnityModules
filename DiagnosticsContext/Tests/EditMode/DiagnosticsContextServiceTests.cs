using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace DiagnosticsContext.Tests
{
    /// <summary>Serviceの入力、容量、ring、thread、保存、終了契約をUnity eventから分離して確認する。</summary>
    public sealed class DiagnosticsContextServiceTests
    {
        /// <summary>各testだけが所有する一時保存root。</summary>
        private string _temporaryRoot;

        /// <summary>testのメインスレッド記録と空の一時directoryを用意する。</summary>
        [SetUp]
        public void SetUp()
        {
            DiagnosticsMainThread.BindCurrentThreadForTesting();
            _temporaryRoot = Path.Combine(Path.GetTempPath(), "DiagnosticsContextTests", Guid.NewGuid().ToString("N"));
        }

        /// <summary>作成された一時reportを各test後に除去する。</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
        }

        /// <summary>満杯時も既存keyを更新でき、新規keyだけ容量超過になる。</summary>
        [Test]
        public void SetContext_AtCapacity_UpdatesExistingAndRejectsNewKey()
        {
            using (var service = CreateService())
            {
                for (var index = 0; index < DiagnosticsContextService.MaximumContextEntryCount; index++)
                {
                    Assert.That(service.SetContext($"key-{index:D2}", $"value-{index:D2}"), Is.EqualTo(DiagnosticsError.None));
                }

                Assert.That(service.SetContext("key-00", string.Empty), Is.EqualTo(DiagnosticsError.None));
                Assert.That(service.SetContext("overflow", "value"), Is.EqualTo(DiagnosticsError.ContextCapacityExceeded));
                Assert.That(service.ContextEntryCount, Is.EqualTo(DiagnosticsContextService.MaximumContextEntryCount));
            }
        }

        /// <summary>Runtime assemblyが契約済み3型だけを公開し、error列挙順を固定する。</summary>
        [Test]
        public void PublicApi_ExportsExactlyFrozenTypesAndErrors()
        {
            var exportedTypeNames = typeof(DiagnosticsContextService).Assembly.GetExportedTypes().Select(type => type.FullName).OrderBy(name => name).ToArray();
            var expectedTypeNames = new[]
            {
                "DiagnosticsContext.DiagnosticsContextService",
                "DiagnosticsContext.DiagnosticsError",
                "DiagnosticsContext.DiagnosticsWriteResult",
            };
            var expectedErrors = new[]
            {
                DiagnosticsError.None,
                DiagnosticsError.InvalidInput,
                DiagnosticsError.ContextCapacityExceeded,
                DiagnosticsError.Disposed,
                DiagnosticsError.MainThreadRequired,
                DiagnosticsError.StorageUnavailable,
                DiagnosticsError.ReportTooLarge,
                DiagnosticsError.WriteFailed,
            };

            Assert.That(exportedTypeNames, Is.EqualTo(expectedTypeNames));
            Assert.That((DiagnosticsError[])Enum.GetValues(typeof(DiagnosticsError)), Is.EqualTo(expectedErrors));
            Assert.That(typeof(DiagnosticsWriteResult).IsValueType, Is.True);
            Assert.That(typeof(DiagnosticsContextService).IsSealed, Is.True);
        }

        /// <summary>null valueを拒否し、空を許可して不正Unicodeとover-limitを安全に整える。</summary>
        [Test]
        public void SetContext_InputBoundaries_ReturnExpectedErrors()
        {
            using (var service = CreateService())
            {
                Assert.That(service.SetContext("empty", string.Empty), Is.EqualTo(DiagnosticsError.None));
                Assert.That(service.SetContext("whitespace", "   "), Is.EqualTo(DiagnosticsError.None));
                Assert.That(service.SetContext("null", null), Is.EqualTo(DiagnosticsError.InvalidInput));
                Assert.That(service.SetContext("bad", "\uD800"), Is.EqualTo(DiagnosticsError.None));
                Assert.That(service.SetContext("long", new string('v', DiagnosticsContextService.MaximumContextValueScalarCount + 1)), Is.EqualTo(DiagnosticsError.None));
                Assert.That(service.SetContext(new string('k', DiagnosticsContextService.MaximumContextKeyScalarCount + 1), "v"), Is.EqualTo(DiagnosticsError.InvalidInput));
            }
        }

        /// <summary>ringは古い項目を追い出し、drop件数と保持件数を単調に更新する。</summary>
        [Test]
        public void Rings_Overflow_DropOldestAndCountDrops()
        {
            using (var service = CreateService())
            {
                for (var index = 0; index < DiagnosticsContextService.MaximumBreadcrumbCount + 3; index++)
                {
                    Assert.That(service.AddBreadcrumb($"crumb-{index:D3}"), Is.EqualTo(DiagnosticsError.None));
                }

                for (var index = 0; index < DiagnosticsContextService.MaximumCapturedLogCount + 2; index++)
                {
                    service.CaptureLogForTesting($"warning-{index:D3}", string.Empty, LogType.Warning);
                }

                Assert.That(service.BreadcrumbCount, Is.EqualTo(DiagnosticsContextService.MaximumBreadcrumbCount));
                Assert.That(service.DroppedBreadcrumbCount, Is.EqualTo(3));
                Assert.That(service.CapturedLogCount, Is.EqualTo(DiagnosticsContextService.MaximumCapturedLogCount));
                Assert.That(service.DroppedLogCount, Is.EqualTo(2));
            }
        }

        /// <summary>通常Logを無視し、workerから届くWarning文字列を即時に保持する。</summary>
        [Test]
        public void CaptureLog_WorkerTargetTypes_AreThreadSafeAndNormalLogIsExcluded()
        {
            using (var service = CreateService())
            {
                service.CaptureLogForTesting("normal", "stack", LogType.Log);
                var worker = new Thread(() =>
                {
                    service.CaptureLogForTesting("worker-warning", "worker-stack", LogType.Warning);
                    service.CaptureLogForTesting("worker-error", "worker-stack", LogType.Error);
                    service.CaptureLogForTesting("worker-assert", "worker-stack", LogType.Assert);
                    service.CaptureLogForTesting("worker-exception", "worker-stack", LogType.Exception);
                });
                worker.Start();
                Assert.That(worker.Join(5000), Is.True);

                Assert.That(service.CapturedLogCount, Is.EqualTo(4));
            }
        }

        /// <summary>最悪escapeの有界payloadが512KiBを超えた場合、directory作成前に拒否する。</summary>
        [Test]
        public void WriteReport_WorstCaseEscapedPayload_ReturnsTooLargeWithoutStorageIo()
        {
            using (var service = CreateService())
            {
                var message = new string('\u0001', DiagnosticsContextService.MaximumLogMessageScalarCount);
                var stack = new string('\u0002', DiagnosticsContextService.MaximumLogStackTraceScalarCount);
                for (var index = 0; index < DiagnosticsContextService.MaximumCapturedLogCount; index++)
                {
                    service.CaptureLogForTesting(message, stack, LogType.Warning);
                }

                var result = service.WriteReport("size boundary");

                Assert.That(result.Error, Is.EqualTo(DiagnosticsError.ReportTooLarge));
                Assert.That(Directory.Exists(_temporaryRoot), Is.False);
            }
        }

        /// <summary>同じ件数でもescape不要の最大長payloadは上限未満で保存できる。</summary>
        [Test]
        public void WriteReport_MaximumPlainTextLogs_RemainsBelowByteCapAndSucceeds()
        {
            using (var service = CreateService())
            {
                var message = new string('m', DiagnosticsContextService.MaximumLogMessageScalarCount);
                var stack = new string('s', DiagnosticsContextService.MaximumLogStackTraceScalarCount);
                for (var index = 0; index < DiagnosticsContextService.MaximumCapturedLogCount; index++)
                {
                    service.CaptureLogForTesting(message, stack, LogType.Error);
                }

                var result = service.WriteReport("plain size boundary");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.ReportByteCount, Is.LessThanOrEqualTo(DiagnosticsContextService.MaximumReportByteCount));
            }
        }

        /// <summary>初期化前の最初の参照がworkerでも、そのworkerをメインスレッドとして記録しない。</summary>
        [Test]
        public void MainThread_FirstTouchFromWorker_FailsClosedUntilExplicitBind()
        {
            DiagnosticsMainThread.ResetForTesting();
            try
            {
                var workerWasMain = true;
                var worker = new Thread(() => workerWasMain = DiagnosticsMainThread.IsCurrent);
                worker.Start();
                Assert.That(worker.Join(5000), Is.True);
                Assert.That(workerWasMain, Is.False);
            }
            finally
            {
                DiagnosticsMainThread.BindCurrentThreadForTesting();
            }
        }

        /// <summary>reportは固定順JSONをBOMなしで保存し、reasonをfile名へ含めず一時fileを残さない。</summary>
        [Test]
        public void WriteReport_ValidSnapshot_WritesContainedParseableUtf8WithoutTemporaryFile()
        {
            using (var service = CreateService())
            {
                service.SetContext("z", "last");
                service.SetContext("a", "first");
                service.AddBreadcrumb("started");
                service.CaptureLogForTesting("warning", "stack", LogType.Warning);

                var result = service.WriteReport("reason/with:path");

                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Error, Is.EqualTo(DiagnosticsError.None));
                Assert.That(Path.GetDirectoryName(result.ReportPath), Is.EqualTo(Path.GetFullPath(_temporaryRoot)));
                Assert.That(Path.GetFileName(result.ReportPath), Does.Not.Contain("reason"));
                var bytes = File.ReadAllBytes(result.ReportPath);
                Assert.That(bytes.Length, Is.EqualTo(result.ReportByteCount));
                Assert.That(bytes.Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
                var json = new UTF8Encoding(false, true).GetString(bytes);
                Assert.That(json.IndexOf("\"key\":\"a\"", StringComparison.Ordinal), Is.LessThan(json.IndexOf("\"key\":\"z\"", StringComparison.Ordinal)));
                Assert.That(Directory.GetFiles(_temporaryRoot, "*.tmp"), Is.Empty);
            }
        }

        /// <summary>blank reason、worker呼出し、終了後のerror優先順位を固定する。</summary>
        [Test]
        public void WriteReport_ErrorPrecedence_IsDisposedThenThreadThenInput()
        {
            var service = CreateService();
            Assert.That(service.WriteReport(" ").Error, Is.EqualTo(DiagnosticsError.InvalidInput));

            DiagnosticsWriteResult workerResult = default;
            var worker = new Thread(() => workerResult = service.WriteReport(" "));
            worker.Start();
            Assert.That(worker.Join(5000), Is.True);
            Assert.That(workerResult.Error, Is.EqualTo(DiagnosticsError.MainThreadRequired));

            service.Dispose();
            Assert.That(service.WriteReport(" ").Error, Is.EqualTo(DiagnosticsError.Disposed));
        }

        /// <summary>複数threadのDisposeは全呼出しが終了完了後に戻り、以後の操作を拒否する。</summary>
        [Test]
        public void Dispose_ConcurrentAndRepeatedCalls_FormCompletionBarrier()
        {
            var service = CreateService();
            service.AddBreadcrumb("before-dispose");
            var first = new Thread(service.Dispose);
            var second = new Thread(service.Dispose);

            first.Start();
            second.Start();
            Assert.That(first.Join(5000), Is.True);
            Assert.That(second.Join(5000), Is.True);
            service.Dispose();

            Assert.That(service.AddBreadcrumb("after"), Is.EqualTo(DiagnosticsError.Disposed));
            Assert.That(service.BreadcrumbCount, Is.Zero);
        }

        /// <summary>snapshot後の書出し中は複数Disposeが待ち、保存完了後だけ全呼出しが戻る。</summary>
        [Test]
        public void Dispose_ActiveWrite_BlocksAllCallersUntilWriterCompletes()
        {
            using (var writerEntered = new ManualResetEvent(false))
            using (var releaseWriter = new ManualResetEvent(false))
            {
                var service = new DiagnosticsContextService(
                    _temporaryRoot,
                    () => new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
                    () => Guid.Empty,
                    false,
                    (directory, createdUtc, id, bytes) =>
                    {
                        writerEntered.Set();
                        releaseWriter.WaitOne();
                        return DiagnosticsWriteResult.Success(Path.Combine(directory, "injected.json"), bytes.Length);
                    });
                DiagnosticsWriteResult writeResult = default;
                var writeThread = new Thread(() =>
                {
                    DiagnosticsMainThread.BindCurrentThreadForTesting();
                    writeResult = service.WriteReport("blocking write");
                });
                var firstDispose = new Thread(service.Dispose);
                var secondDispose = new Thread(service.Dispose);

                try
                {
                    writeThread.Start();
                    Assert.That(writerEntered.WaitOne(5000), Is.True);
                    firstDispose.Start();
                    secondDispose.Start();
                    Assert.That(firstDispose.Join(50), Is.False);
                    Assert.That(secondDispose.Join(50), Is.False);
                    releaseWriter.Set();
                    Assert.That(writeThread.Join(5000), Is.True);
                    Assert.That(firstDispose.Join(5000), Is.True);
                    Assert.That(secondDispose.Join(5000), Is.True);
                    Assert.That(writeResult.Succeeded, Is.True);
                    Assert.That(service.AddBreadcrumb("after"), Is.EqualTo(DiagnosticsError.Disposed));
                }
                finally
                {
                    releaseWriter.Set();
                    writeThread.Join(5000);
                    firstDispose.Join(5000);
                    secondDispose.Join(5000);
                    DiagnosticsMainThread.BindCurrentThreadForTesting();
                }
            }
        }

        /// <summary>sequenceがlong上限へ達してもring順のJSON値は非減少のまま維持する。</summary>
        [Test]
        public void Sequence_AtLongMaximum_RemainsNondecreasing()
        {
            using (var service = CreateService())
            {
                service.SetNextSequenceForTesting(long.MaxValue - 1);
                service.AddBreadcrumb("first");
                service.AddBreadcrumb("second");
                service.AddBreadcrumb("third");

                var result = service.WriteReport("sequence saturation");
                var json = File.ReadAllText(result.ReportPath, Encoding.UTF8);
                var first = json.IndexOf($"\"sequence\":{long.MaxValue - 1}", StringComparison.Ordinal);
                var second = json.IndexOf($"\"sequence\":{long.MaxValue}", first + 1, StringComparison.Ordinal);
                var third = json.IndexOf($"\"sequence\":{long.MaxValue}", second + 1, StringComparison.Ordinal);

                Assert.That(first, Is.GreaterThanOrEqualTo(0));
                Assert.That(second, Is.GreaterThan(first));
                Assert.That(third, Is.GreaterThan(second));
            }
        }

        /// <summary>固定時刻とIDを持ち、Unity log eventを購読しないServiceを作る。</summary>
        private DiagnosticsContextService CreateService()
        {
            return new DiagnosticsContextService(
                _temporaryRoot,
                () => new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc),
                () => Guid.ParseExact("00112233445566778899aabbccddeeff", "N"),
                false);
        }
    }
}
