# Haptics Basics

空のSceneを作り、空のGameObjectに`HapticsBasicsController`を追加してPlayしてください（シーンファイルは同梱していません）。controllerがapplication ownerとして`HapticsService`を1つ生成します。

**振動するのは実機buildだけです。** EditorではdriverがNoOpになりcapability `None`表示となり、すべてのbuttonは`Skipped (UnsupportedPlatform)`を表示します。

画面構成:

- 上段 — 現在の`Capability`と`IsSupported`表示、platform差の説明。
- intent button列 — `SelectionTick`から`NotificationError`まで7種。押すと`TryPlay(intent)`が走り、preset patternへ解決されます。
- `Play Custom Pattern` — 3 stepの任意waveform patternを`TryPlayPattern`で再生します。
- 下段 — 最後の呼出し結果（成功、または`HapticsError`）。

sample終了時にはcontrollerがserviceを`Dispose`します。自分のapplicationでは起動ownerが同じようにserviceを1つ持ち、寿命の終わりでDisposeしてください。
