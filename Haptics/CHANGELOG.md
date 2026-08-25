# Changelog

## [1.0.0] - 2026-08-26

### Added

- 意図(`SelectionTick`、`Impact`3種、`Notification`3種)から標準patternへ解決する`TryPlay`と、任意waveform patternを再生する`TryPlayPattern`。
- 振動可否、amplitude制御、waveform pattern対応をflagsで表す`HapticsCapability`と結合判定helper。
- duration 1〜5000ms、amplitude 0〜1の`HapticsStep`と最大64 stepの不変`HapticsPattern`、intent別preset。
- Android Vibrator JNI driver(API26以降は`VibrationEffect.createWaveform`、不可なら`Vibrate(duration)`へ劣化)、iOS AudioToolbox P/Invoke driver、Editor/Desktop用NoOp driver。
- amplitude制御を持たないdriver向けにamplitudeを1/0へ量子化して送るservice側変換。
- 明示的なownerが1つ保持する`HapticsService`と、Dispose後の呼出しを`ServiceDisposed`で報告する契約。

### Boundaries

- ネイティブプラグインを同梱しない実装範囲です。iOSはAudioToolboxのシステム振動(on/off近似)のみで、Taptic EngineのCore Haptics波形、amplitude制御、pattern再現はできません。長いpatternも最初のstep durationで粗く近似します。
- queue、scheduling、遅延再生、重複要求の調停は非対応です。driverは振動要求を受け取り成否だけを返します。
- Desktop platformは非対応でcapability `None`です。Editorでは常にNoOp driverを使用します。
- Androidの実効振動強度、モータ応答性、API levelごとのOS差分の吸収保証はplatform実装を超えて保証しません。
- singleton、static event、自動GameObject、自動初期化は含みません。serviceの生成と寿命は利用側ownerが決めます。
