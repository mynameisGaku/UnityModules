# Startup Flow 1.0.0

## 目的

ゲーム開始時の設定読込、cache準備、利用規約確認、接続準備などを利用側が任意の順序で直接呼ぶと、失敗時の停止位置、進捗表示、cancel、二重起動の扱いが分散します。Startup Flowは各処理の中身を所有せず、明示された非同期stepを決定論的に直列実行する境界だけを提供します。

## 実行順

`RunAsync` は開始前に全stepの `Id` と `Order` をsnapshotします。次の順で並べ替えます。

1. `Order` の数値昇順
2. 同じ `Order` は `Id` のordinal昇順

`Id` は空白以外で128文字以内、同一flow内で一意にします。step数は128件以下です。検証失敗時はstepを1件も呼びません。

## 進捗

stepは `StartupStepContext.ReportProgress` へ0以上1以下の有限値を渡します。同じstep内で進捗を戻すことはできません。全体進捗は次で計算します。

```text
(完了step数 + 現在step進捗) / 総step数
```

進捗通知、`StatusChanged`、`Finished` はUnityメインスレッド上です。stepがbackground threadで完了した場合も、Serviceは `Awaitable.MainThreadAsync` で戻ってから次stepへ進みます。background threadからの `ReportProgress` は `MainThreadRequired` です。

## 失敗とcancel

| Error | 意味 |
|---|---|
| `Busy` | flow実行中、完了処理中、またはcallback通知中 |
| `MainThreadRequired` | Unityメインスレッド以外から開始または進捗通知 |
| `InvalidSteps` | null一覧、null step、不正Id、metadata取得失敗 |
| `TooManySteps` | 128件超過 |
| `DuplicateStepId` | ordinalで同じIdが複数 |
| `InvalidProgress` | 非有限、範囲外、または減少する進捗 |
| `StepNotActive` | 完了済みまたは別stepのcontext |
| `Canceled` | 利用側tokenによる協調cancel |
| `ApplicationExiting` | `Application.exitCancellationToken` による終了 |
| `StepFailed` | step例外、またはtoken未要求のcancel例外 |
| `OperationFailed` | Service内部のthread復帰または環境確認失敗 |

cancelは現在stepを強制停止しません。stepは `context.CancellationToken` を待機APIへ渡すか、`IsCancellationRequested` を定期的に確認してください。Serviceは現在stepが戻った時点で後続を開始せず、失敗位置と完了件数を返します。

## callback

`StatusChanged` は段階・step・進捗の変化、`Finished` は受理済みflowのterminal結果を通知します。observer例外は他observerと処理本体へ伝播せず、同じobserverの連続失敗は最初の1回だけConsoleへ記録します。callback中、terminal通知中、Idle復帰通知中の `RunAsync` は `Busy` です。

Awaitable completionを受け取る直前にはServiceのbusy状態とactive contextを解除済みです。continuationは次flowを開始できます。continuation例外は次flowへ混入しません。

## 非目標

このpackageはDI container、service locator、並列scheduler、DAG、retry、timeout、rollback、Scene loader、loading screen、入力lock、timeScale、audio、永続化を提供しません。既存moduleとの連携は利用側stepの中で明示してください。

## Sampleと検証

**Startup Flow Basics** は次を確認します。

- 成功3 stepの順序と進捗
- 2番目の失敗で3番目を呼ばないこと
- 長いstepへの協調cancel
- 完了後のResetと再実行
- 960×600の5 Button 1列と640×360の3+2列

package testsはvalidation、順序、進捗、callback再入、observer例外、completion continuation、main/background thread、timeScale 0を検証します。
