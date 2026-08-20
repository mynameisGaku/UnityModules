# Generational Handle 1.0.0

## 目的

整数IDを解放後に再利用すると、古い参照が同じ整数を持つ新しいobjectを誤って操作できます。このmoduleはslot番号へgenerationを組み合わせ、再利用前後を明示的に区別します。

## Input / State / Output

- Input: constructorのcapacity、`TryAcquire`と`Release`の呼出順
- State: slotごとの現在generation、active状態、最小順のfree slot集合
- Output: `GenerationHandle`、または`GenerationHandleError`

時刻、乱数、frame、Unity global、thread schedulerは参照しません。

## 割当順

まだ使われていないslotを0から昇順に割り当てます。解放slotがある場合は、その中の最小番号を先に使います。したがって、同じcapacityと同じ操作列は同じhandle列になります。

## 古いhandle

有効handleを解放するとgenerationを1増やします。同じslotを再取得してもgenerationが異なるため、以前のhandleは`IsActive`でfalseとなり、`Release`で`StaleHandle`になります。

`uint.MaxValue`はwrapしません。そのgenerationを解放したslotはretireし、`RetiredCount`へ加算して以後割り当てません。

## 失敗時の非変更

- full poolのAcquireは`CapacityReached`
- defaultまたは未割当slotは`InvalidHandle`
- 解放済みまたはgeneration不一致は`StaleHandle`

いずれもactive entry、free slot、generationを変更しません。

## ownership

Poolはplain managed objectで、thread-safeではありません。gameplay world、simulation、resource registryなど1つのownerが生成と破棄を管理してください。Moduleはobject lookupやglobal instanceを作りません。

## 検証

EditMode testsはascending allocation、minimum-free reuse、stale rejection、capacity failure、generation上限retire、culture非依存表示、公開3型、UnityEngine非参照を検証します。Sample testsは実Buttonのgolden列と960x600／640x360の実Panel geometryを検証します。
