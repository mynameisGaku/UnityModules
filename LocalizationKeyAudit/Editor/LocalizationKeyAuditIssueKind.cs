// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 手動監査で区別して返す問題種別です。
    /// </summary>
    internal enum LocalizationKeyAuditIssueKind
    {
        /// <summary>raw file の安全な読み取り保証を確立できません。</summary>
        ReadOnlyGuaranteeUnavailable,

        /// <summary>監査設定が不正です。</summary>
        InvalidConfiguration,

        /// <summary>入力または生成 graph が上限を超えています。</summary>
        LimitExceeded,

        /// <summary>typed source の取得または監査処理に失敗しました。</summary>
        AuditFailed,

        /// <summary>required Locale が Localization Settings にありません。</summary>
        RequiredLocaleNotConfigured,

        /// <summary>required Locale の table が collection にありません。</summary>
        MissingLocaleTable,

        /// <summary>shared entry に対応する direct locale entry がありません。</summary>
        MissingDirectEntry,

        /// <summary>direct locale entry の値が null または空です。</summary>
        EmptyDirectValue,

        /// <summary>認識済み static reference の GUID と entry ID を一意に解決できません。</summary>
        DanglingStaticReference,

        /// <summary>宣言済み scope の完全な走査で static reference が見つかりません。</summary>
        NoStaticReferenceFoundWithinDeclaredScope,

        /// <summary>static reference coverage が未完了です。</summary>
        StaticReferenceCoverageIncomplete,

        /// <summary>同名の collection が複数あります。</summary>
        DuplicateCollectionName,

        /// <summary>同じ GUID の collection が複数あります。</summary>
        DuplicateCollectionGuid,

        /// <summary>同じ ID の shared entry が複数あります。</summary>
        DuplicateSharedEntryId,

        /// <summary>同じ key の shared entry が複数あります。</summary>
        DuplicateSharedEntryKey,

        /// <summary>同じ Locale の table が collection 内に複数あります。</summary>
        DuplicateLocaleTable,

        /// <summary>同じ ID の localized entry が table 内に複数あります。</summary>
        DuplicateLocalizedEntryId,

        /// <summary>shared entry に存在しない ID の localized entry があります。</summary>
        OrphanedLocalizedEntry,

        /// <summary>StringTableCollection に属さない typed StringTable があります。</summary>
        OrphanedLocaleTable,

        /// <summary>typed String/Asset Table owner に対応しない valid SharedTableData があります。</summary>
        OrphanedSharedTableData,

        /// <summary>Localization Settings に同じ Locale identifier が複数あります。</summary>
        DuplicateLocaleIdentifier
    }
}
