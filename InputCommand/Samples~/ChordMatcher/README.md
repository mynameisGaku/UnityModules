# Input Chord Matcher Basics

Sceneを開くと、required command 1・2・3の押下edgeが明示tickの最大span 2以内に揃うかを実Buttonで確認できます。

- Guard 1 @100: required 1 / 3
- Light 2 @101: required 2 / 3
- Heavy 3 @102 · Match: complete、span 2、trigger
- Release Guard @103: incompleteへ戻して再arm
- Guard 1 @106 · Late: held commandとの差がspan 5となり拒否

5 Buttonは960×600で1列、640×360で3+2列に収まります。
