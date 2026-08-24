# 再現可能シミュレーション（Deterministic Simulation）

対応: **Unity 6000.5.7f1 以降**

## 30秒で分かる説明

「さっきと同じ操作をしたのに結果が違う」を無くすためのpackageです。

Unityでゲームロジックを書くと、現在時刻・`Time.deltaTime`・`UnityEngine.Random`・`float`演算・`FixedUpdate`の呼ばれ方が、
そのフレームの重さや端末によって変わります。その結果、不具合の再現、リプレイ再生、テストでの検証、
セーブデータの照合が「だいたい合っている」までしか詰められません。

このpackageは、時間・乱数・数値・記録・照合を**利用側が明示した整数値だけで動く形**に置き換えます。
Unityの現在時刻もSceneもGameObjectも読みません。同じ設定・同じ初期状態・同じ入力列なら、
何度実行しても、どの端末でも、同じ結果と同じ最終状態になります。

7つのRuntimeが1つのpackageに入っています。リプレイを1本成立させるには
「固定step時計・乱数状態・記録テープ・状態照合」が同時に要るため、まとめて導入する単位にしています。

## できること

- 経過時間を「今回実行すべき固定step範囲」と「描画用の補間端数」へ変換し、hitch時のcatch-upも上限付きで扱えます。
- 乱数の内部状態をそのまま保存・復元でき、途中の1手からでも同じ乱数列を再開できます。
- 実行したcommandをtick付きの有界テープへ記録し、そのままバイト列として保存・再生できます。
- 実行後の状態を32byteの照合値へ畳み込み、「録画時と再生時で状態が一致したか」を1行で比較できます。
- `float`の誤差を避けたい計算を、桁あふれを検査するQ16.16固定小数点で行えます。
- 値の並びを正規化されたバイト列へ書き出し、同じ内容なら必ず同じバイト列になる形で保存できます。
- 生存確認が要るオブジェクトへ、解放済みなら弾ける世代番号付きの識別子を配れます。

## 使わない方がよい場合

- **値を保持するコンテナが欲しい場合。** `GenerationHandlePool` は識別子を配るだけで、値は持ちません。
  slotに値を格納したいなら `Containers` の `SlotMap<T>` が適します。
  `GenerationHandlePool` は、保管場所を自分で持ちたい・handleだけ外部へ渡したい場合の選択肢です。
- ゲームロジック、物理、AI、描画そのものを実行したい場合。ここにあるのは入力・状態・出力の変換だけです。
- network同期、rollback、遅延補償の実装が欲しい場合。再現性の土台は提供しますが、同期処理は含みません。
- セーブファイルの読み書き、暗号化、圧縮が欲しい場合。バイト列は返しますが、file I/Oは行いません。
- 汎用の暗号ハッシュや署名が欲しい場合。`StateFingerprint` は状態比較用で、改ざん防止用ではありません。
- 単に速い乱数が欲しい場合。保存・復元が不要なら `UnityEngine.Random` の方が手軽です。

## 3分で試す

1. Package Manager を開き、**Add package from git URL** に次を指定します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/DeterministicSimulation#deterministic-simulation-v1.0.0
   ```

2. 追加後、Package Manager の **Samples** から試したいものを **Import** します。
   最初は `Simulation Clock Basics` と `Replay Tape Basics` の2つが分かりやすいです。
3. `Assets/Samples/Deterministic Simulation/1.0.0/...` に入ったSceneを開き、Play します。
4. 自分のコードから使う場合は、対象の asmdef の **Assembly Definition References** に
   `DeterministicSimulation.Runtime` を追加します（Runtimeは `autoReferenced: true` のため、
   asmdefを使っていないScriptからはそのまま参照できます）。
5. Project Settings の変更、初期化用の MonoBehaviour、singleton の配置はいずれも不要です。

## 最小コード

固定step時計で刻み、その各stepで乱数を引き、実行内容をテープへ記録し、最後に状態を1つの照合値へ畳み込みます。
この4つが揃うと「録画したものを、あとから同一性を確認しながら再生できる」状態になります。

```csharp
using System;
using CanonicalPayload;
using DeterministicRandom;
using ReplayTape;
using SimulationClock;
using StateFingerprint;

