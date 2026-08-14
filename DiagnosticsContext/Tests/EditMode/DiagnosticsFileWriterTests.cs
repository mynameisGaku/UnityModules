using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace DiagnosticsContext.Tests
{
    /// <summary>専用directoryの準備、上書き防止、一時file cleanupを確認する。</summary>
    public sealed class DiagnosticsFileWriterTests
    {
        /// <summary>同じ時刻とIDの既存最終reportを上書きせず、今回の一時fileを残さない。</summary>
        [Test]
        public void Write_ExistingFinalPath_ReturnsWriteFailedWithoutOverwriteOrTemporaryFile()
        {
            var root = Path.Combine(Path.GetTempPath(), "DiagnosticsFileWriterTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var createdUtc = new DateTime(2026, 8, 14, 1, 2, 3, DateTimeKind.Utc);
                var id = Guid.ParseExact("00112233445566778899aabbccddeeff", "N");
                var first = DiagnosticsFileWriter.Write(root, createdUtc, id, new byte[] { 1, 2, 3 });
                var second = DiagnosticsFileWriter.Write(root, createdUtc, id, new byte[] { 9, 9, 9 });

                Assert.That(first.Succeeded, Is.True);
                Assert.That(second.Error, Is.EqualTo(DiagnosticsError.WriteFailed));
                Assert.That(File.ReadAllBytes(first.ReportPath), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        /// <summary>空の保存pathを相対directoryへ変換せずStorageUnavailableとして拒否する。</summary>
        [Test]
        public void TryPrepareDirectory_BlankPath_ReturnsStorageUnavailableWithoutCreatingDirectory()
        {
            var error = DiagnosticsFileWriter.TryPrepareDirectory(" ", out var normalizedDirectory);

            Assert.That(error, Is.EqualTo(DiagnosticsError.StorageUnavailable));
            Assert.That(normalizedDirectory, Is.Empty);
        }

        /// <summary>Windowsの既存junctionを専用directoryとして受理せず、物理的な保存先逸脱を防ぐ。</summary>
        [Test]
        public void TryPrepareDirectory_WindowsJunction_ReturnsStorageUnavailable()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) Assert.Ignore("Windows reparse point専用の検証です。");

            var root = Path.Combine(Path.GetTempPath(), "DiagnosticsReparseTests", Guid.NewGuid().ToString("N"));
            var target = Path.Combine(root, "target");
            var junction = Path.Combine(root, "DiagnosticsContext");
            Directory.CreateDirectory(target);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/d /c mklink /J \"{junction}\" \"{target}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                using (var process = Process.Start(startInfo))
                {
                    if (process == null) Assert.Ignore("junction作成processを開始できませんでした。");
                    process.WaitForExit();
                    if (process.ExitCode != 0) Assert.Ignore("この環境ではjunctionを作成できませんでした。");
                }

                var error = DiagnosticsFileWriter.TryPrepareDirectory(junction, out var normalizedDirectory);

                Assert.That(error, Is.EqualTo(DiagnosticsError.StorageUnavailable));
                Assert.That(normalizedDirectory, Is.Empty);
            }
            finally
            {
                if (Directory.Exists(junction)) Directory.Delete(junction);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
