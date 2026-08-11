# Debug Menu Basics

2 つの最上位ページと主要な行を、自動登録だけで確認するサンプルです。

## 使い方

1. Package Manager から **Debug Menu Basics** を Import する。
2. **Tools > Debug Menu > Add To Scene** を実行する。
3. Play Mode に入り、`F1` でメニューを開く。
4. `[` / `]` またはヘッダーの左右ボタンで **Player** と **Diagnostics** を切り替える。

Player では Bool、範囲付き Float、HSV・アルファ対応 Color、Watch、子ページを確認できます。子ページからはヘッダーの戻るボタンでも親へ戻れます。`F2` は Invincible のショートカットです。Diagnostics では Watch と Graph を確認できます。

数値スライダーとカラーピッカーはマウスドラッグに対応します。Float は `Enter` または値欄のダブルクリック、Color は値欄のダブルクリックで直接入力できます。

登録メソッドは IL2CPP のコード除去に備えて `[Preserve]` を付けています。実際のプロジェクトでも `[DebugMenuRegister]` と組み合わせてください。
