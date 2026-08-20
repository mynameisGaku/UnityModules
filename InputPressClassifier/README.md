# Input Press Classifier

1つの入力の押下・解放を利用側の明示simulation tickで追跡し、短押しtapと長押しholdをEngine非依存で決定論的に分類します。

Input System、Unity時刻、frame回数、global stateを内部で読まず、callerが現在の押下boolと非減少tickを渡します。

## 導入

Unity 6000.5.7f1以降で、Package Managerから次を追加します。

~~~text
https://github.com/mynameisGaku/UnityModules.git?path=/InputPressClassifier#input-press-classifier-v1.0.0
~~~

## 基本

~~~csharp
using InputPressing;

if (!InputPressClassifier.TryCreate(3, 100, out var press, out var error)) return;

press.TrySample(100, true, out var started, out error);
press.TrySample(102, false, out var tap, out error);
// tap.Tapped == true / tap.PressDurationTicks == 2

press.TrySample(103, true, out _, out error);
press.TrySample(106, true, out var hold, out error);
// hold.HoldStarted == true / hold.IsHolding == true
~~~

## 契約

- HoldThresholdTicksは1以上のulong tick差
- releasedからpressedへ変わったsampleでPressStartedを1回返す
- 押下継続差が閾値以上へ到達したsampleでHoldStartedを1回返す
- 閾値未満で解放するとTapped、閾値以上で解放するとHoldCompleted
- sampleが閾値を飛び越えて解放した場合はHoldStartedとHoldCompletedを同じ結果で返す
- 同一押下を保持してもedge・分類結果を再発行しない
- 現在tickより前の入力は状態を変えずTickMovedBackwardを返す
- Snapshotは現在状態だけを返し、今回限定flagを保持しない

## 境界

Input Press Classifierはinput deviceや時刻を読みません。押下boolの生成、tick尺度、効果実行、保持repeat、複数command chord、順序combo、入力bufferは利用側または各専用moduleの責務です。sample間に発生して消えたedgeは観測できません。

## Sample

Package ManagerからInput Press Classifier Basicsをimportすると、tick 100→102のtapと、tick 103→106→108のhold開始・完了を設定済みSceneから確認できます。

## License

MIT License。詳細はLICENSE.mdを参照してください。
