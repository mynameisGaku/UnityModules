# Haptics 1.0.0

Hapticsは、iOS、Android、Desktopで異なる振動APIの差異を意図ベースの1つのAPIへまとめ、driver capabilityをflagsとして明示します。導入と最短のSample手順はpackage直下の[README](../README.md)を参照してください。

## 必要環境

- Unity 6000.5.7f1以降。
- Runtime参照: `Haptics.Runtime`。
- 外部package依存なし（`dependencies {}`）。
- 実機振動にはAndroid端末、またはAudioToolbox frameworkを利用できるiOS実機buildが必要です。EditorとDesktopはcapability `None`のNoOp driverで動作します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/Haptics#haptics-v1.0.0
```

Editor menuはありません。Package Managerから`Haptics Basics`をimportし、空Sceneの空GameObjectへ`HapticsBasicsController`を追加してPlayしてください。GUIにcapability表示とintent button列が出ます。振動するのは実機buildだけで、Editorではcapability `None`表示になります。

## ownership

`HapticsService`はapplication全体の振動feedbackを1 instanceで所有するための部品です。static singleton、static event、service locator、自動GameObjectは提供しません。application bootstrapやroot lifetime scopeがserviceを生成し、必要なUIへ同じinstanceを渡してください。

複数instanceからの同時振動要求の順序調停、queue化、schedulingは行いません。driverは1回の振動要求を受け取り成否だけを返します。serviceの`Dispose`後はすべての再生呼出しが`ServiceDisposed`で失敗し、ownerは再生成を行います。

## 型model

| 型 | member | 意味 |
| --- | --- | --- |
| `HapticsIntent` | 7種の定数 | 意図ベースの再生要求。preset patternへ解決される |
| `HapticsCapability` | flags | `None=0`、`Vibrate=1`、`AmplitudeControl=2`、`PatternWaveform=4` |
| `HapticsStep` | `DurationMilliseconds` | 1〜5000ms |
|  | `Amplitude` | 0〜1。範囲外・NaN・無限大はconstructorで例外 |
| `HapticsPattern` | `Steps` | 最大64 stepの不変リスト（防御コピー） |
|  | `TotalDurationMilliseconds` | 全step durationの合計 |
|  | `Presets.Get(intent)` | intent別の決定論的な標準pattern |

全value型はreadonlyです。`HapticsStep`は`Equals` / `GetHashCode` / 比較演算子を実装します。`HapticsPattern`はconstructor時点で検証し、不正値は`ArgumentOutOfRangeException`（null配列は`ArgumentNullException`）で即時に失敗します。保持するstep配列は防御コピーされ、外部から変更できません。

### capability helper

`HapticsCapabilitySupport`は次の拡張methodを提供します。

| method | trueになる条件 |
| --- | --- |
| `CanVibrate()` | `Vibrate`を持つ |
| `CanControlAmplitude()` | `AmplitudeControl`を持つ |
| `CanPlayWaveformPatterns()` | `PatternWaveform`を持つ |
| `SupportsPrecisePatterns()` | `AmplitudeControl`と`PatternWaveform`の両方を持つ |

## serviceの作成

既定ではplatform判定によるdriver解決を使います。

```csharp
var haptics = new HapticsService();
```

明示的にdriverを指定する場合はconstructorへ渡します。testやproject固有driverは`IHapticsDriver`を実装します。

```csharp
IHapticsDriver driver = new FakeHapticsDriver();
var haptics = new HapticsService(driver);
```

nullを渡すか引数を省略すると`HapticsDrivers.ResolveDefault()`が使われます。Editorでは必ずNoOp driver、Androidでは`UnityAndroidHapticsDriver`、iOSでは`UnityIOSHapticsDriver`、その他platformではNoOp driverです。

## 操作

### `TryPlay(HapticsIntent intent, out HapticsError error)`

intentを`HapticsPattern.Presets`の標準patternへ解決し、driverへ渡します。定義外のenum値はcast対策として`UnknownIntent`で失敗します。成功時のみtrue。

### `TryPlayPattern(HapticsPattern pattern, out HapticsError error)`

patternを検証し、capabilityに応じた変換をしてdriverへ渡します。`AmplitudeControl`を持たないdriver向けにはamplitudeを1/0へ量子化した派生patternへ変換します（元patternは変更しません）。`Vibrate` capabilityがない場合はdriverを呼ばず`UnsupportedPlatform`で失敗します。

### `Capability` / `IsSupported`

driverが報告する現在capabilityと、`Capability != None`の簡易判定です。`Dispose`後も読めますが、再生系の呼出しは`ServiceDisposed`で失敗します。

### `Dispose()`

serviceを停止し、driverが`IDisposable`なら後片付け（JNI参照解放など）を行います。以降の`TryPlay` / `TryPlayPattern`は`false` + `ServiceDisposed`です。

## 失敗reason一覧

| `HapticsError` | 発生条件 |
| --- | --- |
| `None` | 成功 |
| `UnsupportedPlatform` | capabilityに`Vibrate`がない、またはdriverが要求を拒否した |
| `DriverMissing` | driver解決へ失敗（予約値。通常経路では発生しない） |
| `NullPattern` | `TryPlayPattern`へnull参照を渡した |
| `EmptyPattern` | step数が0 |
| `PatternTooLong` | step数が`MaxStepCount`(64)を超えた |
| `InvalidDuration` | step durationが1〜5000msの範囲外 |
| `InvalidAmplitude` | amplitudeが0〜1の範囲外、NaN、無限大 |
| `ServiceDisposed` | Dispose後の呼出し |
| `UnknownIntent` | 定義されていないenum値 |

## platform driverの挙動

### Android (`UnityAndroidHapticsDriver`)

起動時に`hasVibrator()`と`hasAmplitudeControl()`を確認し、API levelが26以上ならcapabilityへ`PatternWaveform`を加えます。状態照会のJNI呼び出しだけが失敗した場合はcapabilityを楽観的に`Vibrate`付きとし、実際の再生時の二段fallbackで吸収します。再生時は`VibrationEffect.createWaveform(amplitudes, durations, -1)`を試し、取得できない場合は`Vibrator.Vibrate(totalDuration)`へ劣化します。初期化に失敗した場合も例外は握りつぶさずConsoleへ記録し、capability `None`として安全に動作します。JNI参照は`Dispose`で解放します。

### iOS (`UnityIOSHapticsDriver`)

AudioToolbox frameworkの`AudioServicesPlaySystemSound(kSystemSoundID_Vibrate = 4095)`をP/Invokeで1回呼びます。capabilityは`Vibrate`のみです。patternのduration並びは反映されず、長いpatternも最初のstep durationで粗く近似されます（実際の振動長はOS固定）。ネイティブプラグインを同梱しない実装範囲であり、Core Haptics波形は対象外です。

### NoOp (`UnityNoOpHapticsDriver`)

capability `None`、`TryVibrate`常時false。Editorと未対応platformで使用します。

## 非対象

- Core Haptics、カスタムhaptic file、ネイティブプラグイン同梱による高精細制御。
- queue、scheduling、遅延再生、重複要求のcoalescing。
- Desktop、WebGL、コンソールでの実振動。
- singleton、自動初期化、static event。

## テスト範囲

EditMode testはFakeHapticsDriverにより、intent→preset解決、quantize変換、pattern検証エラー各種、未知intent、null pattern、Dispose契約、Editorの`ResolveDefault`がNoOpであることを検証します。Android/iOS driverの`#if`内コードはEditModeで実行されないため、stub側の存在によりコンパイル可能性だけを担保します。sampleのPlayMode testはNoOp環境でcontroller操作が例外を出さないことと、capability表示文字列がnullでないことを確認します。
