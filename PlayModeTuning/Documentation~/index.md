# プレイ中の調整を反映 詳細仕様

## 解決する問題

Play Mode で値を調整した結果を残したい場合、通常は値を控え、Play Mode 終了後に同じ値を入力し直します。対象が複数になると、取り違え、入力漏れ、Play Mode 中だけ存在する Object の誤選択が起きやすくなります。

Play Mode Tuning は「対象を先に固定する」「Play 中に手動で取り込む」「Play 終了後に差分を Preview する」「確認した同じ計画を 1 回だけ反映する」という 1 つの bounded workflow として扱います。

## 実画面で確認する

操作ガイドは実際の Editor Window に ①から⑤の対応箇所を加えます。

![Play Mode Tuning の操作ガイド](play-mode-tuning-guide.png)

Play Mode 中は ②の取り込みだけが有効になります。

![Play Mode 中の取り込み画面](play-mode-tuning-capture.png)

Play Mode 終了後、③の下に元の値と取り込んだ値を表示します。

![Play Mode 終了後の Preview 画面](play-mode-tuning-preview.png)

## 責務の境界

### このモジュールが所有するもの

- 事前に選ばれた Component/property の stable identity
- Edit Mode の元の値と、Play Mode 中に手動で取り込んだ値
- SessionState に保持する bounded state machine
- 決定論的な差分順と plan revision
- 同じ plan Object の単回消費
- 反映直前、反映後、復元後の検証
- 成功時の `EditorSceneManager.MarkSceneDirty`

### 所有しないもの

- ゲームの調整方法や適切な値の判断
- Runtime code と Player build
- Scene、Prefab、Asset の保存
- Play Mode の開始・終了
- 未選択項目の変更
- 他モジュールの設定

## state machine

`Idle -> Armed -> Capturable -> Captured -> ReadyToPreview -> Previewed -> Completed/Stale`

- `Armed`: ①で対象と Edit Mode baseline を固定済み
- `Capturable`: 対応する Play Mode へ入り、②を手動実行できる
- `Captured`: 選択値を取り込み済みで、Play Mode 終了待ち
- `ReadyToPreview`: Edit Mode へ戻り、③を実行できる
- `Previewed`: 同じ plan Object を 1 回だけ⑤へ渡せる
- `Completed`: 反映、失敗後の復元、差分なし、または破棄で終了
- `Stale`: identity、reload 条件、baseline、domain、session が一致せず終了

Lifecycle hook が自動で行うのは phase の移動だけです。値の取り込み、Preview、反映、保存は自動実行しません。

## identity と revision

Component identity は次を長さ付き UTF-8 token として SHA-256 へ入力します。

- `GlobalObjectId`
- Scene GUID と path
- MonoScript GUID
- assembly-qualified type

property identity には、さらに property path、`SerializedPropertyType`、数値型を含めます。Preview、revision、capture、apply、rollback は `GlobalObjectId` の ordinal 順、同じ対象では property path、property type、numeric type、component key の ordinal 順へ固定します。選択順や SHA-256 の hash 順には依存しません。Hierarchy path、名前、InstanceID を代替 identity として使いません。

Plan revision は session ID、nonce、全 property identity、baseline/captured の kind と payload、未選択 top-level fingerprint から同じ方法で作ります。

## 値の表現

`float` と `double` は生 bit を 8 桁または 16 桁の hex として保持します。Preview の十進表示は保存済み表示文字列を信用せず、exact payload から invariant round-trip 形式で再生成します。Vector、Color、Rect、Bounds、Quaternion も全 component を同じ形式で表示します。P0 では `C1234567` と `400921FB54442D18` の roundtrip を確認しています。

符号付き・符号なし整数は Unity の `SerializedPropertyNumericType` に応じて読み書きします。文字列は UTF-8 byte 数を先に確認し、payload は Base64 で保持します。

Unity 6000.5.7f1 では `SerializedPropertyType.String` の `isArray` が `true` でした。このため generic array rejection より先に String を判定します。通常の配列と List は拒否します。

Color、Vector、Rect、Bounds、Quaternion の浮動小数点要素もすべて有限であることを確認し、生 bit で比較します。

## reload 条件

Session は `SessionState` に JSON として保持します。

- 通常の Domain Reload: Play Mode 進入時に domain token が変わることを必須確認
- Disable Domain Reload: Play Mode 進入時に domain token が変わらないことを必須確認
- Play Mode 終了時の token 変化: 観測対象のみで、成功条件にしない
- Disable Scene Reload: GlobalObjectId と Edit baseline の復元契約を満たさないため拒否

