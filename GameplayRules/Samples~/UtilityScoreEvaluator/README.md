# Utility Score Evaluator Basics

5種類の候補setを実Buttonで評価する設定済みSceneです。

1. `Highest`は0.9の候補を3候補から採用します。
2. `Weighted`は2factorのweightによりscore 0.75の候補を採用します。
3. `Tie`は同scoreで入力順が先の候補ID 20を維持します。
4. `Lines`は3factorのweighted utilityを入力順で返します。
5. `Invalid`は重複候補IDを`DuplicateCandidateIdentifier`として拒否し、入力配列を変更しません。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
