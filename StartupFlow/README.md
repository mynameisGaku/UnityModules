# 起動手順管理（StartupFlow）

## 30秒で分かる

明示した `IStartupStep` を `Order` 昇順、同値なら `Id` の ordinal 昇順で1件ずつ実行し、現在 step、step進捗、全体進捗、停止位置、完了件数を返す Unity 向け startup orchestration モジュールです。

設定読込、認証、マスターデータ準備、最初の Scene 選択など、起動時の非同期処理が `Start` や Coroutine に散らばる問題を一つの順序へまとめます。

## こんなときに使う

- 複数の初期化処理を決まった順番で実行したい。
- 起動画面へ、現在の処理名と全体進捗を表示したい。
- どの step で失敗・cancel したかを結果として受け取りたい。

## 要件

- Unity 6000.5.7f1 以降
- Runtime APIに追加package依存なし
- 同梱 UI Toolkit sample用に `com.unity.modules.uielements` 1.0.0

## 導入

Package Manager の **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/StartupFlow#startup-flow-v1.0.0
```

## 最小例

```csharp
using System.Threading;
using StartupFlow;
using UnityEngine;

sealed class LoadSettingsStep : IStartupStep
{
    public string Id => "20-load-settings";
    public int Order => 20;

    public async Awaitable ExecuteAsync(StartupStepContext context)
    {
        await Awaitable.NextFrameAsync(context.CancellationToken);
        context.ReportProgress(1f);
    }
}

var flow = new StartupFlowService();
var result = await flow.RunAsync(new IStartupStep[] { new LoadSettingsStep() }, CancellationToken.None);
if (!result.IsSuccess) Debug.LogError($"{result.Error}: {result.FailedStepId}");
```

`Awaitable` は一度だけ await してください。stepはUnity APIへ触れる前後を自分で管理し、background threadで完了する可能性がある場合もServiceが各step後にmain threadへ戻ってから次stepと通知を処理します。

## 契約

- 1回の実行は128 stepまで。`Id` は空白以外で128文字以内、同一flow内でordinal一意です。
- `Order` が同じstepは `Id` のordinal順で実行します。入力collectionの列挙順には依存しません。
- `ReportProgress` はmain thread限定、有限の0以上1以下、step内で単調非減少です。
- `StatusChanged` と `Finished` の例外はflowから隔離します。callback中の再入は `Busy` です。
- 利用側tokenと `Application.exitCancellationToken` をstepへ結合して渡します。
- cancelは協調方式です。現在stepがtokenを監視せず完了もしない場合、Serviceはその処理を強制停止できません。
- step例外またはtoken未要求の `OperationCanceledException` は `StepFailed` です。
- 同じServiceで同時に2 flowは実行できません。完了後は再利用できます。

## 含まないもの

- step間の依存graph、並列実行、優先度queue
- retry、timeout、rollback、永続化
- Scene読込、画面遷移、入力停止、時間制御、音声制御
- stepの自動探索、reflection登録、service locator
- global singleton、自動生成GameObject、`DontDestroyOnLoad`

## Sample

Package Managerから **Startup Flow Basics** をImportし、`StartupFlowBasics.unity` を開いてください。Success、Failure、Slow、Cancel、Resetの実Buttonとresponsive UIで契約を確認できます。
