// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 未加工の事前検査、型として読み取ったスナップショット、純粋な解析を順序保証付きで実行します。
    /// </summary>
    internal static class LocalizationKeyAuditService
    {
        /// <summary>既定のAssetsまたは登録済みパッケージの網羅取得元で監査条件を構築します。</summary>
        internal static LocalizationKeyAuditRequest CreateRequest(
            IReadOnlyList<string> requiredLocaleIdentifiers,
            string scopeDescription,
            IReadOnlyList<string> declaredAssetPaths)
        {
            return CreateRequest(
                requiredLocaleIdentifiers,
                scopeDescription,
                declaredAssetPaths,
                new UnityLocalizationKeyAuditCoverageSource());
        }

        /// <summary>宣言済みのAssetsまたは登録済みパッケージ範囲を走査して監査します。</summary>
        internal static LocalizationKeyAuditResult Audit(
            IReadOnlyList<string> requiredLocaleIdentifiers,
            string scopeDescription,
            IReadOnlyList<string> declaredAssetPaths)
        {
            LocalizationKeyAuditRequest provisionalRequest;
            try
            {
                provisionalRequest = new LocalizationKeyAuditRequest(
                    requiredLocaleIdentifiers,
                    new LocalizationKeyAuditCoverage(
                        scopeDescription,
                        declaredAssetPaths,
                        Array.Empty<LocalizationKeyAuditStaticReference>(),
                        true,
                        string.Empty));
            }
            catch (Exception exception)
            {
                return CreateFailure(
                    null,
                    new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.InvalidConfiguration,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        Guid.Empty,
                        string.Empty,
                        string.Empty,
                        0,
                        $"監査入力のスナップショットを作成できません: {exception.GetType().Name}"));
            }

            if (!LocalizationKeyAuditAnalyzer.TryValidateRequest(provisionalRequest, out var requestFailure))
            {
                return CreateFailure(null, requestFailure);
            }

            return Audit(CreateRequest(
                provisionalRequest.RequiredLocaleIdentifiers,
                provisionalRequest.Coverage.ScopeDescription,
                provisionalRequest.Coverage.DeclaredAssetPaths,
                new UnityLocalizationKeyAuditCoverageSource()));
        }

        /// <summary>網羅取得元を注入して監査条件を構築します。</summary>
        internal static LocalizationKeyAuditRequest CreateRequest(
            IReadOnlyList<string> requiredLocaleIdentifiers,
            string scopeDescription,
            IReadOnlyList<string> declaredAssetPaths,
            ILocalizationKeyAuditCoverageSource coverageSource)
        {
            var coverage = LocalizationKeyAuditCoverageScanner.Scan(
                scopeDescription,
                declaredAssetPaths,
                coverageSource);
            return new LocalizationKeyAuditRequest(requiredLocaleIdentifiers, coverage);
        }

        /// <summary>
        /// Unityプロジェクトの未加工取得元と型として読み取る取得元を使い、手動の助言監査を実行します。
        /// </summary>
        internal static LocalizationKeyAuditResult Audit(LocalizationKeyAuditRequest request)
        {
            return Audit(
                request,
                new UnityLocalizationKeyAuditRawSource(),
                new UnityLocalizationKeyAuditTypedSource());
        }

        /// <summary>
        /// 注入した取得元を使い、未加工取得の失敗時は型として読み取る取得元を一度も呼ばず監査停止結果を返します。
        /// </summary>
        internal static LocalizationKeyAuditResult Audit(
            LocalizationKeyAuditRequest request,
            ILocalizationKeyAuditRawSource rawSource,
            ILocalizationKeyAuditTypedSource typedSource)
        {
            if (!LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var requestFailure))
            {
                return CreateFailure(null, requestFailure);
            }

            if (!LocalizationKeyAuditRawPreflight.TryRun(
                    rawSource,
                    out var rawIdentities,
                    out var failureAssetPath,
                    out var failureMessage))
            {
                return CreateFailure(
                    request.Coverage,
                    new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.ReadOnlyGuaranteeUnavailable,
                        failureAssetPath,
                        string.Empty,
                        string.Empty,
                        Guid.Empty,
                        string.Empty,
                        string.Empty,
                        0,
                        failureMessage));
            }

            if (typedSource == null)
            {
                return CreateAuditFailure(request.Coverage, "型付きローカライズ情報の取得元がありません。");
            }

            try
            {
                var snapshot = typedSource.ReadSnapshot();
                return LocalizationKeyAuditAnalyzer.Analyze(request, snapshot, rawIdentities);
            }
            catch (LocalizationKeyAuditLimitException exception)
            {
                return CreateFailure(
                    request.Coverage,
                    new LocalizationKeyAuditIssue(
                        LocalizationKeyAuditIssueKind.LimitExceeded,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        Guid.Empty,
                        string.Empty,
                        string.Empty,
                        0,
                        exception.Message));
            }
            catch (Exception exception)
            {
                return CreateAuditFailure(
                    request.Coverage,
                    $"型として読み取るローカライズ監査に失敗しました: {exception.GetType().Name}");
            }
        }

        /// <summary>型としての読み取りまたは解析の例外を、部分データなしで隔離します。</summary>
        private static LocalizationKeyAuditResult CreateAuditFailure(
            LocalizationKeyAuditCoverage coverage,
            string message)
        {
            return CreateFailure(
                coverage,
                new LocalizationKeyAuditIssue(
                    LocalizationKeyAuditIssueKind.AuditFailed,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Guid.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    message));
        }

        /// <summary>監査停止問題1件だけを持つ未完了結果を作ります。</summary>
        private static LocalizationKeyAuditResult CreateFailure(
            LocalizationKeyAuditCoverage coverage,
            LocalizationKeyAuditIssue issue)
        {
            return new LocalizationKeyAuditResult(
                false,
                coverage,
                Array.Empty<string>(),
                Array.Empty<LocalizationKeyAuditCollectionSnapshot>(),
                new[] { issue },
                0);
        }
    }
}
