# Timed Stack Resolver

## 目的

時限effectの再適用では、stack数と残り時間を別々の規則で組み合わせる必要があります。このmoduleは現在状態・追加状態・明示方針だけを入力にし、状態を変更せず再適用結果を返します。UI preview、server検証、実適用で同じ整数計算を共有できます。

## 入力契約

- 現在状態: 非activeは`0 / 0`、activeはstack数と残りtick数の両方が1以上かつ方針上限以内
- 追加状態: stack数と残りtick数の両方が1〜1,000,000,000
- 方針上限: stack数、残りtick数とも1〜1,000,000,000
- stack方法: 加算、置換、大きい方
- duration方法: 更新、加算、大きい方

追加状態は方針上限を超えられます。これは強い再適用要求を拒否せず、結果を方針上限へ収めた事実を`StackClamped`と`DurationClamped`で返すためです。加算は64bitの中間値を使うため、最大値同士でもoverflowしません。

## 検証順序

最大stack数、最大残りtick数、stack方法、duration方法、現在状態、追加状態の順で検証します。失敗時はdefault結果と対応する`TimedStackError`を返し、部分結果は返しません。

## 結果

`TimedStackResolution`は`PreviousState`、`IncomingState`、`ResultState`、`Policy`を保持します。さらに`WasInactive`、`StackCountChanged`、`DurationChanged`、`StackClamped`、`DurationClamped`から、callerは表示や適用理由を再計算せず判断できます。

## 非目標

effect ID、異種effectの統合、強度比較、周期damage、clock、残り時間の自動減算、GameObject更新、MonoBehaviour、Coroutine、callback、threading、network同期、永続化はv1対象外です。

## 検証

EditMode testsは全6方針、非active、上限、64bit中間値、入力保持、決定性、全失敗境界と優先順位を検証します。Sample testsとMono／IL2CPP Player gateは5つの実Button結果と960×600／640×360実描画を検証します。
