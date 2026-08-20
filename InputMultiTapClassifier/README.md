# Input Multi Tap Classifier

利用側が渡すtap edgeを明示simulation tickのgap windowへ集約し、single・double・triple等の有界burstへ決定論的に分類します。

## 導入

~~~text
https://github.com/mynameisGaku/UnityModules.git?path=/InputMultiTapClassifier#input-multi-tap-classifier-v1.0.0
~~~

## 基本

~~~csharp
using InputMultiTapping;

InputMultiTapClassifier.TryCreate(3, 3, 100, out var classifier, out var error);
classifier.TrySample(100, true, out var first, out error);   // pending 1
classifier.TrySample(102, true, out var second, out error);  // pending 2
classifier.TrySample(106, false, out var completed, out error); // double tap
~~~

## 契約

- 最初のtapはpending burstを開始する
- gapはinclusiveで、最後のtapからmaximumGapTicks後のtickまでは同じburstへ含める
- tapの無いsampleでも、tickがdeadlineを超えた時にGapExpiredで確定する
- deadlineを超えたtickのtapは古いburstを確定し、同時に新しいburstを開始する
- maximumTapCountへ達したburstはMaximumReachedで即時確定する
- Snapshotは状態を進めずtap・確定eventを再発行しない
- 逆行tickは状態を変えずTickMovedBackwardを返す

## 境界

本moduleはInput System、Unity時刻、raw pressed状態、command IDを読みません。tap edge生成、press分類、command sequence、effect callback、network同期、設定保存は利用側の責務です。

## Sample

gap満了によるdouble tap確定と、最大数到達によるtriple tap即時確定を確認できます。

## License

MIT License。詳細はLICENSE.mdを参照してください。
