# Input Threshold Classifier Basics

Sceneを開くと、release threshold `0.25`、press threshold `0.75`のhysteresisを実Buttonで確認できます。

- `Below press 0.10`: releasedを保持し、edgeはNone
- `Press exact 0.75`: inclusive press境界でPressed edge
- `Hysteresis 0.50`: threshold間でpressedを保持し、edgeはNone
- `Release exact 0.25`: inclusive release境界でReleased edge
- `Reject NaN`: 非有限sampleを拒否してpressed状態を保持

5 Buttonは960×600で1列、640×360で3+2列に収まります。Runtime assemblyはUI Toolkitへ依存しません。
