# Time Control Basics

Pause・Slow・Fastのleaseを操作し、複数要求では最小倍率が選ばれることを確認するUI Toolkitサンプルです。スケール時間と非スケール時間のlaneを並べ、pause中もUIと非スケール側が動くことを可視化します。

## 開き方

1. Package Managerから **Time Control Basics** をImportします。
2. `TimeControlBasics.unity`を開きます。
3. Play Modeを開始します。
4. Game Viewの **Pause x0**、**Slow x0.25**、**Fast x2**、**Nested Demo**、**Release Owned**を操作します。

各倍率ボタンはsample自身が所有するleaseを1件追加します。**Release Owned**はsampleが取得したleaseだけを破棄し、ほかの利用者のleaseには触れません。

**Nested Demo** は所有中のsample leaseを最初に解放し、非スケール時間で次の順を実行します。

```text
Fast x2
  -> Slow x0.25を追加
  -> Pause x0を追加
  -> Pauseを解放してx0.25
  -> Slowを解放してx2
  -> Fastを解放して基準値
```

## 画面の読み方

- Statusは基準値、選択中の倍率、実効値、有効lease数、制御可否、失敗理由を表示します。
- Stageは最後の手動操作またはNested Demoの段階を表示します。
- Scaled Laneは`Time.deltaTime`、Unscaled Laneは`Time.unscaledDeltaTime`を累積します。
- pause中はScaled Laneが停止し、Unscaled LaneとButton操作は継続します。

## 構成

- `TimeControlBasicsPanelSettings.asset`がUI Toolkit panelの描画設定を所有します。
- Sceneの同じGameObjectに`UIDocument`、`TimeControlController`、`TimeControlBasicsController`を配置しています。
- `TimeControlController`が`Time.timeScale`の所有と全leaseの集約を担当します。
- `TimeControlBasicsController`はsample画面と、自分が取得したleaseの寿命だけを管理します。

SampleのImportだけではProject Settings、Build Profile、開いているSceneを変更しません。Legacy Input API、UXML、外部画像、外部fontは使用しません。
