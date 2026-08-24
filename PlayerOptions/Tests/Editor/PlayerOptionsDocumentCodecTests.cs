// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PlayerOptions.Editor.Tests
{
    /// <summary>schema、sentinel、JSON codec、future/corrupt分類、migration seamを確認する。</summary>
    internal sealed class PlayerOptionsDocumentCodecTests
    {
        [Test]
        public void Schema_VersionAndInputLimitAreFrozenForVersionOne()
        {
            Assert.That(PlayerOptionsSchema.CurrentVersion, Is.EqualTo(1));
            Assert.That(PlayerOptionsSchema.MaximumDocumentLength, Is.EqualTo(16 * 1024));
        }

        [Test]
        public void Document_DefaultSentinelsDetectEveryMissingRequiredField()
        {
            var document = new PlayerOptionsDocument();

            Assert.That(document.HasAllRequiredFields, Is.False);
            Assert.That(document.SchemaVersion, Is.EqualTo(int.MinValue));
            Assert.That(document.QualityLevelName, Is.Null);
        }

        [Test]
        public void Codec_CurrentDocumentRoundTripsEveryStateValue()
        {
            var codec = CreateCodec();
            var state = PlayerOptionsTestData.CreateState(
                width: 2560,
                height: 1440,
                fullScreenMode: UnityEngine.FullScreenMode.FullScreenWindow,
                refreshNumerator: 60000,
                refreshDenominator: 1001,
                targetFrameRate: 120,
                masterVolume: 0.5f,
                qualityIndex: 2,
                qualityName: "Ultra");

            Assert.That(codec.TryEncode(state, out var contents, out var encodeMessage), Is.True, encodeMessage);
            Assert.That(contents, Does.Contain("\"SchemaVersion\":1"));
            Assert.That(contents, Does.Contain("\"QualityLevelName\":\"Ultra\""));
            Assert.That(
                codec.TryDecode(
                    contents,
                    out var decoded,
                    out var wasMigrated,
                    out var error,
                    out var decodeMessage),
                Is.True,
                decodeMessage);
            Assert.That(decoded, Is.EqualTo(state));
            Assert.That(wasMigrated, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
        }

        [Test]
        public void Codec_MaximumRefreshNumeratorRoundTripsWithoutCollidingWithMissingFieldDetection()
        {
            var codec = CreateCodec();
            var state = PlayerOptionsTestData.CreateState(
                refreshNumerator: uint.MaxValue,
                refreshDenominator: 1);

            Assert.That(codec.TryEncode(state, out var contents, out var encodeMessage), Is.True, encodeMessage);
            Assert.That(
                codec.TryDecode(
                    contents,
                    out var decoded,
                    out var wasMigrated,
                    out var error,
                    out var decodeMessage),
                Is.True,
                decodeMessage);
            Assert.That(decoded, Is.EqualTo(state));
            Assert.That(wasMigrated, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
        }

        [Test]
        public void Codec_RefreshValueBeyondUIntRangeIsCorrupt()
        {
            var codec = CreateCodec();
            Assert.That(
                codec.TryEncode(
                    PlayerOptionsTestData.CreateDefaultState(),
                    out var contents,
                    out var encodeMessage),
                Is.True,
                encodeMessage);
            Assert.That(contents, Does.Contain("\"RefreshRateNumerator\":60"));
            contents = contents.Replace(
                "\"RefreshRateNumerator\":60",
                "\"RefreshRateNumerator\":4294967296");

            var success = codec.TryDecode(
                contents,
                out _,
                out _,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.CorruptData));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{")]
        [TestCase("null")]
        [TestCase("{\"SchemaVersion\":1}")]
        [TestCase("{\"SchemaVersion\":0}")]
        [TestCase("{\"SchemaVersion\":-1}")]
        public void Codec_EmptyMalformedOrMissingDocumentIsCorrupt(string contents)
        {
            var success = CreateCodec().TryDecode(
                contents,
                out var state,
                out var wasMigrated,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(state, Is.EqualTo(default(PlayerOptionsState)));
            Assert.That(wasMigrated, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.CorruptData));
        }

        [Test]
        public void Codec_OversizedDocumentIsRejectedBeforeParsing()
        {
            var contents = new string('x', PlayerOptionsSchema.MaximumDocumentLength + 1);

            var success = CreateCodec().TryDecode(
                contents,
                out _,
                out _,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.CorruptData));
        }

        [Test]
        public void Codec_FutureSchemaIsUnsupportedAndNeverMigrated()
        {
            const string contents = "{\"SchemaVersion\":2}";

            var success = CreateCodec().TryDecode(
                contents,
                out _,
                out var wasMigrated,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(wasMigrated, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.UnsupportedSchemaVersion));
        }

        [Test]
        public void Codec_FutureHeaderWinsBeforeInvalidBodyDeserialization()
        {
            const string contents =
                "{\"SchemaVersion\":2,\"DisplayWidth\":\"not-an-integer\"}";

            var success = CreateCodec().TryDecode(
                contents,
                out _,
                out var wasMigrated,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(wasMigrated, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.UnsupportedSchemaVersion));
            Assert.That(typeof(PlayerOptionsDocumentHeader).IsPublic, Is.False);
        }

        [Test]
        public void Codec_UnknownFieldsDoNotChangeKnownCurrentState()
        {
            var codec = CreateCodec();
            var state = PlayerOptionsTestData.CreateDefaultState();
            Assert.That(codec.TryEncode(state, out var encoded, out _), Is.True);
            var contents = encoded.Substring(0, encoded.Length - 1) + ",\"FutureHint\":42}";

            var success = codec.TryDecode(
                contents,
                out var decoded,
                out _,
                out var error,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(decoded, Is.EqualTo(state));
        }

        [Test]
        public void MigrationPipeline_CurrentDocumentReturnsSameInstanceWithoutMigration()
        {
            var document = PlayerOptionsDocument.FromState(PlayerOptionsTestData.CreateDefaultState());

            var success = PlayerOptionsMigrationPipeline.Default.TryMigrate(
                document,
                out var migrated,
                out var wasMigrated,
                out var error,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(migrated, Is.SameAs(document));
            Assert.That(wasMigrated, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
        }

        [Test]
        public void MigrationPipeline_FutureDocumentIsRejectedBeforeRegisteredMigration()
        {
            var migration = new FakePlayerOptionsMigration(1, 2);
            var pipeline = CreateTargetPipeline(2, migration);
            var document = PlayerOptionsDocument.FromState(PlayerOptionsTestData.CreateDefaultState());
            document.SchemaVersion = 3;

            var success = pipeline.TryMigrate(
                document,
                out _,
                out var wasMigrated,
                out var error,
                out _);

            Assert.That(success, Is.False);
            Assert.That(wasMigrated, Is.False);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.UnsupportedSchemaVersion));
            Assert.That(migration.CallCount, Is.Zero);
        }

        [Test]
        public void MigrationPipeline_RejectsNullNonAdjacentAndDuplicateRegistrations()
        {
            Assert.That(
                () => new PlayerOptionsMigrationPipeline(null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new PlayerOptionsMigrationPipeline(
                    new IPlayerOptionsDocumentMigration[] { null }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new PlayerOptionsMigrationPipeline(
                    new IPlayerOptionsDocumentMigration[]
                    {
                        new FakePlayerOptionsMigration(1, 3),
                    }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new PlayerOptionsMigrationPipeline(
                    new IPlayerOptionsDocumentMigration[]
                    {
                        new FakePlayerOptionsMigration(1, 2),
                        new FakePlayerOptionsMigration(1, 2),
                    }),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void MigrationPipeline_TargetVersionSeamRunsSyntheticOneToTwoMigration()
        {
            var migration = new FakePlayerOptionsMigration(1, 2);
            var pipeline = CreateTargetPipeline(2, migration);
            var codec = new PlayerOptionsDocumentCodec(pipeline);
            var raw = PlayerOptionsTestData.Encode(PlayerOptionsTestData.CreateDefaultState());

            var success = codec.TryDecode(
                raw,
                out var state,
                out var wasMigrated,
                out var error,
                out var message);

            Assert.That(success, Is.True, message);
            Assert.That(state, Is.EqualTo(PlayerOptionsTestData.CreateDefaultState()));
            Assert.That(wasMigrated, Is.True);
            Assert.That(error, Is.EqualTo(PlayerOptionsError.None));
            Assert.That(migration.CallCount, Is.EqualTo(1));
            Assert.That(pipeline.TargetVersion, Is.EqualTo(2));
        }

        [Test]
        public void MigrationPipeline_ThrownCallbackReturnsMigrationFailed()
        {
            var migration = new FakePlayerOptionsMigration(1, 2)
            {
                MigrationException = new InvalidOperationException("migration failure"),
            };
            var pipeline = CreateTargetPipeline(2, migration);
            var codec = new PlayerOptionsDocumentCodec(pipeline);
            var raw = PlayerOptionsTestData.Encode(PlayerOptionsTestData.CreateDefaultState());

            var success = codec.TryDecode(
                raw,
                out _,
                out var wasMigrated,
                out var error,
                out var message);

            Assert.That(success, Is.False);
            Assert.That(wasMigrated, Is.False);
            Assert.That(
                error,
                Is.EqualTo(PlayerOptionsError.MigrationFailed));
            Assert.That(message, Does.Contain("migration failure"));
            Assert.That(migration.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void MigrationPipeline_TargetVersionRequiresCompleteUniqueContiguousSteps()
        {
            Assert.That(
                () => CreateTargetPipeline(0),
                Throws.Exception);
            Assert.That(
                () => CreateTargetPipeline(2),
                Throws.Exception);
            Assert.That(
                () => CreateTargetPipeline(
                    3,
                    new FakePlayerOptionsMigration(1, 2)),
                Throws.Exception);
            Assert.That(
                () => CreateTargetPipeline(
                    3,
                    new FakePlayerOptionsMigration(1, 2),
                    new FakePlayerOptionsMigration(1, 2)),
                Throws.Exception);
            Assert.That(
                () => CreateTargetPipeline(
                    3,
                    new FakePlayerOptionsMigration(1, 2),
                    new FakePlayerOptionsMigration(2, 3)),
                Throws.Nothing);
            Assert.That(PlayerOptionsMigrationPipeline.Default.TargetVersion, Is.EqualTo(1));
        }

        private static PlayerOptionsDocumentCodec CreateCodec()
        {
            return new PlayerOptionsDocumentCodec(PlayerOptionsMigrationPipeline.Default);
        }

        private static PlayerOptionsMigrationPipeline CreateTargetPipeline(
            int targetVersion,
            params IPlayerOptionsDocumentMigration[] migrations)
        {
            IReadOnlyList<IPlayerOptionsDocumentMigration> migrationList = migrations;
            return new PlayerOptionsMigrationPipeline(targetVersion, migrationList);
        }
    }
}
