using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using AuditEditor = LocalizationKeyAudit.Editor;

namespace LocalizationKeyAudit.Tests
{
    /// <summary>
    /// 監査条件、未加工事前検査、型として読み取る取得元、解析器の順序と例外隔離を検証します。
    /// </summary>
    internal sealed class LocalizationKeyAuditServiceTests
    {
        /// <summary>
        /// 監査条件の失敗は未加工取得元と型として読み取る取得元のどちらも呼ばず、監査停止問題1件だけを返します。
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

        /// <summary>論理ルートが混在した要求を取得元の呼び出し前に拒否し、元の網羅情報にあるパス／参照も返しません。</summary>
        [Test]
        public void Audit_MixedRootRequestCallsNeitherSourceAndDiscardsCoverage()
        {
            var coverage = new AuditEditor.LocalizationKeyAuditCoverage(
                "異なる論理ルート",
                new[] { "Assets", "Packages/com.example" },
                new[]
                {
                    new AuditEditor.LocalizationKeyAuditStaticReference(
                        "Assets/Scenes/Main.unity",
                        LocalizationKeyAuditTestData.CollectionGuid,
                        10,
                        "UI",
                        "Start")
                },
                true,
                string.Empty);
            var request = new AuditEditor.LocalizationKeyAuditRequest(new[] { "en" }, coverage);
            var raw = new FakeLocalizationKeyAuditRawSource();
            var typed = new FakeLocalizationKeyAuditTypedSource();

            var result = AuditEditor.LocalizationKeyAuditService.Audit(request, raw, typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration);
            Assert.That(result.Coverage.DeclaredAssetPaths, Is.Empty);
            Assert.That(result.Coverage.RecognizedReferences, Is.Empty);
            Assert.That(raw.ReadCallCount, Is.Zero);
            Assert.That(typed.ReadCallCount, Is.Zero);
        }

        /// <summary>入力スナップショット例外は型名だけを返し、非公開本文と網羅情報を公開しません。</summary>
        [Test]
        public void Audit_InputSnapshotExceptionDoesNotExposeOpaqueDetails()
        {
            const string physicalCanary = "C:\\private\\input-snapshot-canary";

            var result = AuditEditor.LocalizationKeyAuditService.Audit(
                new ThrowingStringList(physicalCanary),
                "Assets",
                new[] { "Assets" });

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.InvalidConfiguration);
            Assert.That(result.Coverage.DeclaredAssetPaths, Is.Empty);
            Assert.That(result.Coverage.RecognizedReferences, Is.Empty);
            Assert.That(
                result.Issues[0].Message,
                Is.EqualTo("監査入力のスナップショットを作成できません: InvalidOperationException"));
            AssertIssueDoesNotExpose(result.Issues[0], physicalCanary);
        }

        /// <summary>
        /// 未加工のYAMLの後半で失敗しても型として読み取る取得元を呼ばず、検証済みの前半を結果へ残しません。
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

        /// <summary>不正な未加工アセットパスを問題項目や網羅情報へ移さず、型として読み取る前に停止します。</summary>
        [Test]
        public void Audit_InvalidRawAssetPathDoesNotExposePhysicalCanary()
        {
            const string physicalCanary = "C:\\private\\raw-asset-path-canary.asset";
            var raw = new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[]
                {
                    LocalizationKeyAuditTestData.CreateRawAsset(
                        physicalCanary,
                        LocalizationKeyAuditTestData.CreateYamlBytes(LocalizationKeyAuditTestData.CollectionGuid),
                        Path.GetFullPath("C:/Project/Localization/UI Shared Data.asset"))
                }
            };
            var typed = new FakeLocalizationKeyAuditTypedSource();

