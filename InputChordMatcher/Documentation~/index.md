# Input Chord Matcher 1.0.0

## 目的

同時押し判定をframe内の入力順やEngine時刻へ直接結び付けると、polling頻度やReplay方法によって成立結果が変わります。本moduleは、利用側が明示したtickとpressed snapshotだけからchordを再現します。

## 分解

- Input: 非減少ulong tickと、positive idを厳密昇順に並べた現在pressed snapshot
- State: required idごとの直前pressed状態と最新押下edge tick
- Output: immutable complete・trigger・span超過・再arm status、または明示error

Unity input、時刻、random、Coroutine、singletonは境界外です。

## 作成

InputChordMatcher.TryCreate(requiredCommandIds, maximumSpanTicks, initialTick, ...)はrequired列を複製して昇順化します。required数は2〜16、idはpositiveかつ重複なしです。maximumSpanTicks=0は同一tickの押下edgeだけを許します。

## Snapshot入力

TrySampleへ渡すpressed idは厳密昇順、重複なし、0〜64件です。required外のidを含められますが判定には影響しません。入力全体とtickを検証してから状態を更新するため、失敗時は現在statusが不変です。

## 成立と再arm

各required idが非押下から押下へ変わったtickを保持します。incompleteからcompleteへ入ったsampleで、最古と最新のedge差をPressSpanTicksとして計算します。

- spanが上限以下: Triggered=true
- spanが上限超過: SpanExceeded=true
- complete保持: 両flag false
- completeからincomplete: Rearmed=true

一度span超過したcompleteを保持しても再判定しません。少なくとも1 required idを解放して再armした後、新しい押下edgeで再試行します。

## 時系列

同じtickは受理しますが、同じcomplete snapshotを再発火しません。現在tickより前はTickMovedBackwardで、edge tick・pressed状態・complete状態を変更しません。Resetは新しいtimelineへ明示的に移ります。

## 非目標

- Input System、Legacy Input Manager、device polling
- 順序combo、buffer、保持repeat、tap・hold分類
- chord優先度、競合解決、効果callback
- key binding、永続化、network同期

## 検証

EditModeで入力検証、inclusive span、超過、latch、再arm、extra command、逆行、reset、max件数を検証します。import済みsampleは実PanelSettingsで960×600の5 Button 1列と640×360の3+2列、Mono/IL2CPP PlayerはtimeScale=0で同一chord結果を検証します。
