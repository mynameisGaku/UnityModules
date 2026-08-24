# プレイヤー設定（Player Options）

## 30秒で分かる説明

Player Optionsは、application全体のmaster volume、quality、resolution、window mode、preferred refresh rate、target frame rateを1つの型付きstateとして扱うRuntimeモジュールです。schema付きdocumentをPlayerPrefsへ保存できますが、設定の読み込み、memory上の変更、Unityへの反映、保存は利用側がそれぞれ明示して実行します。

自動起動するsingletonやEditor menuはありません。applicationの起動処理など、寿命が明確な1つのownerが`PlayerOptionsService`を保持し、いつ読み込み、いつ反映し、いつ保存するかを決めます。

## できること

- `Load`、`SetState`、`Apply`、`Save`を分け、意図しない反映や保存を避ける。
- master volumeを0から1、target frame rateを`-1`または正の値として型付きstateへ保持する。
- width、height、`FullScreenMode`、`RefreshRate`を1つのdisplay requestとして保持する。
- qualityのnameとindexを組で保存し、buildごとのquality並び替えを検出する。
- 未保存時はdefaultsを使用し、保存fileを自動作成しない。
- 破損documentと未来schemaを失敗として報告し、保存済みraw dataを読み込みだけで上書きしない。
- state変化を`StateChanged`で通知し、adjustment、fallback、遅延反映をwarning flagsで区別する。
- `IPlayerOptionsStorage`を差し替え、同じservice contractをtestやproject固有storageで使う。

## 使わない方がよい場合

ゲーム進行、複数save slot、backup、checksum、暗号化、cloud同期、account間同期を保存したい場合には向きません。PlayerPrefsの耐久性を超える保存が必要なら、その責務を持つstorageやSaveSystemを利用してください。

key binding、interactive rebinding、Input Systemのbinding override、言語選択、画面明るさ、AudioMixer group別volumeも1.0.0には含みません。resolutionやframe pacingの反映完了を同じframeで保証したい用途にも向きません。

## 導入

Unity 6000.5.7f1以降を使用します。Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/PlayerOptions#player-options-v1.0.0
```

利用側にasmdefがある場合は`PlayerOptions.Runtime`を参照します。フォルダーを直接管理する場合だけ、`PlayerOptions/`を`Assets/Modules/PlayerOptions/`へ配置してください。

このpackageに`Tools` menuはありません。Package ManagerのSamplesから`Player Options Basics`をimportし、`PlayerOptionsBasics.unity`を開くのが最短の入口です。

| package dependency | version | 用途 |
| --- | --- | --- |
| `com.unity.modules.audio` | 1.0.0 | `AudioListener.volume`によるmaster volume反映 |
| `com.unity.modules.jsonserialize` | 1.0.0 | schema付きdocumentのJSON serialize / deserialize |
| `com.unity.modules.uielements` | 1.0.0 | 同梱Basics sampleのUI Toolkit画面 |

## 3分で試す

1. Package Managerから`Player Options Basics`をimportします。
2. `PlayerOptionsBasics.unity`を開いてPlayします。`PlayerOptionsBasicsController`がserviceを所有し、起動時に`Load`、成功時だけ続けて`Apply`します。
3. Sample UIでstateを変更し、memory上の値だけが変わる`SetState`と、Unityへrequestを送る`Apply`を別々に確認します。
4. `Save`後に`Load`し、保存されたstateとresultのwarning、field masks、`UsedDefaults`、`WasAdjusted`、`RequiresSave`を確認します。
5. 自分のapplicationでは起動ownerがserviceを1つ生成し、成功した`Load`の後に必要なタイミングで`Apply`します。

```csharp
using PlayerOptions;
using UnityEngine;

public sealed class ApplicationOptionsOwner : MonoBehaviour
{
    private PlayerOptionsService _service;

