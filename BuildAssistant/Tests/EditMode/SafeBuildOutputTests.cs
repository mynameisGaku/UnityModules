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
        public void Reserve_CreatesOnlyTheNewRunAndUsesSingleOwnerReservation()
        {
            var parent = Path.GetDirectoryName(BuildAssistantTestData.OutputRoot);
            var fake = new FakeBuildAssistantFileSystem(parent, BuildAssistantTestData.OutputRoot, BuildAssistantTestData.ProjectRoot);
            var output = new SafeBuildOutput(BuildAssistantTestData.ProjectRoot, fake);
            var plan = BuildAssistantTestData.Plan();

            using (var reservation = output.Reserve(plan))
            {
                Assert.That(reservation.IsReserved, Is.True);
                Assert.That(fake.DirectoryExists(plan.RunDirectory), Is.True);
                Assert.That(output.IsRunPathBusy(plan.OutputRoot, plan.RunId), Is.True);
            }

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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathNameW(string longPath, StringBuilder shortPath, uint shortPathLength);
    }
}
