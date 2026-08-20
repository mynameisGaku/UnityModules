# UnityModules

Unity 向けの再利用可能なモジュール置き場。各モジュールは独立したフォルダに入っており、
アセンブリ定義を持つので、必要なものだけをプロジェクトにコピーして使える。

対応: **Unity 6000.0 以降**

---

## モジュール一覧

| モジュール | 内容 | 依存 |
|---|---|---|
| [Containers](Containers/) | コンテナ / データ構造 66 種。GC フリーのコレクション、Inspector に出せるシリアライズ対応型、空間分割、Unity のライフサイクルに耐えるコンテナ。 | なし |
| [Inspector](Inspector/) | Inspector 拡張の属性 43 種。条件による表示・非表示、グループ化とタブ、入力値の検証、メソッドのボタン化。**Unity 6000.5 以降**。 | なし |
| [Drawing](Drawing/) | 実行中の線・矢印・箱・球・経路・文字をコード1行で描くデバッグ可視化。Development Build専用呼び出しと持続時間に対応。**Unity 6000.5 以降**。 | なし |
| [Save System](SaveSystem/) | 型付きJSON保存、複数スロット、破損検出、可能な環境での原子的置換、1世代バックアップ復旧。依存なし。**Unity 6000.5 以降**。 | なし |
| [Scene Flow](SceneFlow/) | 完全なSceneパスでSingle・Additive読込、有効Scene切替、Unloadを直列化し、開始前条件と完了後状態を結果で返す。**Unity 6000.5 以降**。 | なし |
| [Screen Transition](ScreenTransition/) | UI Toolkitの全画面オーバーレイでCover・Revealを非スケール時間に実行し、色・時間・補間方法・完了結果を明示する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Time Control](TimeControl/) | Scene所有のControllerが複数leaseの相対倍率を最小値で集約し、pause・slow motion・単独fast-forwardをTime.timeScaleへ安全に反映する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Diagnostics Context](DiagnosticsContext/) | 明示追加したcontext・breadcrumbと実行中のUnity Warning・Error・Assert・Exceptionを有界に保持し、手動操作時だけJSON reportへ書き出す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Build Guard](BuildGuard/) | Player build対象Sceneのactive・inactive階層とPrefab instanceを検査し、Missing MonoBehaviourを階層path・件数付きでbuild前に拒否するEditor専用module。**Unity 6000.5 以降**。 | なし |
| [Input Gate](InputGate/) | PlayerInputの実行中Action Mapを入れ子leaseで停止し、最後の解放時にActionごとの有効状態を復元する。**Unity 6000.5 / Input System 1.20.0 以降**。 | com.unity.inputsystem 1.20.0 / com.unity.modules.uielements 1.0.0 |
| [Audio Control](AudioControl/) | owner付きAudioSource poolで再生、voice上限、priority steal、handle停止、非スケールfadeを管理する。**Unity 6000.5 以降**。 | com.unity.modules.audio 1.0.0 / com.unity.modules.uielements 1.0.0 |
| [Startup Flow](StartupFlow/) | 明示した非同期stepをOrderとIdで決定論的に直列実行し、進捗・失敗位置・完了件数・協調cancelを結果として返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Simulation Clock](SimulationClock/) | 明示した整数経過時間を再現可能な固定step範囲・端数・補間率・drop量へ変換し、状態を保存・復元する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Deterministic Random](DeterministicRandom/) | version付き256-bit状態を保存・復元し、同じseed・状態・操作順から同じ64-bit列・範囲整数・浮動小数を再現する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [State Fingerprint](StateFingerprint/) | 明示した型付きfield列をversion固定canonical bytesへ変換し、Replay前後のstate一致をportable SHA-256 fingerprintで検証する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Replay Tape](ReplayTape/) | 非減少tick・command id・opaque payloadをversion固定canonical tapeへ記録し、完全検証後に同じ順序で読み戻す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Canonical Payload](CanonicalPayload/) | 明示したschema順のprimitive値をlittle-endian・IEEE 754・厳格UTF-8の有界canonical bytesへ変換し、同順序で読み戻す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Fixed Point](FixedPoint/) | signed Q16.16の小数値を整数raw値で保持し、0方向丸めと明示overflowを持つ四則演算をplatform間で再現する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Generational Handle](GenerationalHandle/) | 最小の空きslotを決定論的に割り当て、generationで解放済みの古いhandleを新しいentryから区別する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Quantizer](InputQuantizer/) | 有限1軸入力をdead zone付きの小さなsigned integer commandへ対称かつ決定論的に変換する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Stabilizer](InputStabilizer/) | 同じsigned integer commandが指定sample数だけ連続した時に確定し、一時的なnoise候補をsimulationへ流さない。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Command Buffer](InputCommandBuffer/) | 早押しcommandを明示simulation tickの短い有効期間だけ保持し、同じidの最古entryからFIFOで消費する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Sequence Matcher](InputSequenceMatcher/) | 正のcommand id列を明示simulation tickで照合し、順序一致・間隔timeout・先頭restartを決定論的に判定する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Repeat](InputRepeat/) | 明示simulation tickと押下状態から初回trigger・保持repeat・tick jump時のcatch-up・解放edgeを決定論的に算出する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Chord Matcher](InputChordMatcher/) | required commandの押下edgeが明示simulation tickの許容span内に揃ったかを決定論的に判定する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Press Classifier](InputPressClassifier/) | 明示simulation tickと押下状態から短押しtap・長押し開始・長押し完了を決定論的に分類する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Axis Conflict Resolver](InputAxisConflictResolver/) | negative・positiveの相反入力を明示simulation tickと4つのpolicyから決定論的に解決する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Input Multi Tap Classifier](InputMultiTapClassifier/) | 明示simulation tickのtap edgeをgap windowへ集約し、single・double・triple等の有界burstを決定論的に確定する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |

