# プレイ中の調整を反映（Play Mode Tuning）

## 30 秒で分かる説明

Play Mode 中に Inspector の値を調整しても、Play Mode を終了すると通常は元へ戻ります。このモジュールは、残したい Component の項目だけを先に選び、調整後に手動で取り込み、Play Mode 終了後に差分を確認してから Scene へ反映する Editor 専用ツールです。

操作は必ず ①対象、②Play 中の取り込み、③Play 終了後の Preview、④確認、⑤反映結果の順です。取り込みも反映も自動では行いません。

![Play Mode Tuning の操作順](Documentation~/play-mode-tuning-guide.png)

## 何が便利になるか

- Play Mode で試した数値をメモして Inspector へ入力し直す手間を減らせます。
- 残したい Component と項目を先に限定できます。
- Play Mode 終了後、元の値と取り込んだ値を項目ごとに比較できます。
- Preview と同じ計画だけを 1 回だけ反映します。
- Preview 後に対象や値が変わった場合は、反映前に停止します。
- 反映後に選択項目の完全一致と、未選択の最上位項目が変わっていないことを確認します。
- 途中失敗時は選択項目を反映前の値へ戻し、反映結果と復元結果を分けて表示します。

## 使い方

Unity メニューの `Tools > Play Mode Tuning > Open` を開き、上から順に操作します。

### ① `Targets`

各行で、保存済み Scene にある `MonoBehaviour` と、残したい最上位の serialized property path を 1 件指定します。たとえば `moveSpeed` や `cameraOffset` です。

`Start Session` を押した時点の値と対象 identity を記録します。まだ Play Mode には入りません。

### ② `Capture During Play`

通常どおり Play Mode に入り、ゲームを動かしながら Inspector の値を調整します。残したい状態になった時点で `Capture Selected Values` を押します。

このボタンを押さずに Play Mode を終了した場合、その session は古いものとして終了します。

![Play Mode 中の手動取り込み](Documentation~/play-mode-tuning-capture.png)

### ③ `Preview After Play`

Play Mode を終了してから `Preview Captured Differences` を押します。この段階では Scene の値を変更しません。

![Play Mode 終了後の Preview](Documentation~/play-mode-tuning-preview.png)

### ④ `Review and Confirm`

対象 GameObject、property path、元の値、取り込んだ値をすべて確認します。target 名は解決済みの実体から取得し、浮動小数点と複合値は exact payload から round-trip 形式で表示します。問題がなければ確認欄をオンにします。

### ⑤ `Apply Tuning / Result`

`Apply Tuning` を押します。Preview 後の対象 identity、現在値、未選択項目が変わっていない場合だけ、同じ計画を 1 回反映します。

成功時は対象 Scene を明示的に変更済みにします。保存は通常の Unity 操作で内容を確認してから行ってください。

## 対応する値

選択できるのは、`MonoBehaviour` の最上位にある次の serialized property です。

- `bool`
- 符号付き・符号なし整数、`char`
- 有限の `float`、`double`
- UTF-8 で 4096 byte 以下の `string`
- `enum`、`LayerMask`
- `Color`
- `Vector2`、`Vector3`、`Vector4`
- `Vector2Int`、`Vector3Int`
- `Rect`、`RectInt`
- `Bounds`、`BoundsInt`
- `Quaternion`

配列、List、入れ子の field、Object reference、AnimationCurve、Gradient、NaN、Infinity は対象外です。Unity では `string` も `isArray == true` と報告されるため、文字列だけは property type を先に判定して対応します。

## 上限と利用条件

- 1 session は最大 32 Component
- 選択項目は最大 256 件
- 元の値と取り込んだ値を合わせた payload は最大 256 KiB
- 対象は `Assets` 以下の保存済み Scene にある、Prefab ではない `MonoBehaviour`
- `MonoScript` も `Assets` 以下にあり、GUID を取得できること
- Scene Reload は有効であること
- 通常の Domain Reload と `Disable Domain Reload` の両方に対応
- `Disable Scene Reload` は非対応で、session 開始時または Play Mode 進入時に停止

## 自動では行わないこと

- Play Mode 進入時や終了時の自動取り込み
- Preview 後の自動反映
- Scene や Asset の自動保存
- 選択していない値の意図的な変更
- InstanceID や Hierarchy path を使った代替検索

identity には `GlobalObjectId`、Scene GUID/path、MonoScript GUID、assembly-qualified type、property path/type を使います。詳しい失敗条件と検証範囲は [詳細仕様](Documentation~/index.md) を参照してください。

Preview と反映の項目順は `GlobalObjectId` の ordinal 順、同じ対象内では property path の ordinal 順です。選択した順番や identity hash の並びには依存しません。

## スクリプトから使う場合

公開入口は `PlayModeTuning.Editor.PlayModeTuningService` です。

- `Start(IReadOnlyList<PlayModeTuningPropertySelection>)`
- `GetCurrentSession()`
- `CaptureDuringPlay(Guid sessionId)`
- `PreviewAfterPlay(Guid sessionId)`
- `Apply(PlayModeTuningPlan plan)`
- `Discard(Guid sessionId)`

公開 DTO は immutable で、collection は defensive copy です。Plan は生成した同じ Object、nonce、revision がそろった場合に 1 回だけ使用できます。
