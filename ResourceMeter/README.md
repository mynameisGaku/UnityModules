# Resource Meter

health・stamina・mana等の有限resourceを、immutableなcapacityの中で回復・部分消費・全量必須消費するEngine非依存の小さなstate holderです。各操作は前後値、要求delta、実適用delta、未適用delta、境界遷移を明示結果で返します。

## 導入

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ResourceMeter#resource-meter-v1.0.0
```

または`ResourceMeter`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using GameplayResources;

if (!ResourceMeter.TryCreate(100d, 40d, out var meter, out var error)) return;

var restored = meter.Restore(30d); // current 70、applied +30
var spent = meter.Spend(80d, ResourceSpendPolicy.AllowPartial); // current 0、applied -70、unapplied -10
```

## 固定契約

- capacityは作成後に変化しない、0より大きい有限値
- 現在値・reset値は有限の`0 <= value <= capacity`
- 回復・消費amountは有限の非負値
- 回復はcapacityでclampし、超過分を正の`UnappliedDelta`で返す
- `AllowPartial`消費は不足時に0まで消費し、残量を負の`UnappliedDelta`で返す
- `RequireFull`消費は不足時に現在値を変えず、要求全量を未適用として返す
- 有効だが全量を適用できない要求も処理成功であり、`WasFullyApplied=false`
- 不正入力・不正policy・不正resetは現在stateを変えない
- `PreviousValue`、`CurrentValue`、`Capacity`、3種類のdeltaと境界flagから変更を再構築できる

内部で時刻、frame、deltaTime、乱数、Unity API、global stateを読みません。

## 境界

利用側がgameplayの明示eventに応じて`Restore`または`Spend`を呼びます。再生開始・load・respawn等で既知stateへ戻す場合だけ`TryReset`を使います。Save System、Time Control、UI、effectとはhard dependencyを持ちません。

## 非目標

- 自動回復、cooldown、expiry、timer、deltaTime読取
- status effect、damage formula、shield、priority、reservation
- capacityのruntime変更、overflow buffer、負resource
- callback、event、global service、singleton
- UI、animation、audio、入力、Save System連携
- file I/O、network transport、Replay再生

## Sample

`Resource Meter Basics`では回復、部分消費、全量必須消費、不足による非変更、exact消費、不正amount拒否を実Buttonで確認できます。
