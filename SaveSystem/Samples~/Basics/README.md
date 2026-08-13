# Save System Basics

保存したコイン数と Play 開始回数が、Play の停止と再開を跨いで残ることを確認するサンプルです。
旧 Input API や追加パッケージは使用しません。

## 使い方

1. Package Manager から **Save System Basics** を Import する。
2. Import 先の `SaveSystemBasics.unity` を開く。
3. Play する。
4. Game View のボタンでコインを増やし、保存、読み込み、削除を試す。
5. Play を止めて再開し、**Play 開始回数**と保存したコイン数が続いていることを確認する。

初回の Play 開始時は `Application.persistentDataPath/SaveSystemBasics/basic.save` を作ります。
2 回目以降は前回の値を読み込み、Play 開始回数を 1 増やして保存します。

## Context Menu から確認する

Hierarchy の **Save System Basics** を選び、コンポーネント右上のメニューから次も実行できます。

| 操作 | 内容 |
|---|---|
| `コインを 100 増やす（未保存）` | 表示中の値だけを変え、保存前後の違いを作る |
| `現在の状態を保存` | `basic` スロットへ同期保存する |
| `保存した状態を読み込む` | 検証済みの保存値を表示へ反映する |
| `サンプルの保存を削除` | 主データ、バックアップ、処理残骸を削除する |

Context Menu は Edit Mode でも実行できます。結果はコンポーネントの **Last Result** と Console に表示されます。

## 安全な失敗

型またはデータ版が一致しない保存を読み込んだ場合、画面にはエラーを出しますが、元の保存ファイルは変更しません。
WebGL Player と tvOS Player では標準の `FileSaveStorage` を作れないため、非対応理由を表示します。
