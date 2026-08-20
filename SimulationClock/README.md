# Simulation Clock

明示された整数の経過時間を、実行すべき連続固定step・補間端数・catch-up超過量へ変換する純粋な時計です。
RuntimeはUnityの現在時刻、`Time.timeScale`、`FixedUpdate`、Scene、GameObjectを参照しません。

対応: **Unity 6000.5.7f1 以降**

## 導入

Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/SimulationClock#simulation-clock-v1.0.0
```

Runtime API自体に追加package依存はありません。依存している組込UI Toolkit moduleは、同梱サンプルの表示だけに使います。

## 解決する問題

ゲームロジックがUnityの現在時刻を内部から読むと、同じ入力を再生してもフレーム状況によって結果が変わり、時計だけの保存・復元・高速検証も難しくなります。
Simulation Clockはゲーム処理を実行せず、次の変換だけを担当します。

```text
明示elapsed ticks + 保存可能なClock State + Settings
                         ↓
                 FixedStepClock
                         ↓
連続step範囲 + 端数 + 補間率 + 明示drop量 + Next State
```

## 最小例

```csharp
using System;
using SimulationClock;

var settings = new FixedStepClockSettings(
    TimeSpan.TicksPerMillisecond * 20L,
    maximumStepsPerAdvance: 4);

if (!FixedStepClock.TryCreate(settings, out var clock, out var createError))
{
    throw new InvalidOperationException(createError.ToString());
}

var result = clock.AdvanceTicks(TimeSpan.TicksPerMillisecond * 33L);
if (!result.IsSuccess)
{
    throw new InvalidOperationException(result.Error.ToString());
}

for (var offset = 0; offset < result.StepCount; offset++)
{
    var stepIndex = result.FirstStepIndex + offset;
    SimulateOneStep(stepIndex, result.StepDurationTicks);
}

RenderInterpolated(result.InterpolationAlpha);
```

`State`を保存して`TryCreate(settings, state, ...)`または`Reset(state)`へ渡すと、同じ位置から再構築できます。

## Unity時間との境界

Unityのフレーム時間を使う場合も、Runtime時計の外で明示的に整数tickへ変換します。

```csharp
var elapsedTicks = (long)Math.Round(
    Time.unscaledDeltaTime * TimeSpan.TicksPerSecond,
    MidpointRounding.AwayFromZero);

var result = clock.AdvanceTicks(elapsedTicks);
```

再現時はfloatの元値ではなく、実際に時計へ渡した整数`elapsedTicks`列を記録してください。同じ設定・初期状態・整数入力列なら、同じ結果と次状態になります。

## catch-up上限

1回の入力から発生したstepが`MaximumStepsPerAdvance`を超える場合、上限件数だけを連続stepとして返します。超過分は実行せず、`DroppedStepCount`と`DroppedTicks`へ明示します。
破棄後も`FirstStepIndex`は飛ばさず、次回は直前に返したstepの続きになります。これはhitch時の処理集中を制限し、いわゆるspiral of doomを避けるための境界です。

## エラーと不変条件

- `StepDurationTicks`は1以上
- `MaximumStepsPerAdvance`は1以上4096以下
- elapsed ticksは0以上
- `RemainderTicks`は0以上step時間未満
- 完了件数・累積破棄tickが`long`を超える進行は`Overflow`で拒否
- 失敗した`AdvanceTicks`と`Reset`は状態を変更しない

## 非目標

- ゲームロジックや物理の実行
- 入力記録、Replay file、network同期
- Unityの`Time.fixedDeltaTime`、`timeScale`、`FixedUpdate`の変更
- callback、event bus、scheduler、timer
- 自動生成singleton、`DontDestroyOnLoad`
- rollback、snapshot保存形式、random seed管理

これらは利用側または別moduleの責務です。

## サンプル

Package Managerから`Simulation Clock Basics`をimportすると、設定済みSceneで次を確認できます。

- 16ms / 33msの整数入力と端数蓄積
- 500ms hitchを4stepへ制限し、21stepを明示drop
- 同じ入力列を2つの時計へ再生した完全一致
- Resetによる初期状態復元
- 960x600の5 Button 1列と640x360の3+2列

## テスト

`SimulationClock.Tests`は純粋EditMode検証で、境界・再現性・分割入力・drop・overflow・復元を扱います。
import済みサンプルの`SimulationClock.Samples.PlayMode.Tests`は、実Button callbackと実RenderTexture上のwide/narrow geometryを検証します。
