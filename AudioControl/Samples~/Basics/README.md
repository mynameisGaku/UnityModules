# Audio Control Basics

`AudioControlController`のowner付きvoice、上限、priority steal、非スケールfadeを確認するready-to-openサンプルです。

Sceneを再生し、`Play Tone`または`Play Loop`でhandleを取得します。`Fill Voices`は空きvoiceを低priority loopで満たし、`Fade One`は最後の所有voiceだけを0.2秒で停止します。`Stop All`はサンプルが所有するhandleだけを解放します。

音は実行時に`AudioClip.Create`で生成するため、外部audio assetを必要としません。`AudioControlController`のvoice limitは4に設定済みです。`Time.timeScale`が0でもfadeは`Time.unscaledDeltaTime`で進みます。
