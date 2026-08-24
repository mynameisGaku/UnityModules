# Changelog

## [2.0.0] - 2026-08-24

### Changed

- 配布単位を統合。次の12個のpackageをInput Assistへ吸収し、単独packageとしての配布を終了。

  | 旧UPM識別子 | 旧displayName |
  |---|---|
  | `com.studiogaku.input-radial-dead-zone` | Input Radial Dead Zone |
  | `com.studiogaku.input-vector-response-curve` | Input Vector Response Curve |
  | `com.studiogaku.input-vector-slew-limiter` | Input Vector Slew Limiter |
  | `com.studiogaku.input-vector-exponential-smoother` | Input Vector Exponential Smoother |
  | `com.studiogaku.input-vector-direction-limiter` | Input Vector Direction Limiter |
  | `com.studiogaku.input-vector-weighted-mixer` | Input Vector Weighted Mixer |
  | `com.studiogaku.input-direction-quantizer` | Input Direction Quantizer |
  | `com.studiogaku.input-quantizer` | Input Quantizer |
  | `com.studiogaku.input-threshold-classifier` | Input Threshold Classifier |
  | `com.studiogaku.input-press-classifier` | Input Press Classifier |
  | `com.studiogaku.input-repeat` | Input Repeat |
  | `com.studiogaku.input-multi-tap-classifier` | Input Multi Tap Classifier |

- 吸収した型のsource互換性は維持。namespace、型名、member、既定値、失敗契約は一切変更していない。既存codeの修正は不要。
- 吸収した型のassemblyだけが`InputAssist.Runtime`へ変わる。旧assembly名（`InputRadialDeadZone.Runtime`ほか11個）を`asmdef`の`references`へ書いているprojectは、その1行を`InputAssist.Runtime`へ置き換える。
- 旧packageの公開済みtagとUPM識別子は削除せず、既存利用者の互換入口として残す。

### Added

- 割り当てなしのdouble・tick契約をInput Assistへ同梱。`InputDeadZones`、`InputResponse`、`InputSmoothing`、`InputFiltering`、`InputMixing`、`InputDirectionQuantization`、`InputQuantization`、`InputThresholding`、`InputPressing`、`InputRepeating`、`InputMultiTapping`の11 namespaceを追加。
- 吸収した12moduleのsampleを`Samples~/RadialDeadZone`から`Samples~/MultiTapClassifier`として同梱。Package ManagerのSamplesから個別にImportできる。
- READMEへ、Unity向け`float`+`deltaTime` APIと割り当てなし`double`/tick契約の使い分けを追加。

### Notes

- `InputAssist.InputDirectionMode`と`InputDirectionQuantization.InputDirectionMode`は別namespaceの別enumとして共存する。片方だけをusingするか、完全修飾名で参照する。
- 上記の共存に伴い、`InputDirectionQuantization.InputDirectionMode`のfile名だけを`InputDirectionMode.cs`から`InputDirectionQuantizationMode.cs`へ変更。型名は変更していない。

## [1.0.0] - 2026-08-22

### Added

- 2D入力のradial dead zone、response curve、rise・fall rate limit、4-way・8-way方向判定を`InputVectorFilter`へ統合。
- press、release、hold、repeat、single・multi-tapを`InputButtonTracker`へ統合。
- 明示delta time、状態reset、失敗時の状態維持、無制限repeat catch-up防止を実装。
- 実Buttonとresponsive geometryを確認できるInput Assist Basics sampleを追加。
