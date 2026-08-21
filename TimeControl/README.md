# ゲーム時間制御（TimeControl）

## 30秒で分かる

Time Controlは、ゲーム全体の`Time.timeScale`を1つのScene-owned Controllerで管理します。複数の利用者は相対倍率をleaseとして取得し、有効なleaseのうち最も小さい倍率が基準値へ掛けられます。pauseは0、slow motionは0より大きく1未満、単独のfast-forwardは1より大きい倍率として同じ仕組みで共存します。

Pause menu、演出、デバッグ倍速がそれぞれ `Time.timeScale` を直接書き換えて競合する問題を、破棄可能な lease へ置き換えます。

## こんなときに使う

- Pause と slow motion が同時に要求される可能性がある。
- 一時停止を解除した機能が、別機能の一時停止まで解除する事故を防ぎたい。
- Scene 終了や owner 破棄時に元の時間倍率へ確実に戻したい。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

Unity組込みのUI Toolkit module以外のパッケージ依存、global singleton、期限付きhit-stop、`Time.fixedDeltaTime`の変更はありません。Runtimeの時間制御API自体はUI要素を公開せず、UI Toolkitは同梱Basics sampleの表示に使います。

## インストール

Package Managerの **Add package from git URL** に固定タグ付きURLを指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/TimeControl#time-control-v1.0.0
```

利用側にasmdefがある場合は `TimeControl.Runtime` を参照します。フォルダーを直接管理する場合だけ、`TimeControl/`を`Assets/Modules/TimeControl/`へ配置してください。

## 所有と配置

`Time.timeScale`を所有するSceneまたはゲーム進行ownerが、`TimeControlController`を1つ配置します。Controllerは有効化時に現在の`Time.timeScale`を基準値として取得し、健全に制御している間だけlease倍率を反映します。2つ目のControllerは所有権を奪いません。先のownerが終了した後は、そのControllerへの次の`TryAcquire`から新しい所有期間を開始できます。

このモジュールはglobal singletonや自動生成GameObjectを作りません。Sceneをまたぐ寿命が必要な場合は、利用側がControllerを持つGameObjectの寿命を管理してください。

## 基本動作

- `TryAcquire(multiplier, out lease, out error)`で相対倍率のleaseを取得します。
- 倍率は有限の0以上100以下です。反映後の`Time.timeScale`も100以下でなければなりません。
- 有効なleaseが複数ある場合は最小倍率を使います。0のpauseが最優先です。
- 最後のleaseを破棄すると、取得時の基準値へ戻ります。
- leaseの`Dispose`は複数回呼んでも安全です。終了済みControllerに属する古いleaseは新しい制御へ影響しません。
- 外部コードが制御中の`Time.timeScale`を書き換えた場合は競合として制御を停止し、外部が設定した値を保持します。
- Controllerの無効化または破棄では、競合がない限り基準値を復元し、全leaseを無効にします。

```csharp
using TimeControl;
using UnityEngine;

public sealed class PauseOwner : MonoBehaviour
{
    [SerializeField] private TimeControlController _timeControl;
    private TimeScaleLease _pauseLease;

    public void Pause()
    {
        if (!_timeControl.TryAcquire(0f, out var lease, out var error))
        {
            Debug.LogError($"Pauseを開始できません: {error}", this);
            return;
        }

        _pauseLease = lease;
    }

    public void Resume()
    {
        _pauseLease?.Dispose();
        _pauseLease = null;
    }
}
```

取得したownerがleaseを保持し、不要になった時点で`Dispose`してください。`TimeScaleLease`は共有資源の所有権を表すため、GC任せにはしません。

## 通知とthread

`Status`は現在の基準値、実効倍率、実効値、有効lease数、制御可否、最後の失敗理由を表します。`StatusChanged`はUnityのメインスレッドで通知され、通知先の例外はほかの通知先と制御本体から分離されます。

`TryAcquire`はUnityのメインスレッドから呼びます。leaseは別threadから`Dispose`できます。その場合は解放要求だけを登録し、実際の`Time.timeScale`更新と通知を次のメインスレッド更新で行います。通知中の新規取得は`Busy`で失敗し、通知中の`Dispose`は通知終了後に確実に反映されます。

## 競合時の方針

Time Controlは、制御中の`Time.timeScale`が最後に書き込んだ値と異なることを検出するとfail-closedで停止します。これは別systemとの書き合いを継続しないためです。競合値は上書きせず、既存leaseを無効にします。`Status`と`StatusChanged`から失敗理由を確認し、`Time.timeScale`のwriterを1つへ整理してからControllerを有効化し直してください。

## v1の境界

- timer、cooldown、期限付きhit-stop、leaseの自動失効は行いません。
- `Time.fixedDeltaTime`を取得または変更しません。
- input map、EventSystem、音声、Animator、物理設定を切り替えません。
- network同期や永続化を行いません。
- Scene FlowまたはScreen Transitionへ依存せず、自動連携しません。
- global singleton、常駐Manager、自動生成GameObjectを作りません。
- 全利用者のleaseを外部から一括解放する公開APIは提供しません。各ownerが自分のleaseを破棄します。

## サンプル

Package Managerから **Time Control Basics** をImportし、同梱Sceneを開いてPlayします。Pause、Slow、Fastを個別に追加でき、Nested Demoでは「2 → 0.25 → 0 → 0.25 → 2 → 基準値」の順を非スケール時間で確認できます。スケール時間と非スケール時間の2本のlaneが、pause中のUI応答と時間差を可視化します。ImportだけではProject Settingsや現在のSceneを変更しません。

利用条件は [LICENSE.md](LICENSE.md)、同梱物と外部依存は [Third-Party Notices.txt](Third-Party%20Notices.txt) を参照してください。
