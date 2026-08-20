# Startup Flow Basics

`StartupFlowBasics.unity` を開いて Play します。

- **Run Success**: `Order → Id` 順に3 stepを完了します。
- **Run Failure**: 2番目で失敗し、3番目を実行しません。
- **Run Slow / Cancel**: 長いstepへ協調cancelを伝えます。
- **Reset**: 結果と実行順表示を初期化します。

画面のphase、step、全体進捗、実行順、最終結果から、利用側の初期化処理を直列化する最小構成を確認できます。
