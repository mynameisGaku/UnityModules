# 振動の統一（Haptics）

## 30秒で分かる説明

振動APIはplatformごとに別物です。Androidは`Vibrator`で波形と振幅を制御でき、iOSはネイティブプラグイン無しではAudioToolboxのシステム振動（on/off）しか使えず、Desktopは非対応です。この差異を利用側で`#if`分岐して書くと、端末ごとの劣化度合いが見えなくなります。

Hapticsは「軽いインパクト」「成功通知」といった意図（`HapticsIntent`）とstep列のpattern（`HapticsPattern`）を1つのAPIにまとめ、driverが実際に持つ能力を`HapticsCapability` flagsで明示します。amplitude制御を持たないdriverにはserviceがamplitudeを1/0へ量子化したpatternを送るため、呼び出し側はplatform差を知らずに済み、capabilityを見て劣化度合いを選べます。

自動起動するsingletonはありません。寿命が明確な1つのownerが`HapticsService`を保持します。

## できること

- `TryPlay(intent)`で7種の意図を標準patternへ解決して再生する。
- `TryPlayPattern(pattern)`で最大64 stepのwaveform pattern（duration 1〜5000ms、amplitude 0〜1）を再生する。
- `Capability`で振動可否・amplitude制御・waveform対応をflagsとして確認し、UIや演出のfallbackを決める。
- amplitude制御なしのdriver向けに、serviceがamplitudeを1/0へ量子化した派生patternへ自動変換する。
- AndroidではAPI 26以降の`VibrationEffect.createWaveform`を使用し、不可なら`Vibrate(duration)`へ劣化する。
- `Dispose`後の呼出しは`ServiceDisposed`として報告し、誤用を静かに握りつぶさない。

## 使わない方がよい場合

Taptic EngineのCore Haptics波形、カスタムhaptic file再生、振動のqueue・scheduling・遅延再生が必要な場合には向きません。iOSでの表現はシステム振動1回のon/off近似です。

ゲームフィールに直結する微細な強度チューニング、デバイス별キャリブレーション、複数pad同時制御も対象外です。振動の実効強度やモータ応答性はOSと端末に依存し、本moduleは保証しません。

## 導入

Unity 6000.5.7f1以降を使用します。Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/Haptics#haptics-v1.0.0
```

利用側にasmdefがある場合は`Haptics.Runtime`を参照します。フォルダーを直接管理する場合だけ、`Haptics/`を`Assets/Modules/Haptics/`へ配置してください。

外部package依存はありません（`dependencies {}`）。このpackageに`Tools` menuはありません。

| platform | capability | 実装 |
| --- | --- | --- |
| Android | `Vibrate`＋`AmplitudeControl`／`PatternWaveform`（端末性能依存） | `android.os.Vibrator` JNI。API 26以降は`VibrationEffect.createWaveform`、不可なら`Vibrate(duration)` |
| iOS | `Vibrate`のみ | AudioToolbox frameworkの`AudioServicesPlaySystemSound(4095)` P/Invoke |
| Editor / Desktop | `None` | NoOp driver。常に何もしない |

## 3分で試す

1. Package Managerからsample `Haptics Basics`をimportします。
2. 空のSceneを作り、空のGameObjectに`HapticsBasicsController`を追加してPlayします。シーンファイルは同梱していません。
3. GUIにcapability表示が出ます。**Editorではcapability `None`表示となり振動しません。振動するのはAndroid/iOSの実機buildだけです。**
4. intent buttonを押すと`TryPlay`の結果（成功・失敗理由）が表示されます。実機では振動し、Editorでは`UnsupportedPlatform`が表示されます。
5. 自分のapplicationでは、起動ownerがserviceを1つ生成し、必要な場面で`TryPlay`または`TryPlayPattern`を呼びます。

```csharp
using Haptics;
using UnityEngine;

public sealed class GameFeedbackOwner : MonoBehaviour
{
    private HapticsService _haptics;

    private void Awake()
    {
        _haptics = new HapticsService();
    }

    public void OnCoinPicked()
    {
        if (!_haptics.TryPlay(HapticsIntent.SelectionTick, out var error))
        {
            // capability未対応やdispose済みなど。演出側は静かに諦めてよい。
            Debug.Log($"haptics skipped: {error}");
        }
    }

