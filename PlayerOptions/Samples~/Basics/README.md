# Player Options Basics

`PlayerOptionsBasics.unity`を開いてPlayしてください。`PlayerOptionsBasicsController`がapplication ownerとして`PlayerOptionsService`を1つ生成し、起動時に`Load`を実行して、成功した場合だけ続けて`Apply`します。

画面上の操作は分離されています。

- `Load` — storageからmemory上の`State`へ読み込みます。Unityへ反映も保存も行いません。
- `Set State` — input値をstrictに検証し、memory上の`State`だけを更新します。
- `Apply` — 現在の`State`をquality、target frame rate、master volume、display requestの順にUnityへ反映します。
- `Save` — 現在の`State`をPlayerPrefsへ保存します。Unityへ反映しません。

下部のstatus cardは`UsedDefaults`、`WasAdjusted`、`RequiresSave`、warning flags、error、`AffectedFields`、`RollbackFailedFields`、`OutcomeUnknownFields`と現在stateを表示します。`ResolutionChangeDeferred`が表示された場合、resolution requestの完了は後続frameで確認してください。`ResolutionOutcomeUnknown`と`OutcomeUnknownFields=Display`はSetResolutionの副作用有無を判定できない失敗です。`TargetFrameRateMayBeOverridden`はvSyncまたはrender intervalがtarget frame rateより優先し得ることを示します。

`Save`を押すまではPlayerPrefsへ書きません。標準keyは`com.studiogaku.player-options.document`です。sample終了時に適用済みUnity global settingや保存値を自動で戻さないため、実projectでは明示的なapplication ownerが終了・reset方針を決めてください。
