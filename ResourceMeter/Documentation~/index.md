# Resource Meter

## Boundaryとデータフロー

- Input: 有限の非負amount、または検証済みreset値、消費policy
- State: immutableな有限capacityと、0以上capacity以下の現在値
- Output: 前後値、capacity、要求・実適用・未適用delta、全量適用・変更・空・満杯・境界遷移・error

Unity frame、時刻、random、global state、UIは境界外です。同じcapacity・初期値・操作列は同じstate列と結果列になります。

## 回復と消費

```text
restoreApplied = min(amount, capacity - current)
partialSpendApplied = min(amount, current)
requireFullApplied = amount <= current ? amount : 0
```

回復deltaは正、消費deltaは負です。`RequestedDelta = AppliedDelta + UnappliedDelta`が成立するため、処理後にresource stateと要求充足率を再構築できます。不足は入力errorではなくdomain上の正常結果であり、`Succeeded=true`と`WasFullyApplied=false`を同時に返します。

## 境界flag

`BecameEmpty`と`BecameFull`は、その操作によって境界へ初めて到達した場合だけtrueです。既に空のmeterへ0を消費した場合や、既に満杯のmeterへ回復を要求した場合は境界遷移ではありません。`IsEmpty`と`IsFull`は操作後stateを表します。

## 失敗時の不変条件

NaN・Infinity・負amount・不正policyは失敗結果を返し、現在stateを変えません。作成と`TryReset`はcapacity・現在値の範囲を先に検証し、失敗時に有効なmeterまたは部分的なstateを残しません。

## 検証

EditModeでcapacity・初期値境界、回復clamp、部分消費、全量必須不足、exact消費、zero、最大有限値、不正入力非変更、reset、再現可能なsequence、result equalityを確認します。SampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、`timeScale=0`でも同じ5操作を再現します。