// 1) 20ms固定step。1回の入力で進めるのは最大4stepまで（hitch時の処理集中を防ぐ）。
var settings = new FixedStepClockSettings(
    TimeSpan.TicksPerMillisecond * 20L,
    maximumStepsPerAdvance: 4);

if (!FixedStepClock.TryCreate(settings, out var clock, out var clockError))
{
    throw new InvalidOperationException(clockError.ToString());
}

// 2) seedから作る乱数列。State を保存すれば途中から再開できる。
var random = DeterministicRandomStream.Create(20260824UL);

using var tape = new ReplayTapeBuilder();

// 3) 時計へ渡すのは float ではなく、明示した整数の経過時間。
foreach (var elapsedMilliseconds in new[] { 16L, 33L, 500L })
{
    var advance = clock.AdvanceTicks(TimeSpan.TicksPerMillisecond * elapsedMilliseconds);
    if (!advance.IsSuccess)
    {
        throw new InvalidOperationException(advance.Error.ToString());
    }

    for (var offset = 0; offset < advance.StepCount; offset++)
    {
        var stepIndex = advance.FirstStepIndex + offset;

        if (!random.TryNextInt32(0, 6, out var roll, out var randomError))
        {
            throw new InvalidOperationException(randomError.ToString());
        }

        // 4) このstepで起きたことを正規化バイト列にする。
        using var payload = new CanonicalPayloadWriter();
        if (!payload.TryWriteInt32(roll, out var writeError))
        {
            throw new InvalidOperationException(writeError.ToString());
        }

        if (!payload.TryBuild(out var payloadValue, out var payloadError))
        {
            throw new InvalidOperationException(payloadError.ToString());
        }

        // 5) tick付きでテープへ記録する（tickは非減少であること）。
        if (!tape.TryAppend((ulong)stepIndex, 1u, payloadValue.ToByteArray(), out var tapeError))
        {
            throw new InvalidOperationException(tapeError.ToString());
        }
    }

    // 描画側へは補間率だけ渡す。
    RenderInterpolated(advance.InterpolationAlpha);
}

if (!tape.TryBuild(out var recorded, out var tapeBuildError))
{
    throw new InvalidOperationException(tapeBuildError.ToString());
}

// 6) 時計・乱数・テープの最終状態を1つの照合値へ畳み込む。
using var fingerprint = new StateFingerprintBuilder();
fingerprint.WriteInt64(1u, clock.State.CompletedStepCount);
fingerprint.WriteInt64(2u, clock.State.RemainderTicks);
fingerprint.WriteInt64(3u, clock.State.TotalDroppedTicks);
fingerprint.WriteUInt64(4u, random.State.Word0);
fingerprint.WriteBytes(5u, recorded.ToByteArray());

if (!fingerprint.TryBuild(out var verification, out var fingerprintError))
{
    throw new InvalidOperationException(fingerprintError.ToString());
}

