# Stack Transfer Planner

## 目的

stack-based inventoryやstorage間の移送では、source不足とdestination空き不足を同時に扱いながら、UI preview・server検証・実適用で同じ明細を使う必要があります。このmoduleはstateを変更せず、1 item種の整数unit移送計画だけを決定論的に構築します。

## 契約

TryPlanは1〜32 source、1〜32 destination、0〜1,000,000,000 requested unitsを受け取ります。

- source: 正の固有ID、0〜1,000,000,000 available units
- destination: 正の固有ID、1〜1,000,000,000 capacity、0〜capacity current units
- source IDとdestination IDの一意性は各配列内で判定し、配列間の同一IDは許可
- transferred = min(requested, source合計, destination空き合計)
- sourceを入力順で減らし、destinationを入力順で満たす
- 入力配列を変更しない

成功時のStackTransferPlanはrequested、transferred、unfulfilled、source合計、destination空き合計と、入力順の全明細を保持します。source明細はbefore・moved・after、destination明細はbefore・capacity・received・afterを返します。両側の移送unit合計は常にTransferredUnitsと一致します。

## 失敗順序

null source、source件数、null destination、destination件数、requested units、source ID・重複・unit、destination ID・重複・capacity・current unitsの順で検証します。失敗時はplanをnullにし、部分明細を返しません。

## 非目標

item identity、異種item変換、stack生成・削除、空slot探索、grid配置、sort、重量、装備、wallet、inventory mutation、transaction、rollback、callback、threading、network同期、永続化はv1対象外です。

## 検証

EditMode testsは全入力境界、エラー優先順位、source/destination制限、入力順、最大合計、保存則、入力不変性を検証します。Sample testsとMono／IL2CPP Player gateは5つの実Button結果と960×600／640×360実描画を検証します。
