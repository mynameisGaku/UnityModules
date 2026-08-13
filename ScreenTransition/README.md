# Screen Transition

Screen Transitionは、UI Toolkitの単色オーバーレイを透明から不透明へ変えるCoverと、不透明から透明へ変えるRevealを提供します。表示時間、色、補間方法を要求ごとに指定でき、`Time.timeScale`が0でも非スケール時間で進みます。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

外部パッケージへの依存、Scene読込、入力ロック、音声再生、Addressables連携、global singletonはありません。

## インストール

Package Managerの **Add package from git URL** に固定タグ付きURLを指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ScreenTransition#screen-transition-v1.0.0
```

利用側にasmdefがある場合は `ScreenTransition.Runtime` を参照します。フォルダーを直接管理する場合だけ、`ScreenTransition/`を`Assets/Modules/ScreenTransition/`へ配置してください。

## 所有と配置

画面遷移を使うSceneまたはゲーム全体のUI ownerが、`PanelSettings`、`UIDocument`、Screen TransitionのControllerを所有します。モジュールは利用側のPanel Settings、描画先display、sort orderを書き換えません。ほかのUIより前面へ表示したい場合は、利用側が専用のPanel Settingsと十分に高いsort orderを設定してください。

全画面の保証範囲は、Controllerが所有するUIDocumentのpanelと、そのPanel Settingsが描画するdisplay viewportです。別display、RenderTexture、world-space panel、別Panel Settingsの前後関係までは自動管理しません。

## 基本動作

- Coverは不透明度0から指定色のalphaへ進めます。
- Revealは指定色のalphaから不透明度0へ進めます。
- 処理中の同じownerへの新しい要求はBusyとして拒否し、進行中の要求を上書きしません。
- 0秒の要求は開始値を表示したままにせず、その場で終端値へ確定します。
- 状態通知と完了通知はUnityのメインスレッドで行います。
- 通知先の例外はほかの通知先と遷移本体から分離します。
- Controllerの無効化または破棄では進行中の要求を終了し、オーバーレイを入力対象から外します。

```csharp
using ScreenTransition;
using UnityEngine;

public sealed class ScreenTransitionOwner : MonoBehaviour
{
    [SerializeField] private ScreenTransitionController _transition;

    public async void PlayTransition()
    {
        var cover = await _transition.CoverAsync(Color.black, 0.25f);
        if (!cover.IsSuccess) return;

        // 画面が覆われている間に、利用側が表示内容を切り替える。

        var reveal = await _transition.RevealAsync(Color.black, 0.25f);
        if (!reveal.IsSuccess) Debug.LogError(reveal.Message, this);
    }
}
```

汎用入口の`ExecuteAsync(ScreenTransitionRequest)`も利用できます。要求には`Cover`または`Reveal`、色、0以上3600以下の秒数、`Linear`・`EaseIn`・`EaseOut`・`EaseInOut`の補間方法を指定します。長時間値による進捗停止を避けるため、この範囲外は`InvalidRequest`です。

## Scene Flowとの組み合わせ

Scene Flowへの依存はありません。利用側が「Cover完了 → Scene操作 → Reveal」の順に呼ぶことで組み合わせます。Scene操作が失敗した場合も、画面を覆ったままにするかRevealするかは利用側ownerが決めます。

## v1の境界

- Sceneの読込、切替、Unloadは行いません。
- 入力を止めません。必要なら利用側が入力mapや操作受付を切り替えます。
- 音声、動画、画像、shader、複数色、wipe効果は含みません。
- Addressablesやネットワーク同期は行いません。
- global singletonや常駐Managerを作りません。
- 実行開始後のcancel APIは提供しません。
- 利用側のPanel Settings、UIDocument、sort order、target displayを自動変更しません。

## サンプル

Package Managerから **Screen Transition Basics** をImportし、同梱Sceneを開いてPlayします。明るい背景と状態表示の上でCover、Reveal、自動デモを実行でき、オーバーレイの全面配置と不透明度変化をGame Viewで確認できます。ImportだけではProject Settingsや現在のSceneを変更しません。

利用条件は [LICENSE.md](LICENSE.md)、同梱物と外部依存は [Third-Party Notices.txt](Third-Party%20Notices.txt) を参照してください。
