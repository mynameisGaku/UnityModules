using System;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// request、raw preflight、typed source、analyzer の順序と例外隔離を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditServiceTests
    {
        /// <summary>
        /// request failure は raw/typed source のどちらも呼ばず terminal issue 一件だけを返します。
        /// </summary>
        [Test]
        public void Audit_InvalidRequestCallsNeitherSource()
        {
            var raw = new FakeLocalizationKeyAuditRawSource();
            var typed = new FakeLocalizationKeyAuditTypedSource();

            var result = AuditEditor.LocalizationKeyAuditService.Audit(null, raw, typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration);
            Assert.That(raw.ReadCallCount, Is.Zero);
            Assert.That(typed.ReadCallCount, Is.Zero);
        }

        /// <summary>
        /// raw YAML の後半で失敗しても typed source を呼ばず、検証済み前半を結果へ残しません。
        /// </summary>
        [Test]
        public void Audit_RawFailureCallsTypedZeroTimesAndDiscardsPartialData()
        {
            var request = CreateRequest();
            var raw = new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[]
                {
                    LocalizationKeyAuditTestData.CreateValidRawAsset(
                        "Assets/Localization/A Shared Data.asset",
                        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
                    LocalizationKeyAuditTestData.CreateRawAsset(
                        "Assets/Localization/Z Shared Data.asset",
                        Array.Empty<byte>())
                }
            };
            var typed = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = CreateCompleteSnapshot()
            };

            var result = AuditEditor.LocalizationKeyAuditService.Audit(request, raw, typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable);
            Assert.That(result.Issues[0].AssetPath, Is.EqualTo("Assets/Localization/Z Shared Data.asset"));
            Assert.That(result.Coverage.ScopeDescription, Is.EqualTo("Scenes"));
            Assert.That(raw.ReadCallCount, Is.EqualTo(1));
            Assert.That(typed.ReadCallCount, Is.Zero);
        }

        /// <summary>
        /// scriptとGUID fieldが別documentにあるspoofは、前半identityを捨てtyped load前に停止します。
        /// </summary>
        [Test]
        public void Audit_UncorrelatedRawYamlCallsTypedZeroTimesAndDiscardsPartialIdentity()
        {
            var targetScriptGuid = AuditEditor.UnityLocalizationKeyAuditRawSource.SharedTableDataScriptGuid;
            var yaml =
                "%YAML 1.1\n" +
                "%TAG !u! tag:unity3d.com,2011:\n" +
                "--- !u!114 &11400000\n" +
                "MonoBehaviour:\n" +
                $"  m_Script: {{fileID: 11500000, guid: {targetScriptGuid}, type: 3}}\n" +
                "--- !u!114 &11400001\n" +
                "MonoBehaviour:\n" +
                "  m_Script: {fileID: 11500000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}\n" +
                $"  {AuditEditor.LocalizationKeyAuditRawPreflight.CollectionGuidFieldName}: {LocalizationKeyAuditTestData.CollectionGuid:N}\n";
            var raw = new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[]
                {
                    LocalizationKeyAuditTestData.CreateValidRawAsset(
                        "Assets/Localization/A Shared Data.asset",
                        Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
                    LocalizationKeyAuditTestData.CreateRawAsset(
                        "Assets/Localization/Z Shared Data.asset",
                        LocalizationKeyAuditTestData.Utf8(yaml))
                }
            };
            var typed = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = CreateCompleteSnapshot()
            };

            var result = AuditEditor.LocalizationKeyAuditService.Audit(CreateRequest(), raw, typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable);
            Assert.That(result.Issues[0].AssetPath, Is.EqualTo("Assets/Localization/Z Shared Data.asset"));
            Assert.That(raw.ReadCallCount, Is.EqualTo(1));
            Assert.That(typed.ReadCallCount, Is.Zero);
        }

        /// <summary>
        /// raw source の例外を ReadOnlyGuaranteeUnavailable に限定し typed source を呼びません。
        /// </summary>
        [Test]
        public void Audit_RawSourceExceptionIsIsolatedBeforeTypedLoad()
        {
            var raw = new FakeLocalizationKeyAuditRawSource
            {
                Exception = new InvalidOperationException("raw boom")
            };
            var typed = new FakeLocalizationKeyAuditTypedSource();

            var result = AuditEditor.LocalizationKeyAuditService.Audit(CreateRequest(), raw, typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable);
            StringAssert.Contains("InvalidOperationException: raw boom", result.Issues[0].Message);
            Assert.That(raw.ReadCallCount, Is.EqualTo(1));
            Assert.That(typed.ReadCallCount, Is.Zero);
        }

        /// <summary>
        /// typed source の欠落、null snapshot、例外を AuditFailed 一件へ隔離します。
        /// </summary>
        [Test]
        public void Audit_TypedSourceFailuresDiscardPartialData()
        {
            var missingResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                null);
            AssertTerminalFailure(missingResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);

            var nullSnapshot = new FakeLocalizationKeyAuditTypedSource { Snapshot = null };
            var nullResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                nullSnapshot);
            AssertTerminalFailure(nullResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);
            Assert.That(nullSnapshot.ReadCallCount, Is.EqualTo(1));

            var throwing = new FakeLocalizationKeyAuditTypedSource
            {
                Exception = new InvalidOperationException("typed boom")
            };
            var exceptionResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                throwing);
            AssertTerminalFailure(exceptionResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);
            StringAssert.Contains("InvalidOperationException: typed boom", exceptionResult.Issues[0].Message);
            Assert.That(throwing.ReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// typed adapter が検出した hard limit 超過を AuditFailed と混同しません。
        /// </summary>
        [Test]
        public void Audit_TypedLimitExceptionReturnsLimitExceeded()
        {
            var typed = new FakeLocalizationKeyAuditTypedSource
            {
                Exception = new AuditEditor.LocalizationKeyAuditLimitException("typed limit")
            };

            var result = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.LimitExceeded);
            Assert.That(result.Issues[0].Message, Is.EqualTo("typed limit"));
            Assert.That(typed.ReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// typed collection、orphan table、Asset Table ownerのpath/GUIDがraw identityと違えばterminal failureにします。
        /// </summary>
        [Test]
        public void Audit_TypedAndRawIdentityMismatchDiscardsTypedSnapshot()
        {
            var mismatchedCollection = new AuditEditor.LocalizationKeyAuditCollectionSnapshot(
                "UI",
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                "Assets/Localization/UI Shared Data.asset",
                Array.Empty<AuditEditor.LocalizationKeyAuditSharedEntrySnapshot>(),
                Array.Empty<AuditEditor.LocalizationKeyAuditLocaleTableSnapshot>());
            var collectionTyped = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                    new[] { "en" },
                    new[] { mismatchedCollection })
            };
            var collectionResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                collectionTyped);
            AssertTerminalFailure(collectionResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);

            var orphan = new AuditEditor.LocalizationKeyAuditOrphanLocaleTableSnapshot(
                "Assets/Localization/UI Shared Data.asset",
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                new AuditEditor.LocalizationKeyAuditLocaleTableSnapshot(
                    "en",
                    "Assets/Localization/Orphan_en.asset",
                    Array.Empty<AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot>()));
            var orphanTyped = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                    new[] { "en" },
                    Array.Empty<AuditEditor.LocalizationKeyAuditCollectionSnapshot>(),
                    new[] { orphan })
            };
            var orphanResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                orphanTyped);
            AssertTerminalFailure(orphanResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);

            var assetTyped = new FakeLocalizationKeyAuditTypedSource
            {
                Snapshot = new AuditEditor.LocalizationKeyAuditTypedSnapshot(
                    new[] { "en" },
                    Array.Empty<AuditEditor.LocalizationKeyAuditCollectionSnapshot>(),
                    nonStringSharedDataIdentities: new[]
                    {
                        new AuditEditor.LocalizationKeyAuditNonStringSharedDataIdentity(
                            "Assets/Localization/Asset Shared Data.asset",
                            LocalizationKeyAuditTestData.CollectionGuid)
                    })
            };
            var assetResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                assetTyped);
            AssertTerminalFailure(assetResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);
            Assert.That(collectionTyped.ReadCallCount, Is.EqualTo(1));
            Assert.That(orphanTyped.ReadCallCount, Is.EqualTo(1));
            Assert.That(assetTyped.ReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 全 raw asset を検証した後にだけ typed snapshot を一度読み、完全な結果を返します。
        /// </summary>
        [Test]
        public void Audit_ValidSourcesCallEachAdapterOnceAndReturnCompleteResult()
        {
            var raw = CreateValidRawSource();
            var typed = new FakeLocalizationKeyAuditTypedSource { Snapshot = CreateCompleteSnapshot() };

            var result = AuditEditor.LocalizationKeyAuditService.Audit(CreateRequest(), raw, typed);

            Assert.That(result.IsComplete, Is.True, string.Join("\n", result.Issues.Select(issue => issue.Message)));
            Assert.That(raw.ReadCallCount, Is.EqualTo(1));
            Assert.That(typed.ReadCallCount, Is.EqualTo(1));
            Assert.That(result.LocaleIdentifiers, Is.EqualTo(new[] { "en" }));
            Assert.That(result.Collections, Has.Count.EqualTo(1));
        }

        /// <summary>valid request と coverage を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditRequest CreateRequest()
        {
            var reference = new AuditEditor.LocalizationKeyAuditStaticReference(
                "Assets/Scenes/Main.unity",
                LocalizationKeyAuditTestData.CollectionGuid,
                10,
                "UI",
                "Start");
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                "Scenes",
                new[] { "Assets/Scenes" },
                new[] { reference },
                true,
                string.Empty);
            return new AuditEditor.LocalizationKeyAuditRequest(new[] { "en" }, coverage);
        }

        /// <summary>valid raw identity を一件返す source を作ります。</summary>
        private static FakeLocalizationKeyAuditRawSource CreateValidRawSource()
        {
            return new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[] { LocalizationKeyAuditTestData.CreateValidRawAsset() }
            };
        }

        /// <summary>direct entry が揃った最小 typed snapshot を作ります。</summary>
        private static AuditEditor.LocalizationKeyAuditTypedSnapshot CreateCompleteSnapshot()
        {
            var table = new AuditEditor.LocalizationKeyAuditLocaleTableSnapshot(
                "en",
                "Assets/Localization/UI_en.asset",
                new[] { new AuditEditor.LocalizationKeyAuditLocalizedEntrySnapshot(10, "Start") });
            var collection = new AuditEditor.LocalizationKeyAuditCollectionSnapshot(
                "UI",
                LocalizationKeyAuditTestData.CollectionGuid,
                "Assets/Localization/UI Shared Data.asset",
                new[] { new AuditEditor.LocalizationKeyAuditSharedEntrySnapshot(10, "Start") },
                new[] { table });
            return new AuditEditor.LocalizationKeyAuditTypedSnapshot(new[] { "en" }, new[] { collection });
        }

        /// <summary>terminal failure が partial typed data を含まないことを検証します。</summary>
        private static void AssertTerminalFailure(
            AuditEditor.LocalizationKeyAuditResult result,
            AuditEditor.LocalizationKeyAuditIssueKind expectedKind)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.LocaleIdentifiers, Is.Empty);
            Assert.That(result.Collections, Is.Empty);
            Assert.That(result.OrphanLocaleTables, Is.Empty);
            Assert.That(result.GraphEdgeCount, Is.Zero);
            Assert.That(result.Issues, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].Kind, Is.EqualTo(expectedKind));
        }
    }
}