    private void Awake()
    {
        _service = PlayerOptionsService.CreateDefault();

        PlayerOptionsResult load = _service.Load();
        if (!load.IsSuccess)
        {
            Debug.LogError(load.Message);
            return;
        }

        PlayerOptionsResult apply = _service.Apply();
        if (!apply.IsSuccess)
        {
            Debug.LogError(apply.Message);
        }
    }
}
```

破損または未来schemaの`Load`は失敗です。上の例はそのstateを勝手に反映せず、利用側UIやreset確認へ判断を戻します。

## 4つの操作

| 操作 | 変更するもの | 変更しないもの |
| --- | --- | --- |
| `Load()` | 読み取れた互換documentからserviceの`State`を更新する | Unity runtime、PlayerPrefsのraw data |
| `SetState(state)` | 検証に成功したmemory上の`State`を更新する | Unity runtime、PlayerPrefs |
| `Apply()` | 現在の`State`をUnity runtimeへ明示的に反映する | memory上のstate、PlayerPrefs |
| `Save()` | 現在の`State`をschema付きdocumentとしてstorageへ書く | Unity runtime |

`Load`や`SetState`が暗黙に`Apply`または`Save`を呼ぶことはありません。`Apply`も保存しません。UIでApplyとSaveを同じbuttonにする場合でも、利用側が2つの結果を別々に確認してください。

`Save`は書き込む直前にcurrent runtimeへ`State`をstrictに再検証します。display hot-plug、quality設定変更などで以前は有効だったstateが利用できなくなった場合、`InvalidOptions`または`RuntimeUnavailable`で失敗し、storageへ書きません。

## 保存と保全

標準backendはkey `com.studiogaku.player-options.document`へJSON documentを保存します。`PlayerPrefsPlayerOptionsStorage.Write`は`PlayerPrefs.SetString`の後に`PlayerPrefs.Save()`を同期呼出しします。`Save`成功が示すのはserializeとその呼出しが例外なく完了したことまでです。

PlayerPrefsはtransactional databaseではありません。OS crash、process強制終了、媒体障害、platform quota、atomic replacement、backup、checksum、複数端末同期を保証しません。重要なgame進行や購入情報を保存しないでください。

未保存keyの`Load`は成功し、`Defaults`を返しますが自動保存しません。破損documentまたは不正なmigration結果は`CorruptData`、現在より新しいschemaは`UnsupportedSchemaVersion`、migration処理の例外は`MigrationFailed`となり、serviceの現在stateと保存raw dataを変えません。修復やdowngradeを推測せず、利用者が明示的にresetまたは別versionで処理するまで保全します。1.0.0のcurrent schemaはversion 1で、実在する旧schema migrationはありません。

## qualityのnameとindex

`PlayerQualityOptions`は`LevelName`と`LevelIndex`を組で保持します。現在の`QualitySettings.names`で同じindexに同じnameがOrdinal一致し、そのnameが一覧内に1件だけ存在する場合が正常です。constructor defaultsと`SetState`にもこの一意性が必要です。

互換documentの`Load`に限り、組がずれた場合はOrdinal一致するnameを全件検索します。一意に見つかればindexを修復して`QualityIndexAdjusted`を返します。見つからない、または同名が複数ある場合は`Defaults.Quality`へfallbackし、`QualityFallbackUsed`を返します。adjustされた結果は`RequiresSave=true`ですが、`Load`自身は保存しません。

`SetState`はこの推測を行いません。現在のquality nameとindexが完全一致しない、または同じnameが複数存在するstateは`InvalidOptions`です。fallback先のdefault nameが一意でない場合は`RuntimeUnavailable`です。

## resolution、vSync、frame rate

`Apply`はdisplayの観測値とrequestが異なる場合だけ`Screen.SetResolution`を呼びます。Unityがrequestを受理しても、windowとdisplayの変更は後続frameで完了することがあります。その場合は`ResolutionChangeDeferred`を返します。同じframeの`Screen.width`などを、適用完了の証拠として扱わないでください。

preferred refresh rateは`RefreshRate`のnumerator / denominatorで保持し、`0/0`は指定なしです。正の比率は最大公約数で約分される場合があり、`RefreshRateNormalized`で通知します。platform、display、window modeがrequestを採用することまでは保証しません。

`ExclusiveFullScreen`だけはwidth、height、指定refresh rateが現在の`Screen.resolutions`に存在する必要があります。`FullScreenWindow`、`MaximizedWindow`、`Windowed`は正のwidth / heightであれば、一覧にないwindow sizeもrequestできます。いずれもOSやdisplayが最終値を採用する保証ではありません。

このmoduleは`QualitySettings.vSyncCount`と`OnDemandRendering.renderFrameInterval`を変更しません。正のtarget frame rateをrequestしても、vSyncやrender intervalが優先する場合は`TargetFrameRateMayBeOverridden`を返します。deviceのthermal control、driver、OS compositorを含む実効fpsは保証しません。

## Applyとrollbackの境界

`Apply`はquality、`Application.targetFrameRate`、`AudioListener.volume`、resolution requestの順に処理します。`AffectedFields`は今回setterまたはresolution requestの呼出しを開始したfieldであり、成功や値変更を保証しません。同期的な失敗やreadback不一致では、変更を開始したquality、frame rate、volumeを逆順にbest-effortで戻します。rollbackが完了すれば`ApplyFailed`、rollback自体が失敗すれば`RollbackFailed`で、復元できなかった同期fieldは`RollbackFailedFields`に残ります。

`Screen.SetResolution`が正常に戻った場合は`ResolutionChangeDeferred`です。callがthrowした場合、同期fieldはrollbackしますがdisplay requestの副作用有無を判定できないため、失敗resultへ`ResolutionOutcomeUnknown`と`OutcomeUnknownFields=Display`を返します。displayのrollbackや同frame完了を主張しないので、後続frameでapplication自身が観測してください。

## application owner

`PlayerOptionsService`はstatic singleton、`DontDestroyOnLoad` object、自動Scene探索を作りません。application bootstrap、root lifetime scope、または同等の明示ownerが1 instanceを持ち、複数UIはそのinstanceを共有してください。複数serviceが同じPlayerPrefs keyやUnity global settingへ同時に書く順序は調停しません。

main thread identityは`RuntimeInitializeLoadType.SubsystemRegistration`でbindされます。同じphase内のcallback順は保証されないため、service生成とpublic操作をサポートする最早phaseは`BeforeSceneLoad`です。bind前またはmain thread外での`CreateDefault` / constructorは`InvalidOperationException`を送出します。生成済みserviceのpublic操作はUnity main threadで直列に呼び、main thread外は`MainThreadRequired`、operationの重複や再入は`Busy`として報告します。

## 公開API

- `PlayerOptionsService` — `Defaults`、`State`、`StateChanged`と、`CreateDefault`、`Load`、`SetState`、`Apply`、`Save`を提供します。
- `PlayerOptionsState` — `Display`、`TargetFrameRate`、`MasterVolume`、`Quality`を持つreadonly stateです。
- `PlayerDisplayOptions` — width、height、`FullScreenMode`、preferred `RefreshRate`を持ちます。
- `PlayerQualityOptions` — qualityのindexとOrdinal nameを持ちます。
- `PlayerOptionsResult` — success、error、message、state、warning flags、defaults使用、adjustment、再保存要否と、affected / rollback failed / outcome unknown field masksを返します。
- `PlayerOptionsField` — `Display`、`TargetFrameRate`、`MasterVolume`、`Quality`を識別するflagsです。
- `PlayerOptionsError` — validation、schema migration、storage、serialization、apply、rollback、runtime、thread、busyの失敗理由です。
- `PlayerOptionsWarning` — fallback、normalization、override可能性、resolution遅延または結果不明を表すflagsです。
- `IPlayerOptionsStorage` — documentのread / write境界です。
- `PlayerPrefsPlayerOptionsStorage` — 明示keyまたは標準keyでPlayerPrefsを使うstorageです。

`StateChanged`は`Action<PlayerOptionsState>`です。stateが変わる前に購読し、ownerの終了時に購読解除してください。

## 1.0.0に含まないもの

- key binding、Input System binding override、rebind UI。
- language、subtitle、accessibility、brightness、HDR calibration。
- AudioMixer group別volume、mute、device選択、音声再生管理。
- backup、checksum、暗号化、cloud、account同期、複数profile、複数save slot。
- resolution変更完了event、display hot-plug調停、実効fps計測。
- singleton、service locator、Editor menu、自動起動、自動保存。

詳細なvalidationとresult contractは[Documentation](Documentation~/index.md)を参照してください。

本packageは[MIT License](LICENSE.md)です。外部依存は[Third-Party Notices.txt](Third-Party%20Notices.txt)を参照してください。
