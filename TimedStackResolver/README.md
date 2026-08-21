# Timed Stack Resolver

buff、debuff、DoTなどの現在stack数・残りtick数と追加状態から、再適用後の状態を決定論的に求める純粋C# moduleです。GameObject、clock、effect stateは変更しません。

## 導入

Unity 6000.5以降で、Package ManagerのAdd package from git URLへ次を入力します。

    https://github.com/mynameisGaku/UnityModules.git?path=/TimedStackResolver#timed-stack-resolver-v1.0.0

## 最小例

    using GameplayEffects;

    var current = new TimedStackState(stackCount: 2, remainingTicks: 50);
    var incoming = new TimedStackState(stackCount: 2, remainingTicks: 30);
    var policy = new TimedStackPolicy(
        maximumStackCount: 3,
        maximumDurationTicks: 100,
        stackMode: TimedStackCountMode.AddClamped,
        durationMode: TimedStackDurationMode.RefreshClamped);

    TimedStackResolver.TryResolve(current, incoming, policy, out var resolution, out _);
    // resolution.ResultState.StackCount == 3
    // resolution.ResultState.RemainingTicks == 30
    // resolution.StackClamped == true

## 方針

- Stack: `AddClamped`、`ReplaceClamped`、`MaximumClamped`
- Duration: `RefreshClamped`、`AddClamped`、`MaximumClamped`
- Limit: stack数とtick数はいずれも1〜1,000,000,000
- Inactive: 現在状態の`0 stacks / 0 ticks`だけを非active表現として受理
- State: 入力値、effect、clock、GameObjectを変更しない
- Dependency: RuntimeはUnityEngineへ依存しない

結果は再適用前・追加・再適用後の状態、使用方針、非activeだったか、各値が変化したか、上限へ収めたかを保持します。追加状態は方針上限を超えても共通上限内なら受理し、結果側で明示的にclampします。

effect ID、強度比較、周期damage、残り時間の減算、frame更新、stack生成・削除、callback、network同期、永続化は対象外です。callerが自身のclockとeffect stateへ結果を適用してください。

## Sample

Package ManagerからTimed Stack Resolver Basicsをimportしてください。Add + Refresh、Add + Extend、Maximum、Replace、Inactive → Activeを実Buttonで確認できます。960×600では5 Button 1列、640×360では3+2列です。
