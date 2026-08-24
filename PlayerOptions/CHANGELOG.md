# Changelog

## [1.0.0] - 2026-08-25

### Added

- 音量、quality、resolution、window mode、refresh rate、target frame rateを保持する型付きPlayer Options snapshot。
- `Load`、in-memory更新、Unity applicationへの`Apply`、PlayerPrefsへの`Save`を分離したworkflow。
- schema versionを持つ保存形式と、未保存、破損、未来schemaを区別する読み込み結果。
- quality名とindexを保持し、現在のquality一覧に対して解決する契約。
- Applyが呼出しを開始したfield、rollback失敗field、結果不明fieldを区別する`PlayerOptionsField` masks。
- SetResolution throw後のdisplay結果不明を表す`ResolutionOutcomeUnknown`と、migration例外を表す`MigrationFailed`。
- 明示的なapplication ownerから操作するRuntime APIとBasics sample。

### Boundaries

- PlayerPrefsの永続化、容量、同期、耐障害性はplatformとUnity実装を超えて保証しません。
- resolution変更はrequestとして扱い、同じframeでの適用完了を保証しません。
- `ExclusiveFullScreen`だけをsupported resolution一覧へ制限し、window modeでは正の任意sizeをrequestとして扱います。
- quality nameはindex一致に加えて現在一覧内で一意である必要があります。
- vSyncは変更せず、有効なvSyncやplatform制約に対するtarget frame rateの実効値は保証しません。
- key binding、rebind、localization、cloud同期、暗号化、backupは含みません。
