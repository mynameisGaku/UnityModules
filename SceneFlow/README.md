# シーン切り替え（SceneFlow）

## 30秒で分かる

`SceneFlowService` が、Scene の非同期読込、有効 Scene の切替、アンロードを1件ずつ実行する小さな遷移モジュールです。
短い Scene 名ではなく完全な Asset path を使い、開始前の条件と完了後の実 Scene 状態を結果型で確認します。

Scene 名の打ち間違い、同時ロード、Build Profile への登録漏れ、Additive Scene の切替順を利用側だけで管理する手間を減らします。

## こんなときに使う

- タイトル、ゲーム本編、結果画面を安全に切り替えたい。
- 常駐 Scene と Gameplay Scene を Additive で組み合わせたい。
- Scene 操作の失敗理由を Console の文字列ではなく結果型で扱いたい。
- Scene 移動後も参照が壊れにくい設定を Inspector で持ちたい。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

外部パッケージへの依存、常駐 `MonoBehaviour`、global singleton、ロード画面 UI はありません。

## インストール

Package Manager の **Add package from git URL** に固定タグ付き URL を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/SceneFlow#scene-flow-v1.0.0
```

利用側に asmdef がある場合は `SceneFlow.Runtime` を参照します。

フォルダーを直接管理する場合だけ、`SceneFlow/` を `Assets/Modules/SceneFlow/` へ配置してください。

## 最小例

```csharp
using SceneFlow;
using UnityEngine;

public sealed class SceneOwner : MonoBehaviour
{
    [SerializeField] private SceneReference _gameplay;

    private SceneFlowService _sceneFlow;

    private void Awake()
    {
        _sceneFlow = new SceneFlowService();
    }

    public async void OpenGameplay()
    {
        var result = await _sceneFlow.LoadSingleAsync(_gameplay);
        if (!result.IsSuccess)
        {
            Debug.LogError($"Scene遷移失敗: {result.Error} / {result.Message}");
        }
    }
}
```

`SceneFlowService` は利用側が所有する通常の C# object です。`MonoBehaviour` のfield initializerでは作らず、`Awake`または`Start`などのUnityメインスレッドcallback内で生成してください。すべての呼び出しも同じメインスレッドへ集約します。

## 操作

| API | 内容 |
|---|---|
| `LoadSingleAsync` | 現在の Scene を置き換えて対象 Scene を非同期読込する |
| `LoadAdditiveAsync` | 現在の Scene を残して対象 Scene を追加読込する |
| `SetActiveAsync` | 読込済みの一意な Scene を有効 Scene にする |
| `UnloadAsync` | 読込済みの一意な非active Sceneをアンロードする |
| `ExecuteAsync` | `SceneFlowRequest` で上記4操作を共通に実行する |

同じサービスが処理中、またはcallbackを通知中のとき、新しい要求は Unity API に触れず `SceneFlowError.Busy` を返します。
直列化されるのは同じ `SceneFlowService` を通した要求だけです。利用側は実行中の Scene 変更をこの所有者へ集約し、外部から `SceneManager` を同時操作しないでください。

## `SceneReference`

Inspector では `SceneAsset` として選択できます。Runtime に保持するのは GUID と、`Assets/.../*.unity` または `Packages/.../*.unity` の完全な path だけで、`UnityEditor` 型は含みません。

- GUID が有効なら、Asset 移動後の path を Inspector で自動修復します。
- GUID が失われても保存済み path が有効なら GUID を補います。
- 現在の active Build Profile の実効 Scene 一覧に未登録または無効なら警告します。
- 同名 Scene の取り違えを避けるため、短い Scene 名では読み込みません。

警告が出た Scene は、Build Profiles の現在の Scene Listへ追加して有効にしてください。サンプルは明示的な setup menu を実行した場合だけ不足 Scene を追加します。

## 状態と結果

`StatusChanged` は次の状態を通知します。

```text
Idle → Validating → Loading / Unloading / SettingActive → Completed / Failed → Idle
```

自動activation完了後の事後条件を確認するときは `Verifying` も通知します。`SceneFlowStatus.Progress` は0以上1以下で減少せず、成功時に1になります。ただし Unity の `AsyncOperation.progress` を正規化した値であり、残り時間の割合ではありません。

`Finished` は、検証処理へ受理された要求が成功または失敗で確定したときに1回通知します。メインスレッド外からの呼び出し、処理中に届いた2件目、`StatusChanged`または`Finished`のcallback中に再入した要求は、状態を変更せずそれぞれ `MainThreadRequired`、`Busy` の戻り値だけを返すため通知対象外です。次の要求はcallbackから戻った後のframeで開始してください。各購読処理の例外は個別に隔離され、後続購読者、Scene操作の結果、Busy解除を止めません。

## 主な失敗

| `SceneFlowError` | 意味 |
|---|---|
| `InvalidRequest` | Scene参照または操作種別が不正 |
| `MainThreadRequired` | 生成時のメインスレッド以外から呼んだ |
| `Busy` | 同じサービスが別要求を処理中、またはcallbackを通知中 |
| `SceneNotInBuild` | Playerまたは現在のBuild Profileから読み込めない |
| `AlreadyLoaded` | Additive対象が既に読込済み |
| `NotLoaded` | 操作対象が読込済みでない |
| `AmbiguousScene` | 同じ完全pathのSceneが複数あり一意に選べない |
| `LastSceneCannotBeUnloaded` | 最後の読込済みSceneをUnloadしようとした |
| `ActiveSceneCannotBeUnloaded` | active Sceneを直接Unloadしようとした |
| `ActivationFailed` | active Sceneの切替に失敗した |
| `ExternalSceneChange` | 完了後の実Scene状態が要求と一致しない |
| `ApplicationExiting` | Play終了またはアプリ終了で待機を終えた |
| `OperationFailed` | UnityのScene操作を開始または完了できなかった |

active Scene をアンロードする場合は、先に別の読込済み Scene を `SetActiveAsync` してください。最後の1 Sceneはアンロードできません。

## v1の境界

- Unity の Scene load 自体は取り消せないため、偽の cancellation API は提供しません。
- `allowSceneActivation = false` は Unity の非同期操作キュー全体を停止させるため使用せず、手動 activation 待機も提供しません。
- fade、ロード画面、音声、入力、ネットワーク同期、Addressables は利用側の責務です。
- Play Mode終了やアプリ終了では待機を `ApplicationExiting` に畳みます。Domain Reloadでmanaged object自体が破棄される場合、terminal callbackは保証しません。
- `UnloadAsync` は関連 Asset の解放まで行いません。必要な時機で `Resources.UnloadUnusedAssets` を利用側が呼びます。

## サンプル

Package Managerから **Scene Flow Basics** をImportし、同梱READMEのsetup手順を実行してください。Bootstrap、Target A、Target Bの3 Sceneで4操作と状態通知を確認できます。

利用条件は [LICENSE.md](LICENSE.md)、同梱物と外部依存は [Third-Party Notices.txt](Third-Party%20Notices.txt) を参照してください。
