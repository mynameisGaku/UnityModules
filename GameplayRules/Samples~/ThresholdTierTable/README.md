# Threshold Tier Table Basics

Bronze 0・Silver 100・Gold 300の3段階を設定済みのSceneです。

1. `Query -10`で最初のtier未到達を確認します。
2. `Query 0 · Bronze`でinclusiveなthreshold一致を確認します。
3. `Query 50 · 50%`でBronzeからSilverへの50%を確認します。
4. `Query 250 · 75%`でSilverからGoldへの75%を確認します。
5. `Query 500 · Gold`で次tierの無いterminal状態を確認します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
