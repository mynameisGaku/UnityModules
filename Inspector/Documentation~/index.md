# Inspector — ドキュメント

Unity 6000.5 以降向けの Inspector 拡張。属性を付けるだけで表示が変わる。外部依存なし、`unsafe` 不使用。

- [なぜこのライブラリなのか](#なぜこのライブラリなのか)
- [導入](#導入)
- [仕組み](#仕組み)
- [属性リファレンス](#属性リファレンス)
- [独自の CustomEditor と併用する](#独自の-customeditor-と併用する)
- [よくある落とし穴](#よくある落とし穴)
- [できないこと](#できないこと)

---

## なぜこのライブラリなのか

Unity の既定の Inspector は、フィールドを**宣言順に、全部、同じ見た目で**出します。
設定が 5 個のうちは問題になりませんが、30 個になると次のことが起きます。

**1. 効いていない設定が見分けられない**

```csharp
[SerializeField] private bool _useCustomGravity;
[SerializeField] private float _gravityScale;      // 上のチェックが外れていると無意味
```

Inspector には両方が同じ濃さで並びます。触った人は `_gravityScale` を変えて、
「変えたのに効かない」と報告してきます。`[ShowIf(nameof(_useCustomGravity))]` を付ければ、
効かないときは出ません。

**2. 関係のある設定が離れる**

フィールドの宣言順はシリアライズの順でもあるため、後から並べ替えると
`[FormerlySerializedAs]` が要るような話になりがちです。
`[Foldout]` と `[Order]` は**保存形式を変えずに表示だけ**を動かします。

**3. 入れ忘れが実行時にしか分からない**

`_projectile` が空のプレハブは、実行して撃つまで誰も気付きません。
`[Required]` を付ければプレハブを開いた時点で赤くなります。

**4. 確認用のボタンのために Editor クラスが増える**

「経路を焼き直す」ボタン 1 個のために `Editor` クラスを 1 ファイル書き、
そのクラスが `DrawDefaultInspector()` を呼ぶだけになっていると、
以降そのコンポーネントでは他のどんな Inspector 拡張も効かなくなります。
`[Button]` はメソッドに 1 行足すだけです。

---

## 導入

`Assets/` 以下にフォルダごと配置し、利用側の asmdef から `Inspector.Runtime` を参照します。

```csharp
using Inspector;
```

`Inspector.Runtime` に入っているのは属性の定義だけです（`Attribute` を継承した小さなクラスの集まり）。
解釈と描画は `Inspector.Editor` 側にあり、ビルドには含まれません。

---

## 仕組み

### 属性は `PropertyAttribute` を継承していない

これは意図的な設計です。Unity は `PropertyAttribute` を継承した属性を見つけると、
対応する `PropertyDrawer` を探して**フィールドの描画そのものを差し替え**ます。
そして 1 つのフィールドに効く `PropertyDrawer` は 1 つだけです。

もし `[ShowIf]` が `PropertyAttribute` だったら、

```csharp
[ShowIf(nameof(_useOverride))]
[SerializeField] private Optional<float> _speed;   // Optional 専用の描画が消える
```

条件を足しただけで `Optional<T>` の見た目が壊れます。
このパッケージの属性は素の `Attribute` なので、Unity の描画解決には一切割り込みません。
`[ShowIf]` は「出すかどうか」だけを決め、実際の描画は `EditorGUILayout.PropertyField` に任せます。
結果として、他パッケージの `PropertyDrawer` と自由に併用できます。

### 属性を使っていない型には触らない

`InspectorEditor` は `[CustomEditor(typeof(Object), true)]` で全ての型を受け持ちますが、
`CreateInspectorGUI()` の中で対象の型を調べ、このパッケージの属性が 1 つも無ければ
Unity の既定のインスペクタをそのまま返します。導入しただけで既存クラスの見た目は変わりません。

なお `[CustomEditor(typeof(Object), true)]` は**最も曖昧な指定**なので、
`Transform` のように専用のエディタを持つ型では常にそちらが優先されます。

### 型ごとに 1 回だけ走査する

メンバーの走査、`[Order]` による並べ替え、グループの木構造の組み立ては、
型が決まれば結果も決まります。`InspectorLayoutCache` が型単位で作り置きし、
2 回目以降は使い回します。Inspector は 1 秒に何十回も描き直されるため、
ここを毎回計算するとフィールドの多いコンポーネントで目に見えて重くなります。

スクリプトを書き換えるとドメインが読み直され、このキャッシュも消えます。
「属性を足したのに反映されない」状態にはなりません。

### 設定を間違えたら、消さずに言う

`nameof` の綴りを間違えたときに黙ってフィールドを消すと、
**消えていること自体に気付けません**。このパッケージは条件が解けなかった場合、
その条件を「成立した」ものとして扱い（＝フィールドは出したまま）、
理由を Inspector の末尾に赤字で出します。

---

## 属性リファレンス

### 条件

条件の書き方は 3 通りあり、`[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]` で共通です。

```csharp
[ShowIf(nameof(_flag))]                                   // bool メンバーが true
[ShowIf(nameof(_mode), Mode.Advanced, Mode.Expert)]       // 値がいずれかと一致
[ShowIf(ConditionOperator.And, nameof(_a), "!" + nameof(_b))]   // 複数条件
```

- 参照先はフィールド・プロパティ・引数なしメソッドのいずれでもよく、private でも基底クラスのものでも引けます。
- メンバー名の先頭に `!` を付けると、そのメンバーの結果だけを反転します。
- 同じ属性を複数付けた場合は**全部成立したときだけ**成立します。
  `[ShowIf(A)] [HideIf(B)]` は「A かつ B でない」になります。
- 値の比較は型の幅を無視します。`int` のフィールドを `3`、`float` のフィールドを `0.5f`、
  enum を `1` と比べられます。

| 属性 | 効果 |
|---|---|
| `[ShowIf]` | 成立している間だけ出す |
| `[HideIf]` | 成立している間だけ隠す |
| `[EnableIf]` | 成立している間だけ編集できる |
| `[DisableIf]` | 成立している間だけ灰色になる |
| `[ReadOnly]` | 常に表示のみ |
| `[ShowInPlayMode]` / `[HideInPlayMode]` | 再生中かどうかで出し分ける |

隠すか灰色にするかは、**その設定がそこにあることを見せたいか**で選びます。
上級者向けの項目は隠し、「今は効いていないが設定済み」の項目は灰色にすると読みやすくなります。

### グループ

```csharp
[Foldout("上級者向け")]                  // 折りたたみ。この階層の見た目を宣言している
[BoxGroup("上級者向け/物理")]            // 枠囲み。所属先はこちら（一番深いパス）
[SerializeField] private float _drag;
```

パスは `/` で入れ子にできます。1 つのメンバーに種類の違うグループ属性を複数付けた場合、
**一番深いパスが所属先**になり、浅いものは途中の階層の見た目を決めるだけの宣言として働きます。
誰も宣言していない階層は折りたたみになります。

| 属性 | 効果 |
|---|---|
| `[Foldout(path)]` | 折りたたみ |
| `[BoxGroup(path)]` | 見出し付きの枠 |
| `[TabGroup(group, tab)]` | タブ。`group` がタブ列、`tab` が 1 枚 |
| `[HorizontalGroup(path)]` | 横一列 |

グループは**そこに入る最初のメンバーがあった位置**に描かれます。
離れた場所に書いたフィールドも 1 つにまとまります。
開閉状態と選択中のタブは型ごとに `EditorPrefs` に記憶されます。

### 装飾

| 属性 | 主な引数 |
|---|---|
| `[Title(title, subtitle)]` | `Line`（下線）、`Bold` |
| `[InfoBox(text, kind)]` | `VisibleIf`（条件表示）、`Placement`（前 / 後） |
| `[HorizontalLine(height, color)]` | `SpaceBefore`、`SpaceAfter` |
| `[Indent(levels)]` | 負の値で外側へ |
| `[Order(order)]` | 小さいほど上。既定 0、同値は宣言順 |

`[Header]` `[Space]` `[Tooltip]` は Unity 標準のものがそのまま効きます。

### 見た目の調整

| 属性 | 補足 |
|---|---|
| `[LabelText(text)]` | `Tooltip` も指定できる。フィールド名は変えないので保存データに影響しない |
| `[HideLabel]` | 横並びで幅を稼ぐときに |
| `[LabelWidth(width)]` | このメンバーの間だけ変わる |
| `[GUIColor(color)]` | 名前付き色、または `(r, g, b, a)` |
| `[Suffix(text)]` | 値の右に単位を添える |
| `[InlineButton(method, label)]` | 複数付けると左から並ぶ |

### 検証

```csharp
[ValidateInput(nameof(IsPowerOfTwo), "2 のべき乗にすること")]
[SerializeField] private int _textureSize = 256;
```

検査メソッドの形は次のいずれかです。

| 形 | 使いどころ |
|---|---|
| `bool Method(T value)` | そのフィールドの値だけを見る |
| `bool Method(T value, out string message)` | 文言を状況で変えたい |
| `bool Method()` | 複数フィールドの整合を見る |

`[Required]` は参照（`null` と破棄済み）、文字列（空白のみ）、配列・リスト（要素 0）を未設定とみなします。

`[MinValue]` / `[MaxValue]` は知らせるだけでなく**値を書き戻して丸めます**。
`int` / `float` のほか `Vector2`・`Vector3`・`Vector4` とその整数版に効き、成分ごとに丸めます。
正しい値が一意に決まるものは、間違った状態のまま保存させないためです。
一方 `[AssetOnly]` / `[SceneObjectOnly]` は知らせるだけです。何を入れるべきかは機械には決められません。

### メンバー

```csharp
[Button("経路を焼き直す")]
private void RebakePath() { ... }

[OnValueChanged(nameof(ApplyRadius))]
[SerializeField] private float _radius;

[ShowNonSerialized] private int _framesSinceHit;
[ShowNativeProperty] public int RemainingAmmo => _magazine - _fired;
```

- `[Button]` は引数なしのメソッドにのみ付きます。選択中の全オブジェクトに対して呼び、
  呼ぶ前に `Undo` へ控えを取ります。`EnableMode` で編集中 / 再生中を限定できます。
- `[OnValueChanged]` は**値が書き戻された後**に呼ばれます。メソッドの中でフィールドを読めば新しい値が入っています。
  `OnValidate` と違い「どのフィールドが変わったか」が分かります。
- `[ShowNonSerialized]` / `[ShowNativeProperty]` は表示だけで、編集はできません。
  `[SerializeField]` を付けて見えるようにすると実行時にしか意味のない値まで保存され、
  シーンやプレハブの差分が毎回汚れます。

### 値の描き方

| 属性 | 対象の型 | 補足 |
|---|---|---|
| `[Dropdown(member)]` | 何でも | `IEnumerable<T>` か `DropdownList<T>` を返すメンバーを指定。候補に無い現在値は `(候補に無い)` として残す |
| `[Tag]` | `string` | |
| `[Layer]` | `int` / `string` | int なら番号、string なら名前 |
| `[SortingLayer]` | `string` / `int` | |
| `[Scene]` | `string` / `int` | Build Settings に登録済みのものだけ |
| `[ProgressBar(label, max)]` | `int` / `float` | `MaxMember` で上限を他のメンバーから取れる。表示専用 |
| `[Expandable]` | `Object` 参照 | 中身をその場で編集。参照が自分自身に戻る場合は開かない |
| `[ResizableTextArea]` | `string` | `MinLines` / `MaxLines` |
| `[FilePath]` / `[FolderPath]` | `string` | `RelativeToProject`（既定 true）、`Extension` |
| `[ShowAssetPreview(w, h)]` | `Object` 参照 | フィールドの下にサムネイル |

`[Dropdown]` は enum には不要です（Unity が既に選択式にします）。
候補が実行時のデータで決まる場合のためのものです。

---

## 独自の CustomEditor と併用する

```csharp
[CustomEditor(typeof(Spawner))]
public sealed class SpawnerEditor : Inspector.Editor.InspectorEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("プレビュー")) Preview();
    }
}
```

継承せず、任意のエディタから直接呼ぶこともできます。

```csharp
public override void OnInspectorGUI()
{
    Inspector.Editor.InspectorGUILayout.Draw(serializedObject, targets);
}
```

`Draw` の中で `Update()` と `ApplyModifiedProperties()` を行うので、呼び出し側で挟む必要はありません。

---

## よくある落とし穴

**複数条件のつもりで 2 つ目に `nameof` を書いてしまう**

```csharp
[ShowIf(nameof(_a), nameof(_b))]   // 「_a の値が文字列 "_b" と等しいか」になる
```

これは値との比較として解釈され、いつまでも成立しません。
このパッケージは bool メンバーが文字列と比較されている場合を検出して Inspector 上に指摘を出します。
正しくは次のとおりです。

```csharp
[ShowIf(ConditionOperator.And, nameof(_a), nameof(_b))]
```

**グループ名の表記ゆれ**

`"表示 / 戦闘"` と `"表示/戦闘"` は同じグループとして扱われます（前後の空白と空の区切りは落とされます）。
一方 `"表示"` と `"ひょうじ"` は当然別です。`const string` に切り出しておくと安全です。

**同じパスに種類の違うグループ属性を付ける**

`[BoxGroup("設定")]` と `[Foldout("設定")]` が混在すると、どちらで描くか決まりません。
先に見つかったほうを使い、Inspector 上に指摘を出します。
ただしタブ列だけは例外で、`[TabGroup]` が要求する構造が優先されます。
折りたたみとして描くと、ぶら下がるタブが素通しになって見た目が壊れるためです。

**`[Button]` を押しても値が変わらない**

メソッドの中で書き換えた値は `EditorUtility.SetDirty` で保存対象になりますが、
`SerializedObject` 側にすでに未適用の編集があると上書きされることがあります。
ボタンの処理は `ApplyModifiedProperties()` の後に走るようにしてあるので、
通常は問題になりません。

---

## できないこと

- **入れ子の `[Serializable]` クラスの中のフィールドには効きません。**
  対象はコンポーネント直下のメンバー（継承したものを含む）までです。
  中途半端に対応させると配列やリストの描画を自前で作り直すことになり、そちらのほうが壊れやすいため、
  いまは範囲を切っています。中のフィールドは Unity の既定どおりに描かれます。
- **複数のオブジェクトを同時に選んでいるときは、条件を最初の 1 つで判定します。**
  値がばらけている場合、検証の表示と数値の丸めは行いません。
- **`[Button]` に引数は取れません。** 引数を入力させる欄を作ると、その値の保存場所が必要になります。
- **`[MinMaxSlider]` と `[SubclassSelector]` は入っていません。** Containers パッケージ側にあります。
