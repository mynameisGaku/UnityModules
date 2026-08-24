# Player Options 1.0.0

Player Optionsは、application全体の音量と表示設定を型付きstateとして保持し、読み込み、memory更新、Unity runtimeへの反映、保存を明示的に分けます。導入と最短のSample手順はpackage直下の[README](../README.md)を参照してください。

## 必要環境

- Unity 6000.5.7f1以降。
- Runtime参照: `PlayerOptions.Runtime`。
- Unity組込みAudio module 1.0.0。
- Unity組込みJSON Serialize module 1.0.0。
- 同梱SampleはUnity組込みUI Toolkit module 1.0.0を使用します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/PlayerOptions#player-options-v1.0.0
```

Editor menuはありません。Package Managerから`Player Options Basics`をimportし、`PlayerOptionsBasics.unity`を開いてください。`PlayerOptionsBasicsController`がapplication ownerとしてserviceを生成し、起動時に`Load`、成功時だけ続けて`Apply`します。画面上ではLoad、Set State、Apply、Saveを別々に再実行できます。

## ownership

`PlayerOptionsService`はapplication全体の現在設定を1 instanceで所有するための部品です。static singleton、service locator、`DontDestroyOnLoad` object、自動Scene探索は提供しません。application bootstrapやroot lifetime scopeがserviceを生成し、各画面へ同じinstanceを渡してください。

複数instanceが同じPlayerPrefs key、`QualitySettings`、`Application.targetFrameRate`、`AudioListener.volume`、`Screen`へ書く順序は調停しません。生成、共有、終了のownerをapplication側で1つに定めます。

main thread identityは`RuntimeInitializeLoadType.SubsystemRegistration`でbindされます。同じphase内のcallback順は保証されないため、service生成とpublic操作をサポートする最早phaseは`BeforeSceneLoad`です。bind前またはmain thread外での`CreateDefault` / constructorは`InvalidOperationException`です。生成済みserviceのpublic操作はUnity main threadで直列に呼び、main thread外は`MainThreadRequired`、operation中の再入または重複は`Busy`です。

## state model

| 型 | member | 意味 |
| --- | --- | --- |
| `PlayerOptionsState` | `Display` | resolution request |
|  | `TargetFrameRate` | `-1`または正のtarget frame rate |
|  | `MasterVolume` | 0から1の`AudioListener.volume` request |
|  | `Quality` | quality name/indexのidentity |
| `PlayerDisplayOptions` | `Width`, `Height` | 正のresolution |
|  | `FullScreenMode` | Unityの定義済み`FullScreenMode` |
|  | `PreferredRefreshRate` | Unityの`RefreshRate`。`0/0`は指定なし |
| `PlayerQualityOptions` | `LevelIndex` | 現在のquality listに対するindex |
|  | `LevelName` | `QualitySettings.names`とのOrdinal一致に使うname |

全state型はreadonly valueです。serviceはconstructorで受け取った`Defaults`と現在の`State`を公開し、変更時は`Action<PlayerOptionsState>`の`StateChanged`を通知します。

## serviceの作成

標準backendを使う場合は`CreateDefault`を呼びます。省略時のkeyは`PlayerOptionsService.DefaultStorageKey`、値は`com.studiogaku.player-options.document`です。

```csharp
PlayerOptionsService service = PlayerOptionsService.CreateDefault();
```

明示defaultsとstorageを所有する場合はconstructorを使います。

```csharp
IPlayerOptionsStorage storage =
    new PlayerPrefsPlayerOptionsStorage("com.example.game.player-options");
