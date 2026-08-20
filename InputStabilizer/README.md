# Input Stabilizer

同じsigned integer commandが指定回数連続した時だけ現在値を更新し、一時的な入力ちらつきをsimulationへ流さないEngine非依存state machineです。

Input Quantizerの整数出力や、利用側で生成した離散commandをcaller定義のsample単位で安定化できます。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputStabilizer#input-stabilizer-v1.0.0
```

または`InputStabilizer`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
if (!InputCommandStabilizer.TryCreate(3, 0, out var stabilizer, out var error)) return;

stabilizer.Push(4); // current 0, candidate 4, count 1
stabilizer.Push(4); // current 0, candidate 4, count 2
var committed = stabilizer.Push(4); // current 4, Changed true

stabilizer.Push(-4); // 1 sampleだけのnoise候補
var cancelled = stabilizer.Push(4); // currentへ戻り候補を破棄
```

## 固定契約

- 必要連続sample数は`1`以上`65535`以下
- callerが`Push`した1 commandを1 sampleとして数える
- 現在値と異なる最初のcommandを候補count 1として保持
- 同じ候補が続くたびcountを1増やす
- 別候補へ変わった場合は新候補count 1から再開
- 現在値へ戻った場合は待機候補を取り消す
- 必要countへ達したsampleだけ`Changed=true`で確定
- `Reset`は現在値を明示更新し、待機候補を破棄

現在時刻、Unity frame、乱数、Unity API、global stateを読みません。必要な待ち時間ではなく、必要なsample数だけを構成します。

## 境界の置き方

device入力はInput Gateなどのadapter側で取得し、必要ならInput Quantizerでinteger commandへ変換してから`Push`します。固定simulation tickごとに1回呼べば、安定化遅延もtick数として再現できます。

## 非目標

- Input SystemやLegacy Input Managerからの読取
- analog dead zone、量子化、smoothing、補間、rate limit
- 経過秒数やUnity frameを使うdebounce
- button edge buffering、combo、repeat
- global service、singleton、event通知
- 保存、network、Replay再生

## Sample

Package Managerから`Input Stabilizer Basics`をimportすると、+4候補の3回目確定と1回だけの-4 noise取消を設定済みSceneで確認できます。
