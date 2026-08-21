# Stat Modifier Stack

攻撃力・移動速度・防御力等の有限base値へ、caller所有のmodifierをID昇順で決定論的に合成するEngine非依存stackです。Flat、加算percent、乗算factorの3 stageと、変更前後値・stage合計・件数・失敗理由を明示します。

## 導入

```text
https://github.com/mynameisGaku/UnityModules.git?path=/StatModifierStack#stat-modifier-stack-v1.0.0
```

または`StatModifierStack`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using GameplayStats;

if (!StatModifierStack.TryCreate(100d, out var stats, out var error)) return;

stats.Add(10, StatModifierKind.Flat, 15d);
stats.Add(20, StatModifierKind.AdditivePercent, 0.2d);
var result = stats.Add(30, StatModifierKind.MultiplicativeFactor, 1.5d);
// (100 + 15) * (1 + 0.2) * 1.5 = 207
```

## 固定契約

- base値とmodifier値は有限値
- modifier IDはcallerが割り当てる正の`long`で、stack内で一意
- 最大32件、常にID昇順で保持
- `Flat`値をID昇順で合計し、baseへ加算
- `AdditivePercent`値をID昇順で合計し、`1 + total`を乗算
- `MultiplicativeFactor`値をID昇順で順に乗算
- 各stageまたは最終値がNaN・Infinityになる変更はstateを変えず拒否
- add・update・remove・base変更・clearの成功結果から変更前後と全stageを再構築可能
- `TryGetModifierAt`はID昇順snapshotをallocationなしで返す

内部で時刻、frame、deltaTime、乱数、Unity API、global stateを読みません。

## Boundary

modifierの寿命とIDは利用側が所有します。装備・buff・difficulty等のsystemが明示eventで`Add`、`Update`、`Remove`を呼び、最終値を読み取ります。Resource Meter、Save System、Simulation Clockとはhard dependencyを持ちません。

## 非目標

- duration、expiry、cooldown、tick、automatic removal
- priority、override、minimum・maximum clamp、curve
- modifier source object、tag、group、一括remove
- status effect、damage formula、attribute graph
- callback、event、global service、singleton
- serialization、file I/O、network transport

## Sample

`Stat Modifier Stack Basics`ではFlat +15、Additive +20%、Factor ×1.5、Factor ×0.5へのupdate、重複ID拒否を左から順に実Buttonで確認できます。