            var result = AuditEditor.LocalizationKeyAuditService.Audit(CreateRequest(), raw, typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable);
            Assert.That(result.Coverage.RecognizedReferences, Has.Count.EqualTo(1));
            Assert.That(result.Issues[0].AssetPath, Is.Empty);
            AssertIssueDoesNotExpose(result.Issues[0], physicalCanary);
            Assert.That(raw.ReadCallCount, Is.EqualTo(1));
            Assert.That(typed.ReadCallCount, Is.Zero);
        }

        /// <summary>
        /// スクリプトとGUID項目が別文書にある偽装では、前半の識別情報を捨てて型として読み取る前に停止します。
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
        /// 未加工データ取得元の例外をReadOnlyGuaranteeUnavailableに限定し、型として読み取る取得元を呼びません。
        /// </summary>
        [Test]
        public void Audit_RawSourceExceptionIsIsolatedBeforeTypedLoad()
        {
            const string physicalCanary = "C:\\private\\raw-source-canary";
            var raw = new FakeLocalizationKeyAuditRawSource
            {
                Exception = new InvalidOperationException(physicalCanary)
            };
            var typed = new FakeLocalizationKeyAuditTypedSource();

            var result = AuditEditor.LocalizationKeyAuditService.Audit(CreateRequest(), raw, typed);

            AssertTerminalFailure(result, AuditEditor.LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable);
            StringAssert.Contains("InvalidOperationException", result.Issues[0].Message);
            AssertIssueDoesNotExpose(result.Issues[0], physicalCanary);
            Assert.That(raw.ReadCallCount, Is.EqualTo(1));
            Assert.That(typed.ReadCallCount, Is.Zero);
        }

        /// <summary>
        /// 型として読み取る取得元の欠落、参照なしのスナップショット、例外をAuditFailed 1件へ隔離します。
        /// </summary>
        [Test]
        public void Audit_TypedSourceFailuresDiscardPartialData()
        {
            var missingResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                null);
            AssertTerminalFailure(missingResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);
            Assert.That(
                missingResult.Issues[0].Message,
                Is.EqualTo("型付きローカライズ情報の取得元がありません。"));

            var nullSnapshot = new FakeLocalizationKeyAuditTypedSource { Snapshot = null };
            var nullResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                nullSnapshot);
            AssertTerminalFailure(nullResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);
            Assert.That(
                nullResult.Issues[0].Message,
                Is.EqualTo("型として読み取るローカライズ監査に失敗しました: InvalidDataException"));
            Assert.That(nullSnapshot.ReadCallCount, Is.EqualTo(1));

            const string physicalCanary = "C:\\private\\typed-source-canary";
            var throwing = new FakeLocalizationKeyAuditTypedSource
            {
                Exception = new InvalidOperationException(physicalCanary)
            };
            var exceptionResult = AuditEditor.LocalizationKeyAuditService.Audit(
                CreateRequest(),
                CreateValidRawSource(),
                throwing);
            AssertTerminalFailure(exceptionResult, AuditEditor.LocalizationKeyAuditIssueKind.AuditFailed);
            Assert.That(
                exceptionResult.Issues[0].Message,
                Is.EqualTo("型として読み取るローカライズ監査に失敗しました: InvalidOperationException"));
            AssertIssueDoesNotExpose(exceptionResult.Issues[0], physicalCanary);
            Assert.That(throwing.ReadCallCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 型として読み取る変換処理が検出した固定上限超過をAuditFailedと混同しません。
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
        /// 型として読み取ったコレクション、所属先なしテーブル、アセットテーブルの所有元にあるパスやGUIDが未加工の識別情報と違えば、監査停止失敗にします。
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
        /// 全未加工アセットを検証した後にだけスナップショットを型として一度読み、完全な結果を返します。
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

        /// <summary>正常な要求と網羅情報を作ります。</summary>
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

        /// <summary>正常な未加工識別情報を1件返す取得元を作ります。</summary>
        private static FakeLocalizationKeyAuditRawSource CreateValidRawSource()
        {
            return new FakeLocalizationKeyAuditRawSource
            {
                Assets = new[] { LocalizationKeyAuditTestData.CreateValidRawAsset() }
            };
        }

        /// <summary>直接項目が揃った、型として読み取った最小のスナップショットを作ります。</summary>
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

        /// <summary>監査停止失敗が、型として読み取った部分データを含まないことを検証します。</summary>
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

            /// <summary>画面表示とコピーに使う問題文字列へ、検査用文字列がないことを確認します。</summary>
        private static void AssertIssueDoesNotExpose(
            AuditEditor.LocalizationKeyAuditIssue issue,
            string canary)
        {
            var displayedAndCopiedText = string.Join("\n", new[]
            {
                issue.Message,
                issue.AssetPath,
                issue.RelatedAssetPath,
                issue.CollectionName,
                issue.LocaleIdentifier,
                issue.EntryKey
            });
            StringAssert.DoesNotContain(canary, displayedAndCopiedText);
        }

            /// <summary>添字アクセスで指定例外を送出する入力一覧です。</summary>
        private sealed class ThrowingStringList : IReadOnlyList<string>
        {
            /// <summary>不透明な例外本文を保持します。</summary>
            internal ThrowingStringList(string message)
            {
                m_Message = message;
            }

            /// <summary>複製を開始させる1件を報告します。</summary>
            public int Count => 1;

            /// <summary>スナップショット作成中の入力失敗を再現します。</summary>
            public string this[int index] => throw new InvalidOperationException(m_Message);

            /// <summary>列挙中の入力失敗を再現します。</summary>
            public IEnumerator<string> GetEnumerator()
            {
                throw new InvalidOperationException(m_Message);
            }

            /// <summary>非ジェネリック列挙を同じ失敗へ揃えます。</summary>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            /// <summary>外部へ出してはいけない例外本文です。</summary>
            private readonly string m_Message;
        }
    }
}
