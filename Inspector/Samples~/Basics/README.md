# Inspector Basics

収録している属性を 1 つのコンポーネントで見比べるサンプルです。

## 使い方

1. Package Manager から **Inspector Basics** を Import する。
2. `InspectorBasics.unity` を開く。
3. Hierarchy の **Inspector Basics Sample** を選ぶ。
4. Inspector を見ながら、次を試す。

| 操作 | 見るところ |
|---|---|
| `Use Override` を切り替える | `[ShowIf]` の欄が消え、`[EnableIf]` の欄が灰色になる |
| `Mode` を Advanced にする | `[ShowIf]` に値を渡した欄（カーブ）が出る |
| `設定` のタブを切り替える | `[TabGroup]`。選んだタブは記憶される |
| `上級者向け` を折りたたむ | `[Foldout]`。中の 2 つは `[HorizontalGroup]` で横並び |
| `Projectile` を空にする | `[Required]` が赤く出る |
| `Projectile` にシーン上のオブジェクトを入れる | `[AssetOnly]` が指摘する |
| `Texture Size` を 300 にする | `[ValidateInput]` が出る |
| `Max Hp` を 0 にする | `[MinValue]` が 1 に丸め直す |
| `Notes` を改行する | `[ResizableTextArea]` の欄が伸びる |
| `Id With Button` の「生成」を押す | `[InlineButton]`。`Ctrl+Z` で取り消せる |
| Play Mode に入る | `[Button(EnableMode = PlayMode)]` が押せるようになり、`[ShowNonSerialized]` の値が動く |

## 補足

このサンプルは `Inspector.Runtime` だけを参照しています。属性を付けるのに
エディタ側のアセンブリを参照する必要はありません。