P0 では通常設定 48/48、Disable Domain Reload 48/48 の確認が成功し、GlobalObjectId の Edit -> Play -> Edit 解決と SessionState 保持も成功しています。

SessionState を読み直すたびに schema、phase、件数、component/property identity、値形式、文字列上限、合計 payload を検査します。壊れた保存状態は Scene を変更せず `SessionDataInvalid` の `Stale` とし、新しい session を開始できる状態へ正規化します。

## Preview と単回消費

Preview 前に、Edit Mode の選択値と未選択 top-level fingerprint が Start 時点と一致することを確認します。表示する target 名は保存値ではなく、exact identity から解決した現在の GameObject から取得します。差分は property identity の ordinal 順です。

Apply は registry にある同じ Plan Object、session ID、nonce、revision だけを受け付けます。保存値、直接 identity field、dirty 対象 Scene path から revision を再計算し、Preview 時の revision と一致することも確認します。計画は engine mutation より前に消費します。複製した DTO、再使用、Domain Reload 後、session の食い違いは反映しません。

## 反映後確認と復元

反映は Component ごとに `SerializedObject` を作り、変化する選択項目だけを書き、`ApplyModifiedPropertiesWithoutUndo` を 1 回呼びます。

その後に次を再取得します。

- 全選択項目が captured payload と完全一致
- 全未選択 top-level property の `contentHash` fingerprint が反映直前と一致
- target identity と property type が一致

P0 では `OnValidate` が未選択項目を変更する副作用と、選択項目だけを戻した後に残る未選択 residual を確認しました。そのため、選択項目だけ合っていても成功にはしません。

反映または反映後確認に失敗した場合は、全選択項目を反映直前の値へ戻します。復元後も、選択値と未選択 fingerprint の両方を確認します。Apply と Rollback の結果は独立して返します。

成功時は対象 Scene ごとに `EditorSceneManager.MarkSceneDirty` を明示的に呼び、`scene.isDirty == true` を確認します。P0 では `ApplyModifiedPropertiesWithoutUndo` だけでは Scene dirty を安定して保証できませんでした。

## 上限

| 項目 | 上限 |
| --- | --- |
| Component | 32 |
| property | 256 |
| baseline + captured payload | 256 KiB |
| 1 文字列 | UTF-8 4096 byte |
| active session | 1 |
| plan registry | 64 |

## 主な Error

| Error | 条件 | 値の反映 |
| --- | --- | --- |
| `DisableSceneReloadUnsupported` | Disable Scene Reload が有効 | なし |
| `DomainReloadMismatch` | Play 進入時 token が設定と不一致 | なし |
| `UnsupportedTarget` | Asset、Prefab、未保存 Scene、非 MonoBehaviour | なし |
| `UnsupportedProperty` | nested、array、Object reference など | なし |
| `NonFiniteValue` | NaN または Infinity | なし |
| `TooManyComponents` | 33 Component 以上 | なし |
| `TooManyProperties` | 257 property 以上 | なし |
| `PayloadTooLarge` | 合計 256 KiB 超過 | なし |
| `SessionDataInvalid` | SessionState の形式、identity、値、上限が不正 | なし |
| `StaleSession` | baseline または未選択 fingerprint が変化 | なし |
| `StalePlan` | exact plan identity が不一致 | なし |
| `PlanAlreadyConsumed` | 同じ Plan Object を再使用 | なし |
| `VerificationFailed` | 反映後に selected/unselected が不一致 | 復元を試行 |
| `SceneDirtyFailed` | Scene を明示的に変更済みにできない | 復元を試行 |
| `RollbackFailed` | 復元後に selected/unselected が不一致 | 自動処理を終了 |

## 検証方針

- Unity API なしで state machine、planner、revision、single-use、payload 上限を試験する。
- fake gateway で stale、保存値・表示・target 名改変、部分失敗、選択・未選択副作用、復元 residual、Scene dirty 失敗を人工的に作る。
- String の `isArray` 特例、unsupported array/nested/object/curve/gradient、raw float/double bit を固定する。
- 通常設定では Play 進入時 token change、Disable Domain Reload では token unchanged を固定する。
- UI 見出しが ①、②、③、④、⑤の順で、Preview が Capture の下、Apply が最後であることを固定する。
