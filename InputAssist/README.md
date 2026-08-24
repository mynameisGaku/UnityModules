# 入力補助（Input Assist）

## 30秒で分かる

生の入力値を「ゲームが扱いやすい値とgesture」へ変換する処理を1つの導入単位へまとめたmoduleです。

Unityでは、stickの中央ぶれ消し、感度curve、急変抑制、方向判定、長押し、連打、double tapを、projectごとに書き直しがちです。Input Assistはこれを設定と結果型として持ち、`Time`もInput Systemも読みません。値と経過時間（またはtick）を渡すだけなので、旧Input Manager、Input System、AI、Replay、単体テストのどこからでも同じ結果になります。

## できること

- stick入力の中央ぶれと端の飽和を消し、方向を保ったまま感度curveをかける。
- 入力の急変を、上げ方向・下げ方向で別々に抑える（rate limit、指数平滑、旋回角制限）。
- 入力を4方向・8方向、または符号付き整数stepへ量子化し、menuやgrid、Replayで扱える値にする。
- 押した瞬間・離した瞬間・長押し開始・連打・single/double/triple tapを1回の更新で受け取る。
- player入力とAI補助のような複数の2D入力を、重み付き平均で1つに合成する。
- 失敗時に例外や暗黙clampではなく明示errorを返し、直前の成功状態を保つ。

## 2つの契約（どちらを使うか）

このpackageには目的の違う2系統が同居します。型名も名前空間も別なので、片方だけを使っても、両方を混ぜても構いません。

| | Unity向けAPI | 割り当てなしAPI |
|---|---|---|
| 代表型 | `InputVectorFilter`、`InputButtonTracker` | `InputRadialDeadZone`、`AxisQuantizer`、`InputRepeatTracker`ほか |
| namespace | `InputAssist` | `InputDeadZones`、`InputResponse`、`InputSmoothing`、`InputFiltering`、`InputMixing`、`InputDirectionQuantization`、`InputQuantization`、`InputThresholding`、`InputPressing`、`InputRepeating`、`InputMultiTapping` |
| 数値型 | `float`、`Vector2` | `double`、`ulong` tick |
| 時間 | `deltaTime`（秒） | 呼び出し側が持つ単調増加tick |
| Inspector | `[Serializable]` + `[SerializeField]`で直接編集 | 非対応（codeで`TryCreate`） |
| UnityEngine依存 | あり | なし（値型中心、追加割り当てなし） |
| 単位 | 機能をひとまとめにした2つの処理器 | 1機能=1型。必要な物だけ組む |

- **MonoBehaviourへ設定を出し、`Time.deltaTime`で回す** ならUnity向けAPI。導入が最短です。
- **fixed tickのsimulation、Replay、bit単位で同じ結果が要る、GC割り当てを増やしたくない** なら割り当てなしAPI。`float`の丸めと`deltaTime`の揺れを契約から排除できます。

同じ「dead zone」「repeat」でも両系統は独立した実装で、互いを呼びません。片方の変更がもう片方の結果を変えることはありません。

## 使わない方がよい場合

- Input Actionやdeviceの読取・pairing・rebindをしたい。
- Action Mapごと入力を一時停止したい（**入力の一時停止（Input Gate）** が担当）。
- command sequence、chord、先行入力buffer、優先順位調停をしたい。
- 入力のnetwork同期、入力record、Player/AIの意思決定をしたい。

このmoduleは「生の入力値を、ゲーム側が扱いやすい値とgestureへ変換する」範囲に絞ります。

## 3分で試す

### 1. 導入する

Unity 6000.5.7f1以降で、Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputAssist#input-assist-v2.0.0
```

または`InputAssist` folderをprojectの`Assets/Modules/`へ配置します。Input System packageへの依存はありません。

### 2. Sampleを入れる

Package Managerの **Samples** から **Input Assist Basics** をImportし、`InputAssistBasics.unity`を開いてPlayします。個別機能を見たい場合は同じ一覧から **Input Radial Dead Zone Basics** や **Input Repeat Basics** などをImportします。

### 3. componentへ設定を埋め込む

`InputVectorFilter`と`InputButtonTracker`は`[Serializable]`です。`[SerializeField]`で持てば、そのままInspectorへ設定が出ます。時間は呼び出し側が渡します。通常操作では`Time.deltaTime`、pause中も動かすUIでは`Time.unscaledDeltaTime`、固定tickではtick間隔を渡します。

## 最小コード

```csharp
using InputAssist;
using UnityEngine;

public sealed class PlayerInputAdapter : MonoBehaviour
{
    [SerializeField] private InputVectorFilter _move = new InputVectorFilter();
    [SerializeField] private InputButtonTracker _action = new InputButtonTracker();

