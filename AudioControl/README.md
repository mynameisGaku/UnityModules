# 音声再生管理（AudioControl）

## 30秒で分かる

Audio Controlは、Sceneまたはgame全体が明示的に所有する`AudioControlController`から、複数のAudioClip再生をhandle単位で管理する小さな音声制御モジュールです。Controllerは専用AudioSource poolだけを作成し、voice上限時はpriorityと開始順でsteal対象を決定します。

効果音ごとに AudioSource を増やす作業、同時再生数の暴走、古い音の停止、Pause 中の fade を一つの Controller へまとめます。

## こんなときに使う

- 短い効果音を多数再生するための AudioSource pool が欲しい。
- 重要な音を残し、優先度の低い古い音だけを止めたい。
- 再生を開始した owner が、その音だけを後から停止したい。

Scene Flow、Screen Transition、Time Control、Input Gateとは依存せず、利用側ownerが必要な期間だけ`AudioControlHandle`を保持して組み合わせます。global singleton、自動永続化、`DontDestroyOnLoad`は作りません。

## 導入

Unity 6000.5.7f1以降を使用します。Package ManagerのGit URLには次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/AudioControl#audio-control-v1.0.0
```

利用側にasmdefがある場合は`AudioControl.Runtime`を参照します。フォルダーを直接管理する場合だけ、`AudioControl/`を`Assets/Modules/AudioControl/`へ配置してください。

## 最小設定

1. 音声の寿命を所有するGameObjectへ`AudioControlController`を追加します。
2. InspectorのVoice Limitを1から32の範囲で設定します。
3. `TryPlay`へAudioClipと`AudioPlayRequest`を渡し、成功時のhandleを利用側ownerが保持します。
4. その音だけを止めるときはhandleを`Dispose`するか、fadeが必要ならControllerの`Stop`を呼びます。

```csharp
var request = new AudioPlayRequest(
    volume: 0.8f,
    pitch: 1f,
    loop: true,
    fadeInSeconds: 0.25f,
    priority: 80,
    allowSteal: true);

if (audioControl.TryPlay(loopClip, request, out var handle, out var error))
{
    // ownerの終了時は handle.Dispose();
    // 0.2秒で止める場合は audioControl.Stop(handle, 0.2f);
}
```

## voice上限とsteal

priorityはUnityのAudioSourceと同じく0が最高、255が最低です。空きvoiceがない場合、`AllowSteal`がtrueで、新しい要求が現在の最も低いpriorityと同等以上のときだけstealします。候補が複数ある場合は最古の開始を選ぶため、結果は呼出順から決定できます。

steal、自然終了、Controller無効化、handle解放のいずれでも古いhandleは即座にinactiveとなります。別Controllerや過去の有効期間に属するhandleを`Stop`へ渡しても、現在のvoiceには影響しません。

## 契約

- Controllerは1から32個の2D AudioSourceを専用childとして所有し、他のAudioSourceを変更しません。
- volumeは0から1、pitchは0.0001から3、fadeは0から60秒、priorityは0から255です。NaNとInfinityを含む不正要求は再生前に拒否します。
- fade-inとfade-outは`Time.unscaledDeltaTime`で進むため、`Time.timeScale == 0`でも完了します。
- `AudioControlHandle.Dispose`は任意スレッドから重複して呼べます。worker解放はhandleを即座にinactiveとし、実AudioSource停止を次のController Updateで行います。
- `TryPlay`、`Stop`、Controller状態参照はUnityメインスレッド向けです。無効なControllerは`ControllerUnavailable`、有効なControllerへのworker操作は`MainThreadRequired`です。
- Controller無効化・破棄・Application終了では全voiceを停止し、現在世代のhandleを無効化します。再有効化後の新しいvoiceを古いhandleが停止することはありません。

## 含まないもの

- AudioClip、Addressables、AssetBundle、streaming resourceの読込。
- AudioMixer group、snapshot、bus volume、ducking、master volume保存。
- 3D spatial設定、位置追従、doppler、reverb、AudioListener管理。
- DSP時刻によるsample精度schedule、音楽拍同期、crossfade playlist。
- network同期、global singleton、service locator、自動Scene連携。

詳しいAPIと失敗理由は[Documentation](Documentation~/index.md)、実操作はPackage Managerの`Audio Control Basics`を参照してください。

利用条件は[LICENSE.md](LICENSE.md)、同梱物と依存は[Third-Party Notices.txt](Third-Party%20Notices.txt)を参照してください。
