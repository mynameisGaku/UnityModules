# Deterministic Random

同じseedまたは保存状態から、同じ64-bit列・範囲整数・浮動小数を再現するEngine非依存の疑似乱数streamです。
algorithm versionと256-bit状態を明示し、Unityのglobal乱数や現在時刻を読みません。

対応: **Unity 6000.5.7f1 以降**

## 導入

Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/DeterministicRandom#deterministic-random-v1.0.0
```

Runtime API自体に追加package依存はありません。組込UI Toolkit moduleは同梱サンプルの表示だけに使います。

## 解決する問題

ゲームロジックが`UnityEngine.Random`や所有者不明の`System.Random`を内部から使うと、保存・復元・Replay・失敗再現で乱数位置が隠れます。
Deterministic Randomは抽選ルールを持たず、次の変換だけを担当します。

```text
明示seed または version付きState + draw operation
                         ↓
             DeterministicRandomStream
                         ↓
       exact value + next saveable State
```

## 最小例

```csharp
using System;
using DeterministicRandom;

var random = DeterministicRandomStream.Create(0xC0FFEEUL);
var saved = random.State;

if (!random.TryNextInt32(1, 21, out var d20, out var error))
{
    throw new InvalidOperationException(error.ToString());
}

var first = random.NextUInt64();
random.Reset(saved);
var replayedD20 = 0;
random.TryNextInt32(1, 21, out replayedD20, out _);
var replayedFirst = random.NextUInt64();
```

同じversion、state、操作順なら`d20 == replayedD20`かつ`first == replayedFirst`です。

## algorithm契約

v1はxoshiro256**を使い、64-bit seedをSplitMix64で4 wordへ展開します。`CurrentAlgorithmVersion`は1です。
保存時は`DeterministicRandomState.AlgorithmVersion`と4つの`ulong`を一緒に保持してください。異なるversionや全word 0の状態は拒否します。

このstreamは暗号学的安全性を持ちません。token、鍵、nonce、認証、予測されてはいけない抽選には使用しないでください。

## 出力契約

- `NextUInt64`: xoshiro256**の次の64 bit
- `NextUInt32`: 次の64 bitの上位32 bit
- `NextBoolean`: 次の64 bitの最上位bit
- `NextDouble`: 上位53 bitから`[0, 1)`
- `NextSingle`: 上位24 bitから`[0, 1)`
- `TryNextUInt64`: rejection samplingによる`[0, exclusiveMax)`
- `TryNextInt32`: rejection samplingによる`[minInclusive, maxExclusive)`

不正範囲と不正stateはstreamを進めません。streamはmutableでthread-safeではないため、ownerを1つに決めてください。

## Simulation Clockとの組み合わせ

各固定stepで必要な乱数だけを決まった順序で引き、Simulation Clockのstateと乱数stateを同じsnapshotへ保存すると、時刻位置と乱数位置を独立に復元できます。
このpackage自体はSimulation Clockへ依存せず、step実行・入力記録・game state保存は利用側の責務です。

## 非目標

- 暗号乱数、secret生成
- Shuffle、重み付き抽選、loot table、sampling without replacement
- global singleton、service locator、自動生成GameObject
- Unityのglobal random stateの読取・変更
- Replay file、game state snapshot、rollback、network同期
- thread-safe共有stream、並列substream、jump関数

## サンプル

Package Managerから`Deterministic Random Basics`をimportすると、設定済みSceneで次を確認できます。

- 固定seedからの64-bit値
- 1以上20以下のD20
- 0以上1未満のdouble
- 保存stateから6出力と最終stateを完全再現
- Reset Seedによる初期位置復元
- 960x600の5 Button 1列と640x360の3+2列

## テスト

`DeterministicRandom.Tests`はreference golden vector、seed展開、保存復元、範囲、不正入力時不変条件を検証します。
import済みサンプルの`DeterministicRandom.Samples.PlayMode.Tests`は実Button callbackと実RenderTexture上のwide/narrow geometryを検証します。
