// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// raw preflight、typed snapshot、pure analyzer を順序保証付きで実行します。
    /// </summary>
    internal static class LocalizationKeyAuditService
    {
        /// <summary>default Assets coverage source で request を構築します。</summary>
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

        /// <summary>Assets-only scope を走査して default source で audit します。</summary>
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
                        $"監査入力を snapshot 化できません: {exception.GetType().Name}: {exception.Message}"));
            }

            if (!LocalizationKeyAuditAnalyzer.TryValidateRequest(provisionalRequest, out var requestFailure))
            {
                return CreateFailure(provisionalRequest.Coverage, requestFailure);
            }

            return Audit(CreateRequest(
                provisionalRequest.RequiredLocaleIdentifiers,
                provisionalRequest.Coverage.ScopeDescription,
                provisionalRequest.Coverage.DeclaredAssetPaths,
                new UnityLocalizationKeyAuditCoverageSource()));
        }

        /// <summary>coverage source を注入して request を構築します。</summary>
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
        /// Unity project の raw/typed source を使って manual advisory audit を実行します。
        /// </summary>
        internal static LocalizationKeyAuditResult Audit(LocalizationKeyAuditRequest request)
        {
            return Audit(
                request,
                new UnityLocalizationKeyAuditRawSource(),
                new UnityLocalizationKeyAuditTypedSource());
        }

        /// <summary>
        /// 注入した source を使い、raw failure 時は typed source を一度も呼ばず terminal result を返します。
        /// </summary>
        internal static LocalizationKeyAuditResult Audit(
            LocalizationKeyAuditRequest request,
            ILocalizationKeyAuditRawSource rawSource,
            ILocalizationKeyAuditTypedSource typedSource)
        {
            if (!LocalizationKeyAuditAnalyzer.TryValidateRequest(request, out var requestFailure))
            {
                return CreateFailure(request?.Coverage, requestFailure);
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
                return CreateAuditFailure(request.Coverage, "typed Localization source がありません。");
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
                    $"typed Localization 監査に失敗しました: {exception.GetType().Name}: {exception.Message}");
            }
        }

        /// <summary>typed または analyzer 例外を partial data なしで隔離します。</summary>
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

        /// <summary>terminal issue 1 件だけを持つ incomplete result を作ります。</summary>
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
