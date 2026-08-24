# Input Sequence Matcher Basics

Sceneを開くと、正のcommand id列を明示simulation tickで照合する流れを実Buttonで確認できます。

- `Light @100 · 1/3`: pattern先頭のcommand id 1を照合
- `Light @101 · 2/3`: 最大間隔2 tick内で2番目のcommand id 1を照合
- `Heavy @102 · Match`: command id 2でLight・Light・Heavyを完成し、進捗を0へ戻す
- `Light @103 · Restart`: 次のsequenceを進捗1から開始
- `Late Light @106`: 前回一致から3 tick後なのでtimeoutし、現在のLightから進捗1へrestart

Runtime moduleはUI Toolkitへ依存しません。UI Toolkitはこのサンプル表示だけに使います。
