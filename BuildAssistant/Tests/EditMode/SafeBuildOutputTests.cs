using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using BuildAssistant.Editor;
using NUnit.Framework;

namespace BuildAssistant.Tests
{
    public sealed class SafeBuildOutputTests
    {
        [Test]
        public void Inspect_AcceptsAnExistingRootOrExactlyOneMissingChild()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);

            var existing = output.Inspect(BuildAssistantTestData.OutputRoot);
            var missing = output.Inspect(Path.Combine(parent, "OneMissingChild"));

            Assert.That(existing.Error, Is.EqualTo(BuildAssistantError.None));
            Assert.That(existing.Mode, Is.EqualTo(OutputRootMode.ExistingDirectory));
            Assert.That(missing.Error, Is.EqualTo(BuildAssistantError.None));
            Assert.That(missing.Mode, Is.EqualTo(OutputRootMode.MissingChild));
        }

        [Test]
        public void Inspect_RejectsRelativeDeepManagedAndReparsePaths()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot, Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets"));
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);

            Assert.That(output.Inspect("relative/builds").Error, Is.EqualTo(BuildAssistantError.InvalidOutputRoot));
            Assert.That(output.Inspect(Path.Combine(parent, "missing", "too-deep")).Error, Is.EqualTo(BuildAssistantError.InvalidOutputRoot));
            Assert.That(output.Inspect(Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets")).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
            fake.MarkReparse(BuildAssistantTestData.OutputRoot);
            Assert.That(output.Inspect(BuildAssistantTestData.OutputRoot).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        [Test]
        public void Inspect_RejectsCaseVariantsAndCanonicalAliasesOfManagedDirectories()
        {
            var assets = Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets");
            var caseVariant = Path.Combine(BuildAssistantTestData.ProjectRoot, "assets");
            var alias = Path.Combine(Path.GetPathRoot(BuildAssistantTestData.ProjectRoot), "BuildAssistantAlias", "Assets");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, assets, alias);
            fake.SetCanonicalPath(alias, assets);
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);

            Assert.That(output.Inspect(caseVariant).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
            Assert.That(output.Inspect(alias).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        [Test]
        public void Inspect_FailsClosedWhenAnyFilesystemProbeThrows()
        {
            var fileProbe = CreateOutputWithFake(out var fileFake);
            fileFake.FileExistsException = new IOException("Injected file probe failure.");
            Assert.That(fileProbe.Inspect(BuildAssistantTestData.OutputRoot).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));

            var directoryProbe = CreateOutputWithFake(out var directoryFake);
            directoryFake.DirectoryExistsException = new UnauthorizedAccessException("Injected directory probe failure.");
            Assert.That(directoryProbe.Inspect(BuildAssistantTestData.OutputRoot).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));

            var attributeProbe = CreateOutputWithFake(out var attributeFake);
            attributeFake.GetAttributesException = new SecurityException("Injected attribute probe failure.");
            Assert.That(attributeProbe.Inspect(BuildAssistantTestData.OutputRoot).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        [Test]
        public void BuildOutputPolicyConstruction_MapsCanonicalProbeFailuresToBoundedResults()
        {
            Assert.That(BuildAssistantService.TryCreateSafeOutput(() => throw new IOException("Injected canonical probe failure."), out var ioOutput, out var ioFailure), Is.False);
            Assert.That(ioOutput, Is.Null);
            Assert.That(ioFailure.Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));

            Assert.That(BuildAssistantService.TryCreateSafeOutput(() => throw new ArgumentException("Injected path failure."), out var pathOutput, out var pathFailure), Is.False);
            Assert.That(pathOutput, Is.Null);
            Assert.That(pathFailure.Error, Is.EqualTo(BuildAssistantError.InvalidOutputRoot));
        }

        [Test]
        public void Inspect_RejectsNetworkDriveRoots()
        {
            var output = CreateOutputWithFake(out var fake);
            fake.MarkNetworkDrive(BuildAssistantTestData.OutputRoot);

            Assert.That(output.Inspect(BuildAssistantTestData.OutputRoot).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        [Test]
        public void DriveClassification_UsesTheLongestReportedUnixMountAndFailsClosed()
        {
            var linuxDrives = new[]
            {
                new KeyValuePair<string, DriveType>("/", DriveType.Fixed),
                new KeyValuePair<string, DriveType>("/mnt/team", DriveType.Network)
            };
            var macDrives = new[]
            {
                new KeyValuePair<string, DriveType>("/", DriveType.Fixed),
                new KeyValuePair<string, DriveType>("/Volumes/LocalBuilds", DriveType.Fixed),
                new KeyValuePair<string, DriveType>("/Volumes/Team", DriveType.Network)
            };

            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/home/user/Builds", linuxDrives, '/'), Is.True);
            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/mnt/team/Builds", linuxDrives, '/'), Is.False);
            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/Volumes/LocalBuilds/Player", macDrives, '/'), Is.True);
            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/Volumes/Team/Player", macDrives, '/'), Is.False);
            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/volumes/team/Player", macDrives, '/'), Is.False);
            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/unclassified", Array.Empty<KeyValuePair<string, DriveType>>(), '/'), Is.False);
        }

        [Test]
        public void DriveClassification_UsesCaseSensitiveUnixMountNames()
        {
            var localRootWithNetworkChild = new[]
            {
                new KeyValuePair<string, DriveType>("/", DriveType.Fixed),
                new KeyValuePair<string, DriveType>("/mnt/data", DriveType.Network)
            };
            var networkRootWithLocalChild = new[]
            {
                new KeyValuePair<string, DriveType>("/", DriveType.Network),
                new KeyValuePair<string, DriveType>("/mnt/Data", DriveType.Fixed)
            };

            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/mnt/Data/Builds", localRootWithNetworkChild, '/'), Is.False);
            Assert.That(BuildAssistantFileSystem.IsLocalFixedDrive("/mnt/data/Builds", networkRootWithLocalChild, '/'), Is.False);
            Assert.That(BuildAssistantFileSystem.GetPathComparison('/'), Is.EqualTo(StringComparison.Ordinal));
            Assert.That(BuildAssistantFileSystem.GetPathComparison('\\'), Is.EqualTo(StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void Inspect_RejectsAPhysicalAliasOfAManagedDirectoryEvenWhenCanonicalTextDiffers()
        {
            var assets = Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets");
            var aliasRoot = Path.Combine(Path.GetPathRoot(BuildAssistantTestData.ProjectRoot), "MountedAssets");
            var aliasChild = Path.Combine(aliasRoot, "Builds");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, assets, aliasRoot, aliasChild);
            fake.SetDirectoryIdentity(assets, "shared-assets-directory");
            fake.SetDirectoryIdentity(aliasRoot, "shared-assets-directory");
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);

            Assert.That(output.Inspect(aliasChild).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        [Test]
        public void Inspect_RejectsAMountedAliasOfAChildInsideAManagedDirectory()
        {
            var assets = Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets");
            var sourceChild = Path.Combine(assets, "SourceData");
            var aliasRoot = Path.Combine(Path.GetPathRoot(BuildAssistantTestData.ProjectRoot), "MountedSourceData");
            var aliasChild = Path.Combine(aliasRoot, "Builds");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, assets, sourceChild, aliasRoot, aliasChild);
            fake.SetPhysicalLocation(assets, "8:1", "/project/Assets");
            fake.SetPhysicalLocation(aliasRoot, "8:1", "/project/Assets/SourceData");
            fake.SetPhysicalLocation(aliasChild, "8:1", "/project/Assets/SourceData/Builds");
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);

            Assert.That(output.Inspect(aliasChild).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        [Test]
        public void LinuxMountInformation_MapsABindAliasBackToItsSourceSubdirectory()
        {
            const string mountInformation = "25 1 8:1 / / rw,relatime - ext4 /dev/sda1 rw\n26 25 8:1 /project/Assets/SourceData /mnt/alias rw,relatime - ext4 /dev/sda1 rw\n";

            var resolved = BuildAssistantFileSystem.TryResolveLinuxMountLocation("/mnt/alias/Builds", mountInformation, out var fileSystemId, out var internalPath);

            Assert.That(resolved, Is.True);
            Assert.That(fileSystemId, Is.EqualTo("8:1"));
            Assert.That(internalPath, Is.EqualTo("/project/Assets/SourceData/Builds"));
        }

        [Test]
        public void Reserve_CreatesOnlyTheNewRunAndUsesSingleOwnerReservation()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();

            var reservation = output.Reserve(plan);
            using (reservation)
            {
                Assert.That(reservation.IsReserved, Is.True);
                Assert.That(fake.DirectoryExists(plan.RunDirectory), Is.True);
                Assert.That(output.IsRunPathBusy(plan.OutputRoot, plan.RunId), Is.True);
            }

            Assert.That(reservation.IsReserved, Is.False);
            Assert.That(fake.FileExists(Path.Combine(plan.OutputRoot, "." + plan.RunId + ".reserve")), Is.False);
            Assert.That(output.Reserve(plan).Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
        }

        [Test]
        public void Reserve_AtomicDirectoryCreationRejectsARaceAfterThePrecheck()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            fake.InjectCreateNewDirectoryCollisionPath = plan.RunDirectory;

            using (var reservation = output.Reserve(plan))
                Assert.That(reservation.Error, Is.EqualTo(BuildAssistantError.OutputAlreadyExists));

            Assert.That(fake.FileExists(Path.Combine(plan.OutputRoot, "." + plan.RunId + ".reserve")), Is.False);
        }

        [Test]
        public void Reservation_RevalidationRejectsContentAddedBeforeInvocation()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();
            using (var reservation = output.Reserve(plan))
            {
                Assert.That(reservation.IsReserved, Is.True);
                fake.SetFile(plan.ArtifactPath, "unexpected");

                Assert.That(reservation.Revalidate(plan, out var message), Is.EqualTo(BuildAssistantError.OutputAlreadyExists));
                Assert.That(message, Is.Not.Empty);
            }
        }

        [Test]
        public void RealWindowsCreateNewDirectoryAndLeaseRejectExistingOrRenamedPaths()
        {
            if (Path.DirectorySeparatorChar != '\\')
                Assert.Ignore("Windows directory sharing is verified only on Windows.");
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "BuildAssistantLeaseTest-" + Guid.NewGuid().ToString("N"));
            var newDirectory = Path.Combine(temporaryRoot, "Run");
            var movedDirectory = Path.Combine(temporaryRoot, "Moved");
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                var fileSystem = new BuildAssistantFileSystem();
                fileSystem.CreateDirectoryNew(newDirectory);
                Assert.Throws<CreateNewDirectoryCollisionException>(() => fileSystem.CreateDirectoryNew(newDirectory));
                using (var lease = fileSystem.AcquireDirectoryIdentityLease(newDirectory))
                {
                    Assert.That(lease.CanonicalPath, Is.Not.Empty);
                    File.WriteAllText(Path.Combine(newDirectory, "child.txt"), "write remains available");
                    Assert.Throws<IOException>(() => Directory.Move(newDirectory, movedDirectory));
                }

                Directory.Move(newDirectory, movedDirectory);
                Assert.That(Directory.Exists(movedDirectory), Is.True);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void BoundedTextRead_AcceptsExistingByteOrderMarksAndReleasesTheRealFile()
        {
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "BuildAssistantBoundedRead-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(temporaryRoot, "history.json");
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                var fileSystem = new BuildAssistantFileSystem();
                var expected = "{\"message\":\"日本語の履歴\"}";
                var encodings = new Encoding[]
                {
                    new UTF8Encoding(true, true),
                    new UnicodeEncoding(false, true, true),
                    new UnicodeEncoding(true, true, true),
                    new UTF32Encoding(false, true, true),
                    new UTF32Encoding(true, true, true)
                };
                foreach (var encoding in encodings)
                {
                    var preamble = encoding.GetPreamble();
                    var body = encoding.GetBytes(expected);
                    var content = new byte[preamble.Length + body.Length];
                    Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
                    Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);
                    File.WriteAllBytes(path, content);

                    Assert.That(fileSystem.ReadAllTextBounded(path, content.Length), Is.EqualTo(expected));
                    AssertFileCanBeOpenedExclusively(path);
                    File.Delete(path);
                    Assert.That(File.Exists(path), Is.False);
                }

                File.WriteAllBytes(path, new byte[] { 0xc3, 0x28 });
                Assert.Throws<InvalidDataException>(() => fileSystem.ReadAllTextBounded(path, 2));
                AssertFileCanBeOpenedExclusively(path);
                File.Delete(path);

                var wideText = new string('日', 6);
                var wideEncoding = new UnicodeEncoding(false, true, true);
                var widePreamble = wideEncoding.GetPreamble();
                var wideBody = wideEncoding.GetBytes(wideText);
                var wideContent = new byte[widePreamble.Length + wideBody.Length];
                Buffer.BlockCopy(widePreamble, 0, wideContent, 0, widePreamble.Length);
                Buffer.BlockCopy(wideBody, 0, wideContent, widePreamble.Length, wideBody.Length);
                File.WriteAllBytes(path, wideContent);
                Assert.Throws<InvalidDataException>(() => fileSystem.ReadAllTextBounded(path, wideContent.Length));
                AssertFileCanBeOpenedExclusively(path);
                File.Delete(path);
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void BoundedTextRead_RejectsARealFileAboveTheLimitAndReleasesTheHandle()
        {
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "BuildAssistantBoundedRead-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(temporaryRoot, "history.json");
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                File.WriteAllBytes(path, new byte[17]);
                var fileSystem = new BuildAssistantFileSystem();

                Assert.Throws<InvalidDataException>(() => fileSystem.ReadAllTextBounded(path, 16));
                AssertFileCanBeOpenedExclusively(path);
                File.Delete(path);
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void BoundedTextRead_RejectsARealFileThatGrowsAfterTheInitialLengthCheck()
        {
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "BuildAssistantBoundedRead-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(temporaryRoot, "history.json");
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                File.WriteAllBytes(path, Encoding.UTF8.GetBytes("1234"));
                using (var stream = new GrowingFileReadStream(path, Encoding.UTF8.GetBytes("56789012345678901234567890123456")))
                    Assert.Throws<InvalidDataException>(() => BuildAssistantFileSystem.ReadStreamTextBounded(stream, 16));

                AssertFileCanBeOpenedExclusively(path);
                File.Delete(path);
                Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, true);
            }
        }

        [Test]
        public void Containment_DoesNotAcceptSiblingPrefixPaths()
        {
            var boundary = Path.Combine(BuildAssistantTestData.OutputRoot, "Run");

            Assert.That(SafeBuildOutput.IsContained(boundary, Path.Combine(boundary, "Player.exe")), Is.True);
            Assert.That(SafeBuildOutput.IsContained(boundary, boundary + "-sibling"), Is.False);
        }

        [Test]
        public void ExecutionGuard_RejectsReentryUntilTheLeaseIsDisposed()
        {
            Assert.That(ExecutionGuard.TryEnter(out var first), Is.True);
            try
            {
                Assert.That(ExecutionGuard.TryEnter(out var second), Is.False);
                Assert.That(second, Is.Null);
            }
            finally
            {
                first.Dispose();
            }

            Assert.That(ExecutionGuard.TryEnter(out var next), Is.True);
            next.Dispose();
        }

        [Test]
        public void Inspect_RejectsWindowsDeviceAliasesBeforeContainmentChecks()
        {
            if (Path.DirectorySeparatorChar != '\\')
                Assert.Ignore("Windows device namespaces only apply on Windows.");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot, Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets"));
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);
            var alias = "\\\\?\\" + Path.Combine(BuildAssistantTestData.ProjectRoot, "Assets");

            Assert.That(output.Inspect(alias).Error, Is.EqualTo(BuildAssistantError.InvalidOutputRoot));
        }

        [TestCase("NUL")]
        [TestCase("CON.json")]
        [TestCase("AUX.")]
        [TestCase("COM1")]
        [TestCase("LPT9.txt")]
        [TestCase("CONIN$.json")]
        [TestCase("CONOUT$")]
        [TestCase("COM\u00B9.json")]
        [TestCase("LPT\u00B3")]
        public void Inspect_RejectsWindowsDeviceComponentsBeforeFilesystemProbes(string deviceComponent)
        {
            if (Path.DirectorySeparatorChar != '\\')
                Assert.Ignore("Windows device names only apply on Windows.");
            var candidate = Path.Combine(BuildAssistantTestData.OutputRoot, deviceComponent);

            Assert.That(LocationPolicy.IsFullyQualifiedPath(candidate), Is.False);
        }

        [Test]
        public void Inspect_RejectsWindowsUncPathsBeforeContainmentChecks()
        {
            if (Path.DirectorySeparatorChar != '\\')
                Assert.Ignore("UNC paths only apply on Windows.");
            var fake = new FakeBuildAssistantFileSystem(BuildAssistantTestData.ProjectRoot);
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);

            Assert.That(output.Inspect("\\\\localhost\\c$\\Project\\Assets").Error, Is.EqualTo(BuildAssistantError.InvalidOutputRoot));
        }

        [Test]
        public void Inspect_RejectsARealWindowsShortNameAliasOfAssetsWhenAvailable()
        {
            if (Path.DirectorySeparatorChar != '\\')
                Assert.Ignore("Windows short paths only apply on Windows.");
            var assets = UnityEngine.Application.dataPath;
            var shortAssets = GetShortPath(assets);
            if (string.IsNullOrEmpty(shortAssets) || StringComparer.OrdinalIgnoreCase.Equals(shortAssets, assets))
                Assert.Ignore("This volume does not expose a distinct short name for the project Assets directory.");
            var projectRoot = Directory.GetParent(assets).FullName;
            var output = new SafeBuildOutput(projectRoot);

            Assert.That(output.Inspect(shortAssets).Error, Is.EqualTo(BuildAssistantError.UnsafeOutputPath));
        }

        private static SafeBuildOutput CreateOutputWithFake(out FakeBuildAssistantFileSystem fake)
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            return new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);
        }

        /// <summary>読み込み処理の終了後に、別の読み書きを拒む形で実ファイルを開けることを確認します。</summary>
        private static void AssertFileCanBeOpenedExclusively(string path)
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }
        }

        private static string GetShortPath(string path)
        {
            var buffer = new StringBuilder(512);
            var length = GetShortPathNameW(path, buffer, (uint)buffer.Capacity);
            if (length == 0)
                return string.Empty;
            if (length >= buffer.Capacity)
            {
                buffer = new StringBuilder(checked((int)length + 1));
                length = GetShortPathNameW(path, buffer, (uint)buffer.Capacity);
            }
            return length == 0 ? string.Empty : buffer.ToString();
        }

        /// <summary>最初の読み込み直前に実ファイルを増やし、長さ確認後の変化を決定的に再現します。</summary>
        private sealed class GrowingFileReadStream : Stream
        {
            private readonly string path;
            private readonly byte[] appendedContent;
            private readonly FileStream reader;
            private bool contentAppended;

            internal GrowingFileReadStream(string path, byte[] appendedContent)
            {
                this.path = path;
                this.appendedContent = appendedContent;
                reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => reader.Length;
            public override long Position
            {
                get => reader.Position;
                set => reader.Position = value;
            }

            public override void Flush() => reader.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                AppendContentOnce();
                return reader.Read(buffer, offset, count);
            }

            public override int ReadByte()
            {
                AppendContentOnce();
                return reader.ReadByte();
            }

            public override long Seek(long offset, SeekOrigin origin) => reader.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    reader.Dispose();
                base.Dispose(disposing);
            }

            private void AppendContentOnce()
            {
                if (contentAppended)
                    return;
                using (var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    writer.Write(appendedContent, 0, appendedContent.Length);
                    writer.Flush(true);
                }
                contentAppended = true;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathNameW(string longPath, StringBuilder shortPath, uint shortPathLength);
    }
}