---

## 使い方

使いたいモジュールのフォルダを、プロジェクトの `Assets/` 以下にコピーする。
アセンブリ定義が同梱されているので、利用側の asmdef からそれを参照する。

```
Assets/
└── Modules/
    ├── Containers/
    │   ├── Runtime/     Containers.Runtime
    │   ├── Editor/      Containers.Editor
    │   └── Tests/       Containers.Tests
    ├── Inspector/
    │   ├── Runtime/     Inspector.Runtime   属性の定義だけ
    │   ├── Editor/      Inspector.Editor    解釈と描画
    │   └── Tests/       Inspector.Tests
    ├── Drawing/
    │   ├── Runtime/     Drawing.Runtime
    │   └── Tests/       Drawing.Tests
    ├── SaveSystem/
    │   ├── Runtime/     SaveSystem.Runtime
    │   ├── Tests/       SaveSystem.Tests
    │   └── Samples~/    SaveSystem.Samples
    ├── SceneFlow/
    │   ├── Runtime/     SceneFlow.Runtime
    │   ├── Editor/      SceneFlow.Editor
    │   ├── Tests/       SceneFlow.Tests / SceneFlow.Editor.Tests / SceneFlow.PlayMode.Tests
    │   └── Samples~/    SceneFlow.Samples
    ├── ScreenTransition/
    │   ├── Runtime/     ScreenTransition.Runtime
    │   ├── Tests/       ScreenTransition.Tests / ScreenTransition.PlayMode.Tests
    │   └── Samples~/    ScreenTransition.Samples / ScreenTransition.Samples.PlayMode.Tests
    ├── TimeControl/
    │   ├── Runtime/     TimeControl.Runtime
    │   ├── Tests/       TimeControl.Tests / TimeControl.PlayMode.Tests
    │   └── Samples~/    TimeControl.Samples / TimeControl.Samples.PlayMode.Tests
    ├── DiagnosticsContext/
    │   ├── Runtime/     DiagnosticsContext.Runtime
    │   ├── Tests/       DiagnosticsContext.Tests / DiagnosticsContext.PlayMode.Tests
    │   └── Samples~/    DiagnosticsContext.Samples / DiagnosticsContext.Samples.PlayMode.Tests
    ├── BuildGuard/
    │   ├── Editor/      BuildGuard.Editor
    │   ├── Tests/       BuildGuard.Tests
    │   └── Samples~/    Build Guard Basics
    ├── InputGate/
        ├── Runtime/     InputGate.Runtime
        ├── Tests/       InputGate.Tests / InputGate.PlayMode.Tests
        └── Samples~/    InputGate.Samples / InputGate.Samples.PlayMode.Tests
    ├── AudioControl/
        ├── Runtime/     AudioControl.Runtime
        ├── Tests/       AudioControl.Tests / AudioControl.PlayMode.Tests
        └── Samples~/    AudioControl.Samples / AudioControl.Samples.PlayMode.Tests
    ├── StartupFlow/
        ├── Runtime/     StartupFlow.Runtime
        ├── Tests/       StartupFlow.Tests / StartupFlow.PlayMode.Tests
        └── Samples~/    StartupFlow.Samples / StartupFlow.Samples.PlayMode.Tests
    ├── SimulationClock/
        ├── Runtime/     SimulationClock.Runtime
        ├── Tests/       SimulationClock.Tests
        └── Samples~/    SimulationClock.Samples / SimulationClock.Samples.PlayMode.Tests
    ├── DeterministicRandom/
        ├── Runtime/     DeterministicRandom.Runtime
        ├── Tests/       DeterministicRandom.Tests
        └── Samples~/    DeterministicRandom.Samples / DeterministicRandom.Samples.PlayMode.Tests
    ├── StateFingerprint/
        ├── Runtime/     StateFingerprint.Runtime
        ├── Tests/       StateFingerprint.Tests
        └── Samples~/    StateFingerprint.Samples / StateFingerprint.Samples.PlayMode.Tests
    ├── ReplayTape/
        ├── Runtime/     ReplayTape.Runtime
        ├── Tests/       ReplayTape.Tests
        └── Samples~/    ReplayTape.Samples / ReplayTape.Samples.PlayMode.Tests
    ├── CanonicalPayload/
        ├── Runtime/     CanonicalPayload.Runtime
        ├── Tests/       CanonicalPayload.Tests
        └── Samples~/    CanonicalPayload.Samples / CanonicalPayload.Samples.PlayMode.Tests
    ├── FixedPoint/
        ├── Runtime/     FixedPoint.Runtime
        ├── Tests/       FixedPoint.Tests
        └── Samples~/    FixedPoint.Samples / FixedPoint.Samples.PlayMode.Tests
    ├── GenerationalHandle/
        ├── Runtime/     GenerationalHandle.Runtime
        ├── Tests/       GenerationalHandle.Tests
        └── Samples~/    GenerationalHandle.Samples / GenerationalHandle.Samples.PlayMode.Tests
    ├── InputQuantizer/
        ├── Runtime/     InputQuantizer.Runtime
        ├── Tests/       InputQuantizer.Tests
        └── Samples~/    InputQuantization.Samples / InputQuantization.Samples.PlayMode.Tests
    ├── InputStabilizer/
        ├── Runtime/     InputStabilizer.Runtime
        ├── Tests/       InputStabilizer.Tests
        └── Samples~/    InputStabilization.Samples / InputStabilization.Samples.PlayMode.Tests
    ├── InputCommandBuffer/
        ├── Runtime/     InputCommandBuffer.Runtime
        ├── Tests/       InputCommandBuffer.Tests
        └── Samples~/    InputBuffering.Samples / InputBuffering.Samples.PlayMode.Tests
    ├── InputSequenceMatcher/
        ├── Runtime/     InputSequenceMatcher.Runtime
        ├── Tests/       InputSequenceMatcher.Tests
        └── Samples~/    InputSequencing.Samples / InputSequencing.Samples.PlayMode.Tests
    ├── InputRepeat/
        ├── Runtime/     InputRepeat.Runtime
        ├── Tests/       InputRepeat.Tests
        └── Samples~/    InputRepeating.Samples / InputRepeating.Samples.PlayMode.Tests
    ├── InputChordMatcher/
        ├── Runtime/     InputChordMatcher.Runtime
        ├── Tests/       InputChordMatcher.Tests
        └── Samples~/    InputChording.Samples / InputChording.Samples.PlayMode.Tests
    ├── InputPressClassifier/
        ├── Runtime/     InputPressClassifier.Runtime
        ├── Tests/       InputPressClassifier.Tests
        └── Samples~/    InputPressing.Samples / InputPressing.Samples.PlayMode.Tests
    ├── InputAxisConflictResolver/
        ├── Runtime/     InputAxisConflictResolver.Runtime
        ├── Tests/       InputAxisConflictResolver.Tests
        └── Samples~/    InputAxisConflict.Samples / InputAxisConflict.Samples.PlayMode.Tests
    └── InputMultiTapClassifier/
        ├── Runtime/     InputMultiTapClassifier.Runtime
        ├── Tests/       InputMultiTapClassifier.Tests
        └── Samples~/    InputMultiTapping.Samples / InputMultiTapping.Samples.PlayMode.Tests
```

UPM パッケージとして扱う場合は、モジュールのフォルダを `Packages/` 以下に置くか、
`manifest.json` からローカルパスで参照する。

---

## 各モジュールの約束

- **外部依存を持たない** — 追加パッケージを要求しない
- **`unsafe` を使わない** — asmdef の設定を変えずに導入できる
- **アセンブリ定義を持つ** — 使わないモジュールはビルドに入らない
- **`.meta` を含む** — GUID が変わらないので、参照が壊れない