PlayerOptionsService service = new PlayerOptionsService(defaults, storage);
```

`PlayerPrefsPlayerOptionsStorage.Key`で実際のkeyを確認できます。testやproject固有storageは`IPlayerOptionsStorage.TryRead(out string contents)`と`Write(string contents)`を実装します。

PlayerPrefs keyは空白以外で256文字以下に限定され、違反時はconstructorが`ArgumentException`を送出します。保存documentは.NET `string.Length`で16,384を上限とし、空document、上限超過、必須field欠落、0以下のschema versionは`CorruptData`です。

## 操作の分離

### `Load()`

storageからdocumentを読み、互換性と値を検証します。成功したstateだけをserviceへ設定します。Unity runtimeへ反映せず、storageへ書き戻しません。

### `SetState(PlayerOptionsState)`

呼出し時点のruntimeに対してstateをstrictに検証し、成功時だけmemory上の`State`を変更します。Unity runtimeとstorageは変更しません。fallbackやnearest resolution、quality nameの推測は行いません。

### `Apply()`

現在の`State`をUnity runtimeへ反映します。stateとstorageは変更しません。quality、frame rate、master volume、resolution requestの順で処理します。

### `Save()`

現在の`State`をcurrent runtimeへstrictに再検証し、成功したstateをcurrent schema documentへserializeしてstorageへ渡します。Unity runtimeへ反映しません。display hot-plugやquality設定変更後は`InvalidOptions`または`RuntimeUnavailable`で失敗し、storageへ書かない場合があります。明示的な`Save`だけが既存raw documentを置き換え得ます。

UIがApplyとSaveを1 actionにまとめる場合も、先に`Apply`、次に`Save`を呼び、それぞれの`PlayerOptionsResult`を別々に処理してください。一方だけ成功する可能性があります。

## Load result

| storage内容 | result | state | raw storage | flags |
| --- | --- | --- | --- | --- |
| keyなし | success | `Defaults` | 書かない | `UsedDefaults=true`, `WasAdjusted=false`, `RequiresSave=false` |
| current schemaで完全一致 | success | 読み込んだstate | 書かない | adjustmentなし |
| display fallback | success | `Defaults.Display`を使う | 書かない | defaults/adjust/save要求が全てtrue |
| quality index修復 | success | nameに一致するindexを使う | 書かない | `UsedDefaults=false`, adjust/save要求はtrue |
| quality fallback | success | `Defaults.Quality`を使う | 書かない | defaults/adjust/save要求が全てtrue |
| refresh rate正規化 | success | GCD約分後のrateを使う | 書かない | `UsedDefaults=false`, adjust/save要求はtrue |
| 破損document | `CorruptData` | 現在stateを維持 | 完全保全 | defaults/adjust/save要求なし |
| currentより新しいschema | `UnsupportedSchemaVersion` | 現在stateを維持 | 完全保全 | defaults/adjust/save要求なし |
| 登録済み旧schemaのmigration処理が例外 | `MigrationFailed` | 現在stateを維持 | 完全保全 | defaults/adjust/save要求なし |
| storage例外 | `StorageReadFailed` | 現在stateを維持 | backend次第、serviceは書かない | save要求なし |

複数のadjustmentがある場合、warningとboolean flagsは組み合わせて返ります。`UsedDefaults`はdisplayまたはqualityのfallbackが1つでもあればtrueです。1.0.0が読み書きするcurrent schemaはversion 1で、実在する旧schema migrationはありません。`RequiresSave=true`は「呼出し側が正規化またはfallback後のstateを再保存できる」という情報です。`Load`自身は保存しません。未来schemaを現在versionへdowngradeしたり、破損rawをdefaultsで上書きしたりしません。

## validation

`SetState`では少なくとも次を満たす必要があります。

- widthとheightが正。
- `FullScreenMode`が定義済み。
- preferred refresh rateが`0/0`、またはnumerator / denominatorの両方が正。
- target frame rateが`-1`、または正。
- master volumeがfiniteで0から1。
- quality indexが範囲内で、その位置のnameとOrdinal完全一致し、そのnameが一覧内で一意。
- `ExclusiveFullScreen`はwidth、height、指定refresh rateが`Screen.resolutions`に存在する。他modeは正の任意window sizeをrequestできる。

不正値をclampして成功扱いにはしません。例外は、正のrefresh rate比率を最大公約数で約分するsemantic normalizationだけです。約分した場合は`RefreshRateNormalized`を返します。

## quality identity

qualityはnameとindexの組で識別します。次の条件が完全一致です。

```text
0 <= LevelIndex < QualitySettings.names.Length
QualitySettings.names[LevelIndex] == LevelName  // Ordinal
CountOrdinal(QualitySettings.names, LevelName) == 1
```

互換documentの`Load`で組がずれた場合だけ、保存nameを現在の一覧からOrdinal検索します。一意に見つかればindexを修復し、`QualityIndexAdjusted`を返します。見つからない、または同名が複数ある場合は`Defaults.Quality`へfallbackし、`QualityFallbackUsed`を返します。

fallback先のdefaults自体が現在のruntimeで不正、またはdefault nameが重複しているなら、正常stateを推測できないため`RuntimeUnavailable`です。constructor defaultsにも同じ一意性が必要です。`SetState`はname検索やfallbackを行わず、完全一致しないquality、またはnameが重複するqualityを`InvalidOptions`にします。

## display request

preferred refresh rateはUnityの`RefreshRate`をそのまま使います。`0/0`は指定なしです。正の比率はGCDで正規化される場合がありますが、displayがそのrateを採用する保証ではありません。

`ExclusiveFullScreen`ではwidth、heightと、指定した場合のrefresh rateが現在の`Screen.resolutions`に存在する必要があります。`FullScreenWindow`、`MaximizedWindow`、`Windowed`は正のwidth / heightであれば一覧にないwindow sizeもrequestできます。Loadしたunsupported exclusive requestだけが`Defaults.Display`へfallbackします。

`Apply`は現在のdisplay観測値とrequestが異なる場合だけ`Screen.SetResolution`を呼びます。requestを発行した場合は`ResolutionChangeDeferred`を返します。Unity、OS、window manager、displayによる反映は後続frameで行われ得るため、呼出し直後のwidth、height、mode、refresh rateがrequestと一致することを保証しません。

同じ観測値なら`Screen.SetResolution`を呼ばず、`ResolutionChangeDeferred`も付けません。display hot-plugや完了event、timeout、retryはapplication側の責務です。

## Apply orderとrollback

同期処理は次の順です。

1. `QualitySettings.SetQualityLevel`
2. `Application.targetFrameRate`
3. `AudioListener.volume`
4. 必要な場合だけ`Screen.SetResolution`

quality、frame rate、volumeは変更前の値をcaptureします。`AffectedFields`はsetterまたはresolution requestの呼出しを開始したfieldであり、最終成功や値変更を保証しません。例外またはreadback不一致が発生した場合は、呼出しを開始した同期値を逆順にbest-effortでrollbackします。rollback完了時は`ApplyFailed`、rollbackにも失敗した場合は`RollbackFailed`で、復元setterまたはreadbackに失敗した同期fieldを`RollbackFailedFields`へ返します。

`Screen.SetResolution`が正常に戻れば成功resultに`ResolutionChangeDeferred`を付けます。callがthrowした場合、同期fieldはrollbackしますがdisplay requestの副作用有無を判定できません。そのため失敗resultに`ResolutionOutcomeUnknown`と`OutcomeUnknownFields=Display`を付けます。resolutionのrollbackや同frame完了確認は行いません。

## vSyncと実効fps

`Apply`は`Application.targetFrameRate`を設定しますが、`QualitySettings.vSyncCount`と`OnDemandRendering.renderFrameInterval`を変更しません。正のtargetを設定した後にvSyncが有効、またはrender frame intervalが1より大きい場合は`TargetFrameRateMayBeOverridden`を返します。

warningはrequestの失敗を意味しません。Unity runtimeへ値を設定できても、vSync、platform policy、thermal control、driver、OS compositorなどにより実効fpsが異なる可能性を示します。本moduleはframe timingを計測しません。

## PlayerPrefs storageの境界

`PlayerPrefsPlayerOptionsStorage.Write`は`PlayerPrefs.SetString`に続けて`PlayerPrefs.Save()`を同期呼出しします。`Save` successが意味するのは、serializeとstorage callが例外なく完了したことまでです。

次は保証しません。

- OS crashやprocess強制終了に対する耐久性。
- atomic replacement、transaction、journal、backup、checksum。
- platform quotaを超えた保存。
- 改ざん防止、暗号化、秘密情報の保護。
- cloud、account、複数端末、複数profileの同期。

重要dataには別のstorageを使用してください。custom storageを実装しても、`PlayerOptionsService`自身はbackupやtransactionを追加しません。

## result

`PlayerOptionsResult`は次を返します。

- `IsSuccess`
- `Error`
- `Message`
- `State`
- `Warnings`
- `UsedDefaults`
- `WasAdjusted`
- `RequiresSave`
- `AffectedFields`
- `RollbackFailedFields`
- `OutcomeUnknownFields`

`Warnings`は`PlayerOptionsWarning` flagsです。多くはsuccessの補足ですが、`ResolutionOutcomeUnknown`はApply failureにも残ります。3つのfield maskは`PlayerOptionsField` flagsです。

| field mask | 意味 |
| --- | --- |
| `AffectedFields` | 今回のApplyでsetterまたはresolution requestの呼出しを開始したfield |
| `RollbackFailedFields` | 復元setterまたはreadbackが失敗した同期field |
| `OutcomeUnknownFields` | 副作用が起きたか判定できないfield。1.0.0ではSetResolution throw時の`Display` |

`PlayerOptionsField`は`None=0`、`Display=1<<0`、`TargetFrameRate=1<<1`、`MasterVolume=1<<2`、`Quality=1<<3`です。

| warning | 意味 |
| --- | --- |
| `DisplayFallbackUsed` | loadしたdisplayを現在のruntime向けdefaultsへfallbackした |
| `QualityIndexAdjusted` | quality nameの一意一致からindexを修復した |
| `QualityFallbackUsed` | 保存qualityを解決できずdefaultsへfallbackした |
| `RefreshRateNormalized` | refresh rate比率をGCDで約分した |
| `TargetFrameRateMayBeOverridden` | vSyncまたはrender intervalがtarget frame rateより優先し得る |
| `ResolutionChangeDeferred` | resolution requestを発行し、完了が後続frameになり得る |
| `ResolutionOutcomeUnknown` | SetResolutionがthrowし、display requestの副作用有無を判定できない |

warningはbit flagsとして個別に確認してください。`ResolutionOutcomeUnknown`はfailure resultでも確認が必要です。

## errors

| error | 意味 |
| --- | --- |
| `None` | operation成功 |
| `InvalidOptions` | strict validationに失敗 |
| `CorruptData` | 保存documentを安全に解釈できない |
| `UnsupportedSchemaVersion` | currentより新しいschema |
| `MigrationFailed` | 登録済み旧schemaのmigration処理が例外で停止 |
| `StorageReadFailed` | storage readが例外で失敗 |
| `StorageWriteFailed` | storage writeが例外で失敗 |
| `SerializationFailed` | current stateをdocumentへ変換できない |
| `ApplyFailed` | Unity反映に失敗し、同期rollbackは完了 |
| `RollbackFailed` | Unity反映後のrollbackも完了できない |
| `RuntimeUnavailable` | defaultsやruntime capabilityを安全に解決できない |
| `MainThreadRequired` | Unity main thread外から呼ばれた |
| `Busy` | operation中の再入または重複 |

## 公開API一覧

- `PlayerOptionsService.DefaultStorageKey`
- `PlayerOptionsService.CreateDefault(string storageKey = DefaultStorageKey)`
- `PlayerOptionsService(PlayerOptionsState defaults, IPlayerOptionsStorage storage)`
- `PlayerOptionsService.Defaults`
- `PlayerOptionsService.State`
- `PlayerOptionsService.StateChanged`
- `PlayerOptionsService.Load()`
- `PlayerOptionsService.SetState(PlayerOptionsState state)`
- `PlayerOptionsService.Apply()`
- `PlayerOptionsService.Save()`
- `PlayerOptionsState`
- `PlayerDisplayOptions`
- `PlayerQualityOptions`
- `PlayerOptionsResult`
- `PlayerOptionsField`
- `PlayerOptionsError`
- `PlayerOptionsWarning`
- `IPlayerOptionsStorage`
- `PlayerPrefsPlayerOptionsStorage`

## 非目標

key binding、rebind、Input System integration、language、subtitle、accessibility、brightness、HDR calibration、AudioMixer group別volume、audio device選択、game save slot、backup、checksum、暗号化、cloud同期、account同期、resolution完了event、display hot-plug調停、実効fps計測、Editor menu、自動起動、自動保存は1.0.0の対象外です。

## 検証方針

Runtime testではLoad / Set / Apply / Saveの分離、未保存defaults、future / corrupt / migration failure raw保全、quality name/index一意性、exclusive display validation、refresh normalization、Apply順、field masks、rollback、結果不明warning、SubsystemRegistration / main-thread / busy境界を確認します。Sample testでは実UIDocumentと`PlayerOptionsBasicsController`で別々の操作Button、test所有storage key、自動保存しない境界を確認します。Scene assetからPanelSettings、UXML、controllerへのGUID参照はpackage static gateで別に確認します。

本packageは[MIT License](../LICENSE.md)です。Unity module依存は[Third-Party Notices](../Third-Party%20Notices.txt)を参照してください。
