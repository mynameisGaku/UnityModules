# Periodic Tick Planner

DoT、HoT、定期回復などの次回発火tick・間隔・残り回数から、指定simulation tickまでに到来した発火範囲と次cursorを計画する純粋C# moduleです。clockやeffect stateは変更しません。

## 導入

Unity 6000.5以降で、Package ManagerのAdd package from git URLへ次を入力します。

    https://github.com/mynameisGaku/UnityModules.git?path=/PeriodicTickPlanner#periodic-tick-planner-v1.0.0

## 最小例

    using GameplayTiming;

    var state = new PeriodicTickState(nextTick: 10, intervalTicks: 4, remainingCount: 5);
    PeriodicTickPlanner.TryPlan(state, throughTick: 22, maximumEmissionCount: 10, out var plan, out _);
    // plan.DueCount == 4
    // plan.EmittedCount == 4
    // plan.FirstEmittedTick == 10
    // plan.LastEmittedTick == 22
    // plan.NextState.NextTick == 26

## 境界

- Cursor: 次回tickは0以上、間隔と残り回数は最大1,000,000,000
- Completed: `PeriodicTickState.Completed`の`0 / 1 / 0`だけをcanonical完了表現として受理
- Evaluation: `throughTick`以下をinclusiveに到来済みとして数える
- Catch-up: 1回の計画は最大1,000,000発火に制限し、残りを次cursorへ保持
- State: cursor、clock、effect、GameObjectを変更しない
- Dependency: RuntimeはUnityEngineへ依存しない

結果は到来総数、今回の発火数、最初・最後の発火tick、次cursor、上限分割、完了状態を返します。大きなtick jumpも発火ごとのloopではなく整数計算で処理します。

時刻取得、simulation進行、damage/heal適用、stack再適用、effect ID、callback、Coroutine、network同期、永続化は対象外です。callerが発火範囲を自身のeffect処理へ適用してください。

## Sample

Package ManagerからPeriodic Tick Planner Basicsをimportしてください。Future、Exact、Catch-up、Limited、Completeを実Buttonで確認できます。960×600では5 Button 1列、640×360では3+2列です。