    public void UpdateInput(Vector2 rawMove, bool actionPressed, float deltaTime)
    {
        var move = _move.Process(rawMove, deltaTime);
        var action = _action.Process(actionPressed, deltaTime);

        if (!move.Succeeded || !action.Succeeded) return;
        transform.position += new Vector3(move.Value.x, 0f, move.Value.y) * deltaTime;

        if (action.Events.HasFlag(InputButtonEvent.TapCompleted))
            Debug.Log($"Tap count: {action.TapCount}");
    }
}
```

## 実行するとどうなるか

- **Inspector**: `PlayerInputAdapter`にdead zone、response mode、rise/fall speed、hold時間、repeat間隔、multi-tap gapが並びます。Play中に動かすと次のframeから反映されます。
- **Scene**: stickを中央付近で揺らしても`transform`は動かず、境界を越えた瞬間から滑らかに動き出します。急に倒しても`RiseSpeed`の分だけ立ち上がりが鈍ります。
- **Console**: 短押しを続けると`Tap count: 1`、`2`、`3`が出ます。設定が不正な場合は例外ではなく`move.Succeeded == false`となり、`move.Error`に理由が入ります。
- **Sample**: **Input Assist Basics**をPlayすると、実Buttonでstick補正、tap、hold、repeatを切り替えた結果を画面上で確認できます。960×600と640×360の両方で収まります。

## よくある問題

- **Inspectorに何も出ない**: fieldに`[SerializeField]`が無い、または`new`で初期化していない。`= new InputVectorFilter()`を付けます。
- **`Process`の結果が毎回失敗する**: 設定値が契約外です。`Error`（`InputAssistError`）を確認します。失敗時は前回の成功状態を保つため、値は動きません。
- **pause中もrepeatが進む**: 渡している`deltaTime`がpauseの影響を受けていません。`Time.unscaledDeltaTime`と`Time.deltaTime`を用途で使い分けます。
- **`InputDirectionMode`が曖昧だと言われる**: `InputAssist.InputDirectionMode`と`InputDirectionQuantization.InputDirectionMode`は別enumです。片方だけを`using`するか、完全修飾名で書きます。
- **旧moduleのasmdefが解決できない**: 旧assembly名（`InputRepeat.Runtime`など）を`references`に書いている場合、`InputAssist.Runtime`へ置き換えます。C#側の`using`は変更不要です。
- **Input Gateとの違い**: Input Gateは「入力を止める」、Input Assistは「入力値を整える」。両立します。

## 詳しい契約

### Unity向けAPI（namespace `InputAssist`）

| やりたいこと | 設定・結果 |
|---|---|
| stickの中央ぶれを消す | `InnerDeadZone` / `OuterDeadZone` |
| 小さい入力を弱くする | `ResponseMode` |
| 入力の急変を抑える | `RiseSpeed` / `FallSpeed` |
| menuやgrid用に方向を得る | `DirectionMode` / `Direction` |
| 押した瞬間・離した瞬間 | `Pressed` / `Released` |
| 長押し開始 | `HoldStarted` |
| 押しっぱなしの連続入力 | `Repeated` / `RepeatCount` |
| single・double・triple tap | `TapCompleted` / `TapCount` |

- `InputVectorFilter.Process(Vector2, float)` → `InputVectorFilterResult`（`Succeeded` / `Value` / `Direction` / `Error`）
- `InputButtonTracker.Process(bool, float)` → `InputButtonResult`（`Events` / `RepeatCount` / `TapCount` / `PressDuration` / `Error`）
- 設定は`TryConfigure(...)`で検証し、失敗時は`InputAssistError`を返して現在値を維持します。
- 状態は`Reset()` / `TryReset(Vector2, out InputAssistError)`で戻せます。Scene切替、Replay seek、testのarrangeで使います。
- repeatの大きな時間jumpは1回の更新につき32件へ制限します。残りは次回以降へ持ち越し、異常なframe停止で無制限loopしません。

### 割り当てなしAPI（double・tick）

| namespace | 主な型 | 役割 |
|---|---|---|
| `InputDeadZones` | `InputRadialDeadZone` | 内外radial dead zoneの再mapping |
| `InputResponse` | `InputVectorResponseCurve`、`InputVectorResponseMode` | 方向を保つmagnitude curve |
| `InputSmoothing` | `InputVectorSlewLimiter`、`InputVectorDirectionLimiter` | 1 stepあたりの最大変化量・最大旋回角 |
| `InputFiltering` | `InputVectorExponentialSmoother` | step単位の指数low-pass |
| `InputMixing` | `InputVectorWeightedMixer`、`InputVectorContribution` | 正規化重み付き平均 |
| `InputDirectionQuantization` | `InputDirectionQuantizer`、`InputDirectionMode` | 4方向・8方向への量子化 |
| `InputQuantization` | `AxisQuantizer` | 軸値→符号付き整数step |
| `InputThresholding` | `InputThresholdClassifier`、`InputThresholdEvent` | hysteresis付きpressed判定 |
| `InputPressing` | `InputPressClassifier`、`InputPressStatus` | tap / holdの分類 |
| `InputRepeating` | `InputRepeatTracker`、`InputRepeatStatus` | 初回delayとrepeat間隔、tick jumpのcatch-up |
| `InputMultiTapping` | `InputMultiTapClassifier`、`InputMultiTapStatus` | single〜N連tapの確定 |

生成は`TryCreate(...)`、評価は`Process` / `Quantize` / `TryPush`です。結果はimmutableな`readonly struct`で、`Succeeded`と専用error enumを持ちます。

```csharp
using InputDeadZones;
using InputRepeating;

