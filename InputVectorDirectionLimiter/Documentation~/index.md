# Input Vector Direction Limiter

## Boundaryとデータフロー

- Input: unit circle内の有限target horizontal・vertical
- State: immutableな1 step最大radianと、公開される現在horizontal・vertical
- Output: 更新後成分、target magnitude、実適用・残りradian、prior方向・到達・数値補正・error

入力device、Unity frame、時刻、random、global stateは境界外です。同じ構成・初期state・target列は同じstate列になります。

## 方向更新

現在とtargetが非ゼロなら、正規化方向のdotから必要回転量を求め、号付きcrossから最短回転方向を決めます。180°でcrossがzeroの場合は反時計回りを固定tie-breakにします。

```text
applied = min(requiredTurn, MaximumTurnRadians)
nextDirection = rotate(currentDirection, signed applied)
next = nextDirection * targetMagnitude
```

target magnitudeは方向制限と分離して即時反映します。stateがzeroな場合はprior方向が無いため、targetを直接受理して`HadPriorDirection=false`を返します。

## 数値契約

magnitudeは大きい成分でscaleするhypot計算で求め、subnormal成分もzeroと混同しません。dotは`[-1,1]`へ補正して`acos`を呼び、回転後のmagnitudeが丸めでtargetを超えた場合は内側へ補正します。補正は`WasNumericallyClamped`で観測できます。

## 失敗時の不変条件

NaN・Infinity、成分範囲外、unit circle外のtargetはerrorとして返し、現在stateを変えません。`TryReset`も同じ検証を行います。

## 検証

EditModeで構成境界、unit circle、反時計・時計回り、180°tie、zero state・zero target、target magnitude反映、subnormal、不正入力非変更、reset再構築、equalityを確認します。SampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、`timeScale=0`でも同じ5操作を再現します。
