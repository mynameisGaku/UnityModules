# Weighted Choice Table

最大32件の正weightをID昇順の累積区間へ変換し、呼出側が渡した`[0, 1)`のsampleから1件を選ぶ純粋C# moduleです。乱数生成は行わないため、同じentry集合とsampleから同じ結果を再構築できます。

## Install

```text
https://github.com/mynameisGaku/UnityModules.git?path=/WeightedChoiceTable#weighted-choice-table-v1.0.0
```

Unity 6000.5以降を対象とします。Runtime assemblyはUnityEngineへ依存しません。UI Toolkit built-in moduleは同梱sampleだけが利用します。

## Quick start

```csharp
using GameplaySelection;

var table = new WeightedChoiceTable();
table.Add(10, 6d); // Common: [0, 6)
table.Add(20, 3d); // Rare:   [6, 9)
table.Add(30, 1d); // Epic:   [9, 10)

var result = table.Select(0.65d);
// result.SelectedIdentifier == 20
// result.Ticket == 6.5
// result.IntervalStart == 6 / IntervalEnd == 9
```

## Boundary

- Input: 正の一意ID、有限で正のweight、有限な0以上1未満のsample
- State: 最大32件のentry、ID昇順、ID昇順で再計算した有限weight合計
- Output: 選択ID・sorted index・entry weight・ticket・半開累積区間・total weight・明示error
- Dependency: 乱数、時間、Scene、resource、loot規則へ依存しない

`Add`・`Update`・`Remove`・`Clear`は変更前後のweight・合計・件数を`WeightedChoiceChangeResult`へ返します。重複ID、無効weight、容量超過、合計overflowはtableを変更せず拒否します。

`Select`はstateを変更しません。sampleを`totalWeight`へ掛けたticketを、ID昇順の`[IntervalStart, IntervalEnd)`へ照合します。浮動小数の端点丸めでticketがtotalへ一致した場合も、最大sampleが最後のentryから外れないよう有限範囲へ補正します。

## Non-goals

- 乱数またはseedの生成・所有
- loot item生成、rarity規則、保証枠、重複排除抽選
- 時間制限、entry期限、cooldown
- Unity object参照、singleton、service locator

乱数列が必要なら、利用側でDeterministic Random等から`[0, 1)`のsampleを作り、明示的に`Select`へ渡してください。

## Sample

Package Managerから`Weighted Choice Table Basics`をimportすると、weight 6・3・1の追加とsample 0.65・0.95の選択を実Buttonで確認できます。960×600では5 Button 1列、640×360では3+2列の実Panel geometryを検証します。
