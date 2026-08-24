// SPDX-License-Identifier: MIT

using NUnit.Framework;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>Loadのraw保全、fallback、flags、event、storage error境界を確認する。</summary>
    internal sealed class PlayerOptionsServiceLoadTests
    {
        [Test]
        public void Load_MissingResetsDefaultsWithoutWritingOrRequestingSave()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 90)).IsSuccess,
                Is.True);
            var notifications = 0;
            service.StateChanged += _ => notifications++;
            runtime.Calls.Clear();

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.State, Is.EqualTo(service.Defaults));
            Assert.That(result.UsedDefaults, Is.True);
            Assert.That(result.WasAdjusted, Is.False);
            Assert.That(result.RequiresSave, Is.False);
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(storage.ReadCount, Is.EqualTo(1));
            Assert.That(storage.WriteCount, Is.Zero);
            Assert.That(runtime.Calls, Is.Empty);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void Load_MissingDefaultDoesNotRaiseNoOpEvent()
        {
            var service = PlayerOptionsTestData.CreateService(new FakePlayerOptionsRuntime());
            var notifications = 0;
            service.StateChanged += _ => notifications++;

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.UsedDefaults, Is.True);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void Load_CurrentDocumentUpdatesStateWithoutApplyOrWrite()
        {
            var loaded = PlayerOptionsTestData.CreateState(
                targetFrameRate: 90,
                masterVolume: 0.5f);
            var originalRaw = PlayerOptionsTestData.Encode(loaded);
            var storage = new FakePlayerOptionsStorage
            {
                Exists = true,
                Contents = originalRaw,
            };
            var runtime = new FakePlayerOptionsRuntime();
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            var notifications = 0;
            service.StateChanged += _ => notifications++;

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.State, Is.EqualTo(loaded));
            Assert.That(result.UsedDefaults, Is.False);
            Assert.That(result.WasAdjusted, Is.False);
            Assert.That(result.RequiresSave, Is.False);
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.None));
            Assert.That(storage.Contents, Is.EqualTo(originalRaw));
            Assert.That(storage.WriteCount, Is.Zero);
            Assert.That(runtime.Calls, Is.Empty);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void Load_ReorderedUniqueQualityRepairsIndexAndRequiresExplicitSave()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                QualityLevelValue = 0,
                QualityNameValues = new[] { "High", "Low", "Ultra" },
            };
            var defaults = PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "High");
            var loaded = PlayerOptionsTestData.CreateState(qualityIndex: 1, qualityName: "High");
            var raw = PlayerOptionsTestData.Encode(loaded);
            var storage = new FakePlayerOptionsStorage { Exists = true, Contents = raw };
            var service = PlayerOptionsTestData.CreateService(runtime, storage, defaults);

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.State.Quality, Is.EqualTo(defaults.Quality));
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.QualityIndexAdjusted));
            Assert.That(result.UsedDefaults, Is.False);
            Assert.That(result.WasAdjusted, Is.True);
            Assert.That(result.RequiresSave, Is.True);
            Assert.That(storage.Contents, Is.EqualTo(raw));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void Load_DuplicateQualityNameFallsBackToDefaultAndRequiresSave()
        {
            var runtime = new FakePlayerOptionsRuntime
            {
                QualityLevelValue = 2,
                QualityNameValues = new[] { "High", "High", "Ultra" },
            };
            var defaults = PlayerOptionsTestData.CreateState(qualityIndex: 2, qualityName: "Ultra");
            var loaded = PlayerOptionsTestData.CreateState(qualityIndex: 0, qualityName: "High");
            var raw = PlayerOptionsTestData.Encode(loaded);
            var storage = new FakePlayerOptionsStorage { Exists = true, Contents = raw };
            var service = PlayerOptionsTestData.CreateService(runtime, storage, defaults);

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.State.Quality, Is.EqualTo(defaults.Quality));
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.QualityFallbackUsed));
            Assert.That(result.UsedDefaults, Is.True);
            Assert.That(result.WasAdjusted, Is.True);
            Assert.That(result.RequiresSave, Is.True);
            Assert.That(storage.Contents, Is.EqualTo(raw));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void Load_UnsupportedDisplayFallsBackWithoutOverwritingRaw()
        {
            var loaded = PlayerOptionsTestData.CreateState(
                width: 2560,
                height: 1440,
                fullScreenMode: UnityEngine.FullScreenMode.ExclusiveFullScreen);
            var raw = PlayerOptionsTestData.Encode(loaded);
            var storage = new FakePlayerOptionsStorage { Exists = true, Contents = raw };
            var service = PlayerOptionsTestData.CreateService(
                new FakePlayerOptionsRuntime(),
                storage);

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.True, result.Message);
            Assert.That(result.State.Display, Is.EqualTo(service.Defaults.Display));
            Assert.That(result.Warnings, Is.EqualTo(PlayerOptionsWarning.DisplayFallbackUsed));
            Assert.That(result.UsedDefaults, Is.True);
            Assert.That(result.WasAdjusted, Is.True);
            Assert.That(result.RequiresSave, Is.True);
            Assert.That(storage.Contents, Is.EqualTo(raw));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [TestCase("{")]
        [TestCase("{\"SchemaVersion\":1}")]
        public void Load_CorruptRawPreservesStateAndRaw(string raw)
        {
            var storage = new FakePlayerOptionsStorage { Exists = true, Contents = raw };
            var service = PlayerOptionsTestData.CreateService(
                new FakePlayerOptionsRuntime(),
                storage);
            var previous = service.State;

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.CorruptData));
            Assert.That(result.State, Is.EqualTo(previous));
            Assert.That(service.State, Is.EqualTo(previous));
            Assert.That(result.UsedDefaults, Is.False);
            Assert.That(result.WasAdjusted, Is.False);
            Assert.That(result.RequiresSave, Is.False);
            Assert.That(storage.Contents, Is.EqualTo(raw));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void Load_InvalidTypedValuesAreCorruptAndPreserveRaw()
        {
            var invalid = PlayerOptionsTestData.CreateState(targetFrameRate: 0);
            var raw = PlayerOptionsTestData.Encode(invalid);
            var storage = new FakePlayerOptionsStorage { Exists = true, Contents = raw };
            var service = PlayerOptionsTestData.CreateService(
                new FakePlayerOptionsRuntime(),
                storage);

            var result = service.Load();

            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.CorruptData));
            Assert.That(result.State, Is.EqualTo(service.Defaults));
            Assert.That(storage.Contents, Is.EqualTo(raw));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void Load_FutureRawPreservesStateAndRaw()
        {
            const string raw = "{\"SchemaVersion\":2}";
            var storage = new FakePlayerOptionsStorage { Exists = true, Contents = raw };
            var service = PlayerOptionsTestData.CreateService(
                new FakePlayerOptionsRuntime(),
                storage);
            Assert.That(
                service.SetState(PlayerOptionsTestData.CreateState(targetFrameRate: 90)).IsSuccess,
                Is.True);
            var previous = service.State;

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.UnsupportedSchemaVersion));
            Assert.That(result.State, Is.EqualTo(previous));
            Assert.That(service.State, Is.EqualTo(previous));
            Assert.That(result.UsedDefaults, Is.False);
            Assert.That(result.WasAdjusted, Is.False);
            Assert.That(result.RequiresSave, Is.False);
            Assert.That(storage.Contents, Is.EqualTo(raw));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void Load_StorageReadExceptionReturnsStorageReadFailed()
        {
            var storage = new FakePlayerOptionsStorage
            {
                ReadException = new System.InvalidOperationException("read failure"),
            };
            var service = PlayerOptionsTestData.CreateService(
                new FakePlayerOptionsRuntime(),
                storage);

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.StorageReadFailed));
            Assert.That(result.State, Is.EqualTo(service.Defaults));
            Assert.That(storage.WriteCount, Is.Zero);
        }

        [Test]
        public void Load_RuntimeDefaultBecomingInvalidReturnsRuntimeUnavailable()
        {
            var runtime = new FakePlayerOptionsRuntime();
            var storage = new FakePlayerOptionsStorage
            {
                Exists = true,
                Contents = PlayerOptionsTestData.Encode(PlayerOptionsTestData.CreateDefaultState()),
            };
            var service = PlayerOptionsTestData.CreateService(runtime, storage);
            runtime.QualityNameValues = new[] { "Only" };
            runtime.QualityLevelValue = 0;

            var result = service.Load();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo(PlayerOptionsError.RuntimeUnavailable));
            Assert.That(result.State, Is.EqualTo(service.Defaults));
            Assert.That(storage.WriteCount, Is.Zero);
        }
    }
}
