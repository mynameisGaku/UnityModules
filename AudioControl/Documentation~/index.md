# Audio Control 1.0.0

## 目的

Audio Controlは、複数機能が個別にAudioSourceを生成して上限、停止、fadeを競合させる問題を、明示ownerの小さなvoice poolへ集約します。clipの取得方法やAudioMixer方針は利用側へ残します。

## 公開API

公開Runtime型は次の4型だけです。

- `AudioControlController`: pool、割当、steal、fade、lifecycleを所有します。
- `AudioControlHandle`: 1つのvoiceを所有し、`Dispose`でそのvoiceだけを解放します。
- `AudioPlayRequest`: volume、pitch、loop、fade-in、priority、steal許可を保持します。
- `AudioControlError`: 受付失敗理由を表します。

`TryPlay`の成功は、返されたhandleがactiveで、Controllerの`ActiveVoiceCount`に含まれることを意味します。失敗時はhandleがnullで、AudioSource poolを変更しません。

## allocation規則

1. 空きvoiceがあれば最初の空きを使用します。
2. 空きがなければ、数値が最大のpriority、同値なら開始sequenceが最古のvoiceを候補にします。
3. `AllowSteal == false`、または新要求のpriority数値が候補より大きい場合は`VoiceLimitReached`です。
4. それ以外は候補handleをinactiveにして、新要求を同じslotへ割り当てます。

priority 0が最高、255が最低という規則はUnity AudioSourceのpriorityと同じです。Unity側の実voice virtualizationとは別に、このpackageは所有するAudioSource数を制限します。

## fadeと終了

fadeは`Time.unscaledDeltaTime`で更新します。fade-inは0から要求volume、fade-outは現在volumeから0へ進みます。fade-out中に非loop clipが自然終了した場合は、その時点でvoiceを解放します。

非loop AudioSourceの`isPlaying`が停止した場合、自然終了としてhandleを無効化します。loopはhandle解放、明示Stop、steal、またはController lifecycle終了まで保持します。

## threadとlifecycle

`TryPlay`と`Stop`は有効ControllerのUnityメインスレッドから呼びます。`AudioControlHandle.Dispose`と`IsActive`だけはworkerを含む任意スレッドで使用できます。worker DisposeはUnity APIを呼ばずmanaged queueへ積み、次のUpdateでAudioSourceを停止します。

無効化、破棄、Application終了はgenerationを閉じて全handleを無効化します。再有効化は新generationを作るため、stale handleのDisposeは新しいvoiceへ届きません。

## 依存と非目標

RuntimeはUnity組込みAudio moduleだけを使用します。同梱UI Toolkit sampleのためUIElements moduleもmanifestへ明記します。第三者codeや音声assetは同梱しません。

AudioMixer、3D spatial、AudioListener、clip読込、DSP schedule、playlist、bus volume、永続化、singletonはv1の責務外です。

Unity 6のAudioSourceはclipの再生、停止、volume、pitch、priorityを提供します。volumeは0から1、priorityは0を最高・255を最低として扱います。

- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource-volume.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-unscaledDeltaTime.html