Console.WriteLine(verification.ToString());
```

再生側は `recorded.ToByteArray()` を保存しておき、`ReplayTapeValue.TryParse` で読み直して
同じ `settings` と同じ seed から再実行します。最後に同じ手順で作った照合値が一致すれば、再現できています。

## 実行するとどうなるか

上の最小コードでは次のようになります。

- 16ms の入力では step は0件です（端数として蓄積されます）。
- 33ms の入力で合計49msとなり、step 0 と step 1 の2件が返ります。端数は9msです。
- 500ms の入力では本来25step分ですが、`MaximumStepsPerAdvance: 4` により step 2〜5 の4件だけを実行し、
  残りは `DroppedStepCount` と `DroppedTicks` に明示されます（勝手に飛ばされたりはしません）。
- テープには6件のエントリが入り、`recorded.ByteCount` はそのバイト長になります。
- Console には64桁の16進文字列が1行出ます。これが状態の照合値です。
  設定・seed・入力列を変えなければ、何度実行しても、どの端末でも同じ文字列になります。

サンプルSceneでは、Play すると UI Toolkit のButtonが並び、押すたびに入力・結果・状態がその場に表示されます。
`Simulation Clock Basics` では 16ms / 33ms / 500ms hitch のdrop件数、同一入力列の再生一致、Resetによる復元が確認できます。
Consoleにエラーは出ません。ProjectにAssetが生成されることもありません（保存は利用側の責務です）。

## よくある問題

- **`FixedStepClock` が見つからない / 型が解決できない。**
  自分の asmdef の References に `DeterministicSimulation.Runtime` を追加してください。
  名前空間は統合前と同じ `SimulationClock`、`DeterministicRandom` などのままです（`DeterministicSimulation` ではありません）。
- **`CS0433: 型が複数のアセンブリに存在します` が出る。**
  統合前の旧package（`com.studiogaku.simulation-clock` など）が同時に入っています。
  名前空間と型名が同一のため併用できません。Package Manager から旧packageを削除してください。
- **同じ入力なのに結果が一致しない。**
  時計へ `Time.deltaTime` を毎回 `float` のまま渡していないか確認してください。
  記録・再生の入力は、実際に `AdvanceTicks` へ渡した整数値の列です。float から整数への変換は時計の外で1回だけ行い、その整数を記録します。
- **catch-up 上限に達したときに時間が消える。**
  仕様です。超過分は `DroppedStepCount` / `DroppedTicks` に出るので、必要なら利用側で補正してください。
  上限を上げるだけだと、重いフレームで処理集中が連鎖します。
- **`ReplayTapeError` で追記が失敗する。**
  tick が前回より小さい、`commandId` が0、テープの上限byte数・件数を超えた、のいずれかです。
- **`GenerationHandlePool` に値を入れたい。**
  この型は識別子だけを配ります。値も持たせたい場合は `Containers` の `SlotMap<T>` を検討してください。
- **Unity のversionが古い。** 6000.5.7f1 以降が対象です。

## 詳しい契約

### 公開API

| 名前空間 | 主な型 | 役割 |
|---|---|---|
| `SimulationClock` | `FixedStepClock` / `FixedStepClockSettings` / `FixedStepClockState` / `FixedStepAdvanceResult` / `FixedStepClockError` | 整数経過時間を連続step範囲・端数・drop量へ変換する。 |
| `DeterministicRandom` | `DeterministicRandomStream` / `DeterministicRandomState` / `DeterministicRandomError` | xoshiro256\*\* の乱数列と、保存可能な内部状態。 |
| `FixedPoint` | `Fixed32` / `Fixed32Result` / `Fixed32Error` | 検査付き Q16.16 の四則演算と丸め。 |
| `CanonicalPayload` | `CanonicalPayloadWriter` / `CanonicalPayloadReader` / `CanonicalPayloadValue` / `CanonicalPayloadError` | 呼び出し側が決めた順序で書き読みする正規化バイト列。 |
| `ReplayTape` | `ReplayTapeBuilder` / `ReplayTapeReader` / `ReplayTapeValue` / `ReplayTapeEntry` / `ReplayTapeError` | tick・commandId・payload の有界テープ。 |
| `StateFingerprint` | `StateFingerprintBuilder` / `StateFingerprintValue` / `StateFingerprintError` | 明示した順序付きフィールドから作る版付きSHA-256。 |
| `GenerationalHandles` | `GenerationHandle` / `GenerationHandlePool` / `GenerationHandleError` | 最小空きslot割り当てと、世代検査による解放済みhandleの拒否。 |

### 失敗条件

失敗は例外ではなく `bool` と `*Error` 列挙で返します（引数のnullや範囲外など、呼び出し側の明確な誤りは例外です）。

- 失敗した操作は状態を変更しません。`AdvanceTicks`、`Reset`、`TryAppend`、`TryWrite*` はいずれも「失敗したら何も起きていない」ことを保証します。
- `StepDurationTicks` は1以上、`MaximumStepsPerAdvance` は1以上4096以下、elapsed ticks は0以上です。
- 累積step数や累積drop tickが `long` を超える進行は `Overflow` で拒否します。
- `DeterministicRandomState` は `AlgorithmVersion` を持ち、未知の版や全語0の状態は復元を拒否します。
- `Fixed32` は加減乗除・符号反転のすべてで桁あふれを検査し、`Fixed32Result` で成否を返します。0除算も拒否します。
- `ReplayTapeBuilder` は tick 非減少、`commandId != 0`、上限byte数（既定1MiB）・上限件数（既定65536）を検査します。
- `CanonicalPayloadValue` / `ReplayTapeValue` の `TryParse` / `TryCreate` は、切り詰め・長さ不整合・上限超過を拒否します。
- `GenerationHandlePool` は容量1〜1,000,000で、世代が一巡したslotは再利用せず `RetiredCount` に退避します。

### 非対象

ゲームロジック・物理・描画の実行、入力の取得、network同期、rollback、file I/O、暗号署名、
callback・event bus・DI container・自動生成singleton・`DontDestroyOnLoad` は含みません。
いずれも利用側または別 module の責務です。

### 決定性の範囲

同じ設定・同じ初期状態・同じ順序の整数入力列に対して、各呼び出しの結果と最終状態が一致することを保証します。
catch-up 上限を超える場合は呼び出し単位でdrop判定するため、**入力をどう分割したか**も入力契約の一部です
（合計時間が同じでも、1回で渡すか2回に分けるかで結果が変わり得ます）。

### テスト範囲

- `DeterministicSimulation.Tests`（EditMode）: Unityの実行状態に依存しない純粋検証。境界値、失敗時の無変更、
  再現性、分割入力、drop、overflow、状態の保存と復元を扱います。
- 各サンプル同梱の `*.Samples.PlayMode.Tests`: 実際のButton callbackと、実 `PanelSettings` 上の
  960x600 / 640x360 geometry を検証します。

## 統合前のpackage（互換入口）

このpackageは、次の7つを1つの配布単位へまとめたものです。

| 旧UPM識別子 | 旧tag | 現在の位置 |
|---|---|---|
| `com.studiogaku.simulation-clock` | `simulation-clock-v1.0.0` | `Runtime/SimulationClock` |
| `com.studiogaku.deterministic-random` | `deterministic-random-v1.0.0` | `Runtime/DeterministicRandom` |
| `com.studiogaku.state-fingerprint` | `state-fingerprint-v1.0.0` | `Runtime/StateFingerprint` |
| `com.studiogaku.replay-tape` | `replay-tape-v1.0.0` | `Runtime/ReplayTape` |
| `com.studiogaku.canonical-payload` | `canonical-payload-v1.0.0` | `Runtime/CanonicalPayload` |
| `com.studiogaku.fixed-point` | `fixed-point-v1.0.0` | `Runtime/FixedPoint` |
| `com.studiogaku.generational-handle` | `generational-handle-v1.0.0` | `Runtime/GenerationalHandle` |

公開済みのtagとUPM識別子は削除していません。既存利用者の互換入口としてそのまま有効です。

**C#の名前空間・型名・メンバーは統合前と一切変わりません。**
`using SimulationClock;` や `using GenerationalHandles;` はそのまま通るため、移行にあたって既存コードの編集は不要です。
変わるのは、Package Manager で入れる package が7つから1つになる点と、
asmdef の References が各 `*.Runtime` から `DeterministicSimulation.Runtime` の1つになる点だけです。
