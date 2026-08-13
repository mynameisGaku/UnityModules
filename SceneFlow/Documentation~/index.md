# Scene Flow

`SceneFlowService` は、完全な Scene path を使う4種類の操作を直列化し、開始前条件と完了後状態を確認します。
導入の最短手順はパッケージ直下の [README](../README.md) を参照してください。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

## 導入

Package Manager の **Add package from git URL** に次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/SceneFlow#scene-flow-v1.0.0
```

利用側の asmdef から `SceneFlow.Runtime` を参照します。外部パッケージへの依存はありません。

## 所有と寿命

`SceneFlowService` は `MonoBehaviour` やsingletonではありません。Scene遷移を決めるゲーム側ownerが通常のfieldとして保持します。
`MonoBehaviour`のfield initializerでは作らず、`Awake`または`Start`などのUnityメインスレッドcallback内で生成します。イベント購読とすべての要求も同じメインスレッドへ集約してください。

同一serviceの要求は1件ずつ処理し、進行中の2件目を`Busy`で拒否します。別serviceや直接の`SceneManager`呼び出しは相互に直列化されないため、1つのownerへ集約します。

## 要求

```csharp
var sceneFlow = new SceneFlowService();

SceneFlowResult single = await sceneFlow.ExecuteAsync(SceneFlowRequest.LoadSingle(gameplay));
SceneFlowResult additive = await sceneFlow.ExecuteAsync(SceneFlowRequest.LoadAdditive(hud));
SceneFlowResult active = await sceneFlow.ExecuteAsync(SceneFlowRequest.SetActive(gameplay));
SceneFlowResult unload = await sceneFlow.ExecuteAsync(SceneFlowRequest.Unload(hud));
```

便利メソッド`LoadSingleAsync`、`LoadAdditiveAsync`、`SetActiveAsync`、`UnloadAsync`も同じ処理へ委譲します。

### Single

`Application.CanStreamedLevelBeLoaded`で完全pathを事前確認し、非同期読込後に対象Sceneが一意にloadedであることを確認します。Unityの`LoadSceneMode.Single`に従い、現在のSceneは置き換わります。

### Additive

対象pathが1件でもloadedなら`AlreadyLoaded`です。同じpathのSceneを複数読み込むと後続のactive切替とUnloadで対象を一意に決められないため、service経由では重複を作りません。

### SetActive

対象が一意にloadedであることを確認してから`SceneManager.SetActiveScene`を呼び、完了後もactive状態を再確認します。

### Unload

対象が一意にloadedであり、activeでなく、他にloaded Sceneが残る場合だけ開始します。active Sceneは別Sceneへ切り替えてからUnloadします。最後の1 SceneはUnityの制約に合わせ拒否します。

## 状態通知

`SceneFlowStatus`は`Phase`、`Request`、`Progress`、`IsBusy`を固定済みの値として返します。

```text
Idle
  → Validating
  → Loading → Verifying
    または Unloading
    または SettingActive
  → Completed / Failed
  → Idle
```

LoadのprogressはUnityの0〜0.9を0〜1へ正規化し、通知値を減少させません。所要時間や残り時間の割合ではありません。

`StatusChanged`と`Finished`は購読者ごとに例外を隔離します。`Finished`は検証へ受理された要求だけを通知し、メインスレッド外の呼び出し、処理中の2件目、いずれかのcallback中に再入した要求は、状態を変えず戻り値だけで`MainThreadRequired`または`Busy`を返します。次の要求はcallbackから戻った後のframeで開始してください。

## `SceneReference`

RuntimeにはGUIDと完全pathの文字列だけを保持します。PropertyDrawerはGUIDを優先してSceneAssetを解決し、移動後pathを修復します。
active Build ProfileがScene Listを上書きしている場合はその実効一覧、platform profileの場合はglobal一覧を検査します。

Scene名だけを渡すAPIはありません。`Assets/Feature/Gameplay.unity`のような完全pathにより、別フォルダーの同名Sceneを取り違えません。

## 失敗と外部変更

想定内の失敗は`SceneFlowResult.IsSuccess == false`と`SceneFlowError`で返ります。
Unity APIが操作開始後に外部から変更され、完了後の一意性、loaded、active状態が要求と違う場合は`ExternalSceneChange`または`AmbiguousScene`です。

Status/Finished callbackの例外はScene操作へ伝播しません。Unity APIまたは内部境界の予期しない例外は`OperationFailed`へ変換します。

## 終了と対象外

- `Application.exitCancellationToken`でPlay終了またはアプリ終了を検出し、待機を`ApplicationExiting`として終了します。
- Unityのloadは真にcancelできないためcancellation APIを公開しません。
- `allowSceneActivation=false`はUnityの他AsyncOperationも待たせるため使用しません。
- fade、ロード画面、入力、音声、Addressables、network同期は利用側で組み合わせます。
- Domain Reloadでmanaged owner自体が破棄される場合、terminal callbackを保証しません。
- SceneのUnloadは関連Assetを解放しません。Asset解放の時機は利用側が所有します。

## サンプル

**Scene Flow Basics**をImportし、同梱READMEのsetup menuを実行します。既存のScene Listと順序を維持したまま不足Sceneだけを追加し、Bootstrap Sceneを開きます。
