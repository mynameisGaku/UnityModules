# Input Command Arbiter

同一simulation stepで成立したcommand候補から、明示priorityと安定した入力順で1件を選ぶEngine非依存moduleです。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputCommandArbiter#input-command-arbiter-v1.0.0
```

または`InputCommandArbiter`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using InputArbitration;

var candidates = new[]
{
    new InputCommandCandidate(commandId: 10, priority: 100, isEligible: true),  // Attack
    new InputCommandCandidate(commandId: 20, priority: 200, isEligible: true),  // Interact
    new InputCommandCandidate(commandId: 30, priority: 300, isEligible: false)  // Dodge
};

var result = InputCommandArbiter.Select(candidates);
// result.CommandId == 20, result.SelectedIndex == 1
```

## 固定契約

- 候補は最大64件で、全command idは正かつ一意
- `IsEligible`がtrueの候補だけを選択対象にする
- 整数`Priority`が大きい候補を優先する
- priority同値ではordered listの小さいindexを選ぶ
- 不正idや重複idはineligible候補を含めて選択前に拒否する
- 候補0件またはeligible 0件は成功した未選択結果
- 入力listを変更せず、時刻・乱数・Unity API・global stateを読まない

入力順はtie-break規則の一部です。Dictionary等の列挙順へ暗黙依存せず、呼出側が再現可能なordered listを渡してください。

## 境界

候補の生成、Input System読取、command実行、buffer消費は利用側adapterの責務です。Input Command Buffer、Input Chord Matcher、Input Sequence Matcher等の結果を候補へ変換できますが、hard dependencyはありません。

## 非目標

- Input System・Legacy Input Manager・binding
- command検出、edge、repeat、tap、hold、chord、sequence
- tick・実時間・timeout・cooldown
- queue、buffer消費、callback、effect実行
- dynamic priority計算、random tie-break、global service
- file I/O、network transport、Replay再生

## Sample

`Input Command Arbiter Basics`では未選択、単独Attack、高priorityのInteract、同priorityの先頭候補、重複id拒否を実Buttonで確認できます。
