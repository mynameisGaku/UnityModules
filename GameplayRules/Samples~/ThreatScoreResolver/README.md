# Threat Score Resolver Basics

`ThreatScoreResolverBasics.unity` を開くと、対象別threat scoreへの加算・減算、入力順の複数増減、0下限clamp、未知IDの明示失敗を5つのButtonで確認できます。

- Add: ID 1を10から25へ増加し、ID 2の20を上回る首位にします。
- Reduce: ID 1を30から18へ減少し、ID 2を首位にします。
- Ordered: 3件の増減を入力順に適用し、全stepと最終首位を表示します。
- Clamp: 10へ-50を要求し、実適用量-10・最終0を表示します。
- Invalid: 初期entryにないIDを明示的に拒否します。

sampleは利用方法の確認用で、Runtime assemblyはUI Toolkitへ依存しません。
