// SPDX-License-Identifier: MIT

namespace LocalizationKeyAudit.Editor
{
    /// <summary>
    /// 手動監査で区別して返す問題種別です。
    /// </summary>
    internal enum LocalizationKeyAuditIssueKind
    {
        /// <summary>未加工ファイルの安全な読み取り保証を確立できません。</summary>
        ReadOnlyGuaranteeUnavailable,

        /// <summary>監査設定が不正です。</summary>
        InvalidConfiguration,

        /// <summary>入力または生成した参照関係が上限を超えています。</summary>
        LimitExceeded,

        /// <summary>型として読み取る取得元、または監査処理に失敗しました。</summary>
        AuditFailed,

        /// <summary>必須ロケールがローカライズ設定にありません。</summary>
        RequiredLocaleNotConfigured,

        /// <summary>必須ロケールのテーブルがコレクションにありません。</summary>
        MissingLocaleTable,

        /// <summary>共有項目に対応する直接ロケール項目がありません。</summary>
        MissingDirectEntry,

        /// <summary>直接ロケール項目の値が未設定または空です。</summary>
        EmptyDirectValue,

        /// <summary>認識済み静的参照のGUIDと項目識別子を一意に解決できません。</summary>
        DanglingStaticReference,

        /// <summary>宣言済み走査範囲の完全な走査で静的参照が見つかりません。</summary>
        NoStaticReferenceFoundWithinDeclaredScope,

        /// <summary>静的参照網羅が未完了です。</summary>
        StaticReferenceCoverageIncomplete,

        /// <summary>同名のコレクションが複数あります。</summary>
        DuplicateCollectionName,

        /// <summary>同じGUIDのコレクションが複数あります。</summary>
        DuplicateCollectionGuid,

        /// <summary>同じIDの共有項目が複数あります。</summary>
        DuplicateSharedEntryId,

        /// <summary>同じキーの共有項目が複数あります。</summary>
        DuplicateSharedEntryKey,

        /// <summary>同じロケールのテーブルがコレクション内に複数あります。</summary>
        DuplicateLocaleTable,

        /// <summary>同じIDのローカライズ済み項目がテーブル内に複数あります。</summary>
        DuplicateLocalizedEntryId,

        /// <summary>共有項目に存在しないIDのローカライズ済み項目があります。</summary>
        OrphanedLocalizedEntry,

        /// <summary>文字列テーブルコレクションに属さない、型として読み取った文字列テーブルがあります。</summary>
        OrphanedLocaleTable,

        /// <summary>型として読み取った文字列テーブルまたはアセットテーブルの所有元に対応しない、有効な共有テーブルデータがあります。</summary>
        OrphanedSharedTableData,

        /// <summary>ローカライズ設定に同じロケール識別子が複数あります。</summary>
        DuplicateLocaleIdentifier
    }
}
