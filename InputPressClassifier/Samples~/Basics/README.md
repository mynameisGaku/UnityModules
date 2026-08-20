# Input Press Classifier Basics

Sceneを開くと、hold閾値3 tickでtapとholdを実Buttonから確認できます。

- Tap Press @100: 押下edge開始
- Tap Release @102: duration 2でtap分類
- Hold Press @103: 新しい押下edge開始
- Hold Check @106: duration 3でhold開始
- Hold Release @108: duration 5でhold完了

5 Buttonは960×600で1列、640×360で3+2列に収まります。
