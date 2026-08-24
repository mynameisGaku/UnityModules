# Numeric Requirement Evaluator Basics

5種類の数値条件setを実Buttonで評価する設定済みSceneです。

1. `All Pass`はAtLeastとAtMostの2条件を両方満たします。
2. `Mixed`は1条件を満たし、1条件を未達のまま明細へ残します。
3. `Tolerance`は1.005と1の絶対差を許容差0.01内として成立させます。
4. `Strict`は5 > 5を不成立として返します。
5. `Invalid`は重複IDを`DuplicateIdentifier`として拒否し、入力配列を変更しません。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
