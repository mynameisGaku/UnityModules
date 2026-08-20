# Input Repeat

押下edgeを即時triggerし、保持中のrepeatを利用側が渡す明示simulation tickから決定論的に計算するEngine非依存state machineです。

入力deviceやUnity時刻を内部で読まず、initial delay、repeat interval、pressed状態をcallerが明示するため、pause、fixed simulation、Replayでも同じ発行件数を再現できます。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputRepeat#input-repeat-v1.0.0
```

または`InputRepeat`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
if (!InputRepeatTracker.TryCreate(3, 2, 100, out var repeat, out var error)) return;

repeat.TryPush(100, true, out var press, out error); // 初回trigger 1
repeat.TryPush(102, true, out var wait, out error);  // delay前なので0
repeat.TryPush(103, true, out var first, out error); // repeat 1
repeat.TryPush(110, true, out var jump, out error);  // 105, 107, 109のrepeat 3
repeat.TryPush(111, false, out var release, out error);
```

## 固定契約

- `InitialDelayTicks`と`RepeatIntervalTicks`は1以上
- 非押下から押下へのedgeはtickに関係なく初回triggerを1個発行する
- 保持中の最初のrepeatは押下tickとの差がinitial delay以上になった時に発行する
- 以後はrepeat intervalごとに発行し、同じtickを再処理しても重複しない
- tickが複数intervalを飛んだ場合、未発行repeatの全件数を`ulong`で返す
- 解放edgeは`Released`を1回返し、押下開始tickと発行済みrepeat数を破棄する
- 逆行tickは状態を変えず`TickMovedBackward`を返す
- `Snapshot`はedgeやtriggerを再発行せず、`Reset`は新しいtimelineの非押下状態へ戻す

現在時刻、Unity frame、入力device、乱数、Unity API、global stateを読みません。

## 境界の置き方

Input System等のadapterがbuttonの現在pressed状態を取得し、Simulation Clock等が管理するtickとともに`TryPush`します。`TriggerCount`回の処理を実行するか、catch-upを1回の加速処理へまとめるかは利用側が決めます。このmoduleは発行時刻の計算だけを担当します。

## 非目標

- Input SystemやLegacy Input Managerからの読取
- button mapping、edge event購読、Coroutine、秒・Unity frameによるschedule
- callback実行、catch-up件数の上限やdrop policy、repeat加速curve
- command buffer、sequence、chord、priority arbitration
- global service、singleton、file I/O、network transport

## Sample

Package Managerから`Input Repeat Basics`をimportすると、Press@100、delay前のHold@102、初回repeat@103、tick jumpによる3件catch-up@110、Release@111を設定済みSceneで確認できます。