if (!InputRadialDeadZone.TryCreate(0.15d, 1d, out var deadZone, out var deadZoneError)) return;
if (!InputRepeatTracker.TryCreate(30ul, 6ul, 0ul, out var repeat, out var repeatError)) return;

var stick = deadZone.Process(rawHorizontal, rawVertical);
if (stick.Succeeded && repeat.TryPush(tick, pressed, out var status, out _) && status.Triggered)
{
    MoveCursor(stick.Horizontal, stick.Vertical, (int)status.TriggerCount);
}
```

- Unity時刻、input device、乱数、global stateを読みません。同じ初期状態・同じ入力列から同じ結果になります。
- tickは呼び出し側が持つ単調増加の`ulong`です。tickが飛んでも、その間に到達していたrepeat数やtapをまとめて返します。
- 非有限値、範囲外、順序違反は明示errorで返し、暗黙clampしません。

### 非対象

Input Actionの購読、device pairing、rebind、Action Map停止、入力record、command comboは扱いません。呼び出し側または専用moduleへ残し、入力値の補正とgesture判定へ責務を限定します。

### テスト範囲

`Tests/EditMode`のEditMode testが両系統の境界値を確認します（dead zone境界、curve端点、rate limitの到達、旋回のtie、重み合計0、量子化のclamp、threshold hysteresis、tap/hold境界、repeatのcatch-up、multi-tapの確定条件）。各sampleの`Tests/PlayMode`はSceneのButton操作とresponsive geometryを確認します。

## 吸収した旧moduleと互換性

2.0.0で次の12packageをInput Assistへ吸収し、単独packageとしての配布を終了しました。

| 旧UPM識別子 | 旧displayName | 現在のnamespace |
|---|---|---|
| `com.studiogaku.input-radial-dead-zone` | Input Radial Dead Zone | `InputDeadZones` |
| `com.studiogaku.input-vector-response-curve` | Input Vector Response Curve | `InputResponse` |
| `com.studiogaku.input-vector-slew-limiter` | Input Vector Slew Limiter | `InputSmoothing` |
| `com.studiogaku.input-vector-exponential-smoother` | Input Vector Exponential Smoother | `InputFiltering` |
| `com.studiogaku.input-vector-direction-limiter` | Input Vector Direction Limiter | `InputSmoothing` |
| `com.studiogaku.input-vector-weighted-mixer` | Input Vector Weighted Mixer | `InputMixing` |
| `com.studiogaku.input-direction-quantizer` | Input Direction Quantizer | `InputDirectionQuantization` |
| `com.studiogaku.input-quantizer` | Input Quantizer | `InputQuantization` |
| `com.studiogaku.input-threshold-classifier` | Input Threshold Classifier | `InputThresholding` |
| `com.studiogaku.input-press-classifier` | Input Press Classifier | `InputPressing` |
| `com.studiogaku.input-repeat` | Input Repeat | `InputRepeating` |
| `com.studiogaku.input-multi-tap-classifier` | Input Multi Tap Classifier | `InputMultiTapping` |

- 上記の公開済みtagとUPM識別子は削除せず、既存利用者の互換入口として残します。今使っているprojectはそのまま動きます。
- C#のnamespace、型名、member、既定値、失敗契約は変更していません。**既存codeの`using`と呼び出しを書き換える必要はありません。**
- 変わるのはassemblyだけです。旧runtime assembly名（`InputRadialDeadZone.Runtime`ほか11個）を自作`asmdef`の`references`に書いている場合のみ、`InputAssist.Runtime`へ置き換えます。
- 新規projectでは、個別tagを探さず`com.studiogaku.input-assist` 2.0.0以降を1つ入れてください。