    private void OnDestroy()
    {
        _haptics.Dispose();
    }
}
```

## 実行するとどうなるか

- **Editor** — driverはNoOpでcapabilityは`None`です。すべての`TryPlay` / `TryPlayPattern`は`false`と`UnsupportedPlatform`を返し、Consoleに何も出しません。
- **Android実機** — 起動時に`hasVibrator()`と`hasAmplitudeControl()`を確認し、capabilityが決まります。intent buttonでpreset patternが振動します。amplitude制御なし端末では1/0量子化された波形になります。
- **iOS実機** — capabilityは`Vibrate`のみです。どのpatternでもシステム振動が1回鳴り、長いpatternは最初のstep durationで粗く近似されます（境界は下記）。
- sample controllerのGUIには現在のcapability、IsSupported、最後の呼出し結果が常時表示されます。

## よくある問題

- **Editorで振動しない** — 仕様です。EditorのdriverはNoOpです。実機buildで確認してください。
- **Androidで波形が単発の短い振動になる** — API 25以前、または`VibrationEffect`取得に失敗した端末では`Vibrate(duration)`へ劣化します。capabilityに`PatternWaveform`がない場合はその挙動が正です。
- **iOSでpatternの長さが無視される** — システム振動のon/off近似のためです。duration制御が必要ならAndroid向け演出に限定するか、capabilityで分岐してください。
- **`ServiceDisposed`が返る** — serviceをDisposeした後の呼出しです。ownerの寿命より長持ちさせず、再作成してください。
- **他moduleとの違い** — Player Optionsは音量等の設定保存・適用であり、入力や演出用の即時feedbackである振動は扱いません。両者を同じownerで併用しても干渉しません。

## 詳しい契約

### 公開API

- `HapticsService` — 明示ownerが1つ保持するservice。`Capability`、`IsSupported`、`TryPlay`、`TryPlayPattern`、`Dispose`を提供します。
- `HapticsIntent` — `SelectionTick`、`ImpactLight/Medium/Heavy`、`NotificationSuccess/Warning/Error`の7種。
- `HapticsCapability` — flags型。`None=0`、`Vibrate=1`、`AmplitudeControl=2`、`PatternWaveform=4`。helper（`CanVibrate()`、`SupportsPrecisePatterns()`など）を`HapticsCapabilitySupport`で提供します。
- `HapticsStep` — duration 1〜5000ms、amplitude 0〜1のreadonly value。不正値はconstructorで例外。
- `HapticsPattern` — step列の不変ラッパー（最大64 step、防御コピー）。`Presets.Get(intent)`でintent別標準patternを取得できます。値は決定論的に定義されています。
- `IHapticsDriver` — `Capability`と`bool TryVibrate(HapticsPattern)`だけの境界。遅延やキューは扱いません。
- `HapticsDrivers.ResolveDefault()` — platform判定によりdriverを1つ返すstatic factory。Editorでは必ずNoOpです。
- `HapticsError` — `None`、`UnsupportedPlatform`、`DriverMissing`、`NullPattern`、`EmptyPattern`、`PatternTooLong`、`InvalidDuration`、`InvalidAmplitude`、`ServiceDisposed`、`UnknownIntent`。

### 失敗条件

| 呼出し | 条件 | 結果 |
| --- | --- | --- |
| `TryPlay(intent)` | 定義外のenum値（cast対策） | `false` + `UnknownIntent` |
| `TryPlayPattern(null)` | null参照 | `false` + `NullPattern` |
| pattern検証 | step数0 / 65以上 / duration範囲外 / amplitude範囲外 | `EmptyPattern` / `PatternTooLong` / `InvalidDuration` / `InvalidAmplitude` |
| capabilityに`Vibrate`なし | NoOp、Desktop、未初期化JNI | `false` + `UnsupportedPlatform`（driverは呼ばない） |
| driverが要求を拒否 | JNI失敗、P/Invoke失敗など | `false` + `UnsupportedPlatform` |
| Dispose後 | 以降の`TryPlay` / `TryPlayPattern` | `false` + `ServiceDisposed` |

`DriverMissing`はservice構築時にdriver解決へ失敗した場合の予約値です。通常の構築経路では発生しません。patternの不正値は`HapticsStep` / `HapticsPattern`のconstructorが`ArgumentOutOfRangeException`（null配列は`ArgumentNullException`）として即時報告するため、serviceへ届う段階で残る検証エラーは防御目的です。

### amplitude量子化

`driver.Capability`に`AmplitudeControl`がない場合、serviceは各stepのamplitudeを`> 0 → 1`、`== 0 → 0`へ量子化した派生patternをdriverへ渡します。元のpatternオブジェクトは変更されません。この変換は仕様であり、capability `Vibrate`のみの環境（iOSなど）で中間強度のstepが「振動あり」として扱われます。

### iOSの近似境界

iOS driverは`AudioServicesPlaySystemSound(kSystemSoundID_Vibrate = 4095)`を1回呼ぶだけです。duration指定、amplitude、複数stepの並びは反映されません。pattern全体を最初のstepのdurationで粗く近似し、実際の振動時間はOS側の固定長です。精密なhaptic表現にはCore Hapticsを使うネイティブプラグインが別途必要です。

### ownership

`HapticsService`はsingleton、static event、自動GameObjectを作りません。application bootstrapなどの明示ownerが1 instanceを持ち、複数UIへ同じinstanceを渡してください。複数serviceからの同時振動要求の調停は行いません。

### 非対象

- Core Haptics、ネイティブプラグイン同梱、カスタムhaptic asset。
- queue、scheduling、遅延再生、重複要求のcoalescing。
- Desktop platform対応、WebGL、コンソール。
- singleton、自動初期化、`Tools` menu。

### テスト範囲

EditMode test（`Haptics.Editor.Tests`）がFakeHapticsDriverで次を検証します: intent→preset解決、capability別のquantize通過/素通し、pattern検証エラー各種、未知intent、null pattern、Dispose後の`ServiceDisposed`、Editorでの`ResolveDefault`がNoOpを返すこと。Android/iOS driverの`#if`内コードはEditModeでは実行されないため、stub側の存在でコンパイルを担保します。sampleは空Scene+controller構成のPlayMode testで、NoOp環境の全メソッド呼出しが例外を出さないことを確認します。

詳細なvalidationとresult contractは[Documentation](Documentation~/index.md)を参照してください。

本packageは[MIT License](LICENSE.md)です。外部依存は[Third-Party Notices.txt](Third-Party%20Notices.txt)を参照してください。
