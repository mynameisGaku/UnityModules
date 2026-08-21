# Input Vector Exponential Smoother 1.0.0

## 分解

- Input: `[-1,1]`内の有限double target horizontal・vertical
- State: immutable factorと、明示的に公開する現在horizontal・vertical
- Output: 更新後成分、実適用差分magnitude、target残差magnitude、exact到達状態または明示error

入力device、Unity frame、時刻、random、global stateは境界外です。同じfactor・初期値・target列は同じ状態列になります。

## 定率更新

1回の`Process`は各成分へ次式を適用します。

```text
delta = target - current
next = current + delta * SmoothingFactor
```

factor `0.5`、初期値`(0,0)`、target`(1,0)`なら現在値は`0.5`、`0.75`、`0.875`と進みます。初期値zeroでは対角targetの方向も保ちます。factor `1`はtargetを直接代入してexact到達を保証します。

## 丸めと観測

結果の`AppliedDeltaMagnitude`は数式上のdeltaではなく、double丸め後の新旧状態差です。`RemainingDeltaMagnitude`は更新後状態からtargetまでの距離です。極小差で更新値が現在値と同じに丸められた場合も自動snapせず、適用差0・正の残差・`ReachedTarget=false`として呼出側へ示します。

`TryReset`は有効な明示状態だけを受け入れます。失敗時は現在状態を変えないため、保存済み状態からの再構築可否をerrorで判断できます。

## 非目標

Input System読取、dead zone、magnitude curve、一定量slew、方向量子化、加速度、spring、実時間変換、自動snap、callback、global service、I/O、Replay再生は含めません。

## 検証

EditModeでfactor境界、golden反復列、対角方向、factor 1、同値target、subnormal残差、不正入力非変更、reset再構築、equalityを検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0でも同じ5操作を再現します。
