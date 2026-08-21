# Input Vector Slew Limiter 1.0.0

## 分解

- Input: 有限かつ各成分`[-1,1]`の2D target
- State: immutableな最大差と、明示的に観測・resetできる現在2成分
- Output: 更新後2成分、適用delta magnitude、target到達状態、明示error

## 規則

現在値からtargetへの差vectorを求めます。差のmagnitudeが`MaximumDeltaPerStep`以下ならtargetへexactに到達します。超える場合は差の方向を保ち、magnitudeが上限になる分だけ現在値を進めます。componentごとのclampではありません。

`TryReset`はsave/load、scene再初期化、test fixtureから状態を明示再構築する境界です。不正入力時は状態を変えません。

## 非目標

Unity frameやdeltaTimeの読取、Input System、dead zone、quantization、加速度・減速度別policy、curve、overshoot、予測、callback、thread同期、global service、I/Oは含めません。

## 検証

EditModeで構成・入力境界、inclusive上限、方向保持、反復収束、target反転、invalid時不変、reset、instance分離、result equalityを検証します。sampleとMono/IL2CPP Playerは960×600と640×360で実描画し、timeScale=0でも同じ5操作を再現します。
