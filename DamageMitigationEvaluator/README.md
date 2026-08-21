# Damage Mitigation Evaluator

元damageへ最大32件の固定軽減・率軽減を入力順に適用し、stateを変更せず各段階の要求量・実適用量・残damageを返す純粋C# moduleです。

## Install

Unity Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/DamageMitigationEvaluator#damage-mitigation-evaluator-v1.0.0
```

Unity 6000.5以降に対応します。Runtime APIはUnityEngineへ依存しません。付属sampleだけがbuilt-in UI Toolkitを使います。

## Quick start

```csharp
using GameplayDamage;

var layers = new[]
{
    new DamageMitigationLayer(1, DamageMitigationKind.FlatReduction, 20d),
    new DamageMitigationLayer(2, DamageMitigationKind.RatioReduction, 0.25d)
};

DamageMitigationEvaluator.TryEvaluate(100d, layers, out var evaluation, out _);
// flat: 100 - 20 = 80
// ratio: 80 - 25% = 60
// evaluation.FinalDamage == 60
```

## Contract

- Input: 0以上の有限damageと、IDが重複しない0〜32件の軽減層
- Processing: `FlatReduction`は固定量、`RatioReduction`はその時点のdamageに0〜1の軽減率を適用
- Output: 元damage、最終damage、実軽減合計と、入力順のrequested・applied・output明細
- State: なし。入力配列、HP、shield、armor、effectを変更しない

固定軽減が残damageを超える場合、要求量は保持したまま実適用量だけを残damageまでに制限します。最終damageは0未満になりません。入力順は計算結果の一部であり、flat→ratioとratio→flatは異なる結果になり得ます。

## 既存moduleとの境界

Stat Modifier Stackは攻撃力や防御力のstatを合成します。Resource MeterはHPやshieldのstateを更新します。このmoduleは合成済みの元damageと明示した軽減層だけを評価し、stat計算やHP適用を行いません。

## Non-goals

HP・shield・armor stateの更新、critical、damage type対応表、貫通、吸収、反射、最低保証damage、random、effect検索、優先度sort、callback、network同期、永続化は対象外です。必要な順序と層はcallerが明示してください。

## Sample

`Damage Mitigation Evaluator Basics`はflat・ratio・ordered・clamp・invalidを実Buttonで評価します。960×600では5 Button 1列、640×360では3+2列です。
