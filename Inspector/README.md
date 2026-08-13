# Inspector

属性を付けるだけで Inspector の見た目を変えるエディタ拡張。**43 個**の属性を 6 カテゴリで収録し、
外部依存はゼロ、`unsafe` も使わない。

対応: **Unity 6000.5 以降** / .NET Standard 2.1 / C# 9

---

## 何のためのライブラリか

Unity の既定の Inspector は「フィールドを宣言順に、全部、同じ見た目で出す」しかできない。
そのため、設定が増えたコンポーネントでは次のことが起きる。

| 起きること | 既定でどうなるか | このパッケージ |
|---|---|---|
| 効いていない設定が並ぶ | 上書きが無効でも全部見えていて、どれが効くのか分からない | `[ShowIf]` `[EnableIf]` |
| 関係のある設定が離れる | 宣言順に縛られ、`[Header]` で区切るのが精一杯 | `[Foldout]` `[BoxGroup]` `[TabGroup]` |
| 入れ忘れが実行時に出る | `NullReferenceException` で初めて気付く | `[Required]` `[ValidateInput]` |
| 手打ちの文字列が壊れる | タグ名・シーン名の綴り違いが実行時まで残る | `[Tag]` `[Scene]` `[Layer]` |
| 確認用のボタンが要る | そのためだけに `Editor` クラスを 1 つ書く | `[Button]` |

`[Header]` `[Space]` `[Tooltip]` `[Range]` `[TextArea]` `[HideInInspector]` など、
**Unity が既に持っているものは再実装していない**。このパッケージは既定の描画をそのまま通すので併用できる。
`[MinMaxSlider]` と `[SubclassSelector]` は姉妹パッケージの **Containers** 側にあるので、こちらには入れていない。

---

## インストール

Package Manager の **Install package from git URL** に、次の固定版 URL を入力する。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/Inspector#inspector-v1.0.0
```

手動で導入する場合は `Inspector` フォルダーを `Assets/` 以下へ配置する。
どちらの方法でも、利用側に asmdef がある場合は `Inspector.Runtime` を参照する。
属性の定義だけなのでビルドサイズへの影響は小さく、エディタ側は自動で有効になる。

```csharp
using Inspector;
```

---

## まず使うもの

### `[ShowIf]` / `[HideIf]` — 効かない設定を隠す

```csharp
[SerializeField] private bool _useOverride;

[ShowIf(nameof(_useOverride))]
[SerializeField] private float _speed;

[ShowIf(nameof(_mode), Mode.Advanced, Mode.Expert)]   // 値がいずれかと一致
[SerializeField] private AnimationCurve _falloff;

[ShowIf(ConditionOperator.And, nameof(_useOverride), "!" + nameof(_locked))]
[SerializeField] private float _acceleration;         // "!" で反転、複数条件は演算子を先頭に
```

参照先はフィールド・プロパティ・引数なしメソッドのどれでもよく、private でも基底クラスのものでも引ける。
**消すのではなく灰色にしたい**なら `[EnableIf]` / `[DisableIf]` / `[ReadOnly]` を使う。

### `[Foldout]` / `[BoxGroup]` / `[TabGroup]` — 並びを整理する

```csharp
[BoxGroup("体力")] [SerializeField] private int _maxHp;
[BoxGroup("体力")] [SerializeField] private float _regenPerSecond;

[TabGroup("設定", "見た目")] [SerializeField] private Color _tint;
[TabGroup("設定", "挙動")]   [SerializeField] private float _friction;

[Foldout("上級者向け")]                       // 「上級者向け」は折りたたみ
[BoxGroup("上級者向け/物理")]                 // その中の「物理」は枠囲み。所属先はこちら
[SerializeField] private float _drag;
```

離れた位置に書いてあっても 1 つにまとまり、**最初のメンバーがあった位置**に描かれる。
開閉状態と選択中のタブは型ごとに記憶される。

### `[Required]` / `[ValidateInput]` — 入れ忘れと不正値を止める

```csharp
[Required("弾のプレハブを入れないと発射できない")]
[SerializeField] private GameObject _projectile;

[ValidateInput(nameof(IsPowerOfTwo), "2 のべき乗にすること")]
[SerializeField] private int _textureSize = 256;

private bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
```

`[MinValue]` / `[MaxValue]` は知らせるだけでなく**その場で値を丸める**。
正しい値が一意に決まるものは、間違った状態のまま保存させない。

### `[Button]` — 確認用の操作をその場に置く

```csharp
[Button("経路を焼き直す")]
private void RebakePath() { ... }

[Button(EnableMode = ButtonEnableMode.PlayMode)]
private void Respawn() { ... }
```

`Editor` クラスを書かずに済む。選択中の全オブジェクトに対して呼ばれ、`Undo` に控えを取るので取り消せる。

### `[Tag]` / `[Scene]` / `[Layer]` / `[Dropdown]` — 手打ちをやめる

```csharp
[Tag]   [SerializeField] private string _targetTag;
[Scene] [SerializeField] private string _nextScene;    // Build Settings に登録済みのものだけ
[Layer] [SerializeField] private int _groundLayer;

[Dropdown(nameof(Sizes))]
[SerializeField] private int _textureSize = 256;
private static readonly int[] Sizes = { 128, 256, 512, 1024 };
```

`[Dropdown]` の候補は実行時に決めてよい。表示名と値を分けたいときは `DropdownList<T>` を返す。

---

## カテゴリ別の一覧

### 条件（表示・編集可否）

| 属性 | 何が起きるか |
|---|---|
| `[ShowIf]` / `[HideIf]` | 条件が成立している間だけ出す / 隠す |
| `[EnableIf]` / `[DisableIf]` | 条件が成立している間だけ編集できる / 灰色になる |
| `[ReadOnly]` | 常に表示のみ。実行時に決まる値の確認に |
| `[ShowInPlayMode]` / `[HideInPlayMode]` | 再生中かどうかで出し分ける |

条件の書き方は 3 通り。bool メンバー、値との一致（複数可）、`ConditionOperator` による複数条件。
`"!" + nameof(x)` で個別に反転できる。

### レイアウト・装飾

| 属性 | 何が起きるか |
|---|---|
| `[Foldout]` | 折りたたみのまとまりに入れる |
| `[BoxGroup]` | 見出し付きの枠で囲む |
| `[TabGroup]` | タブで切り替える |
| `[HorizontalGroup]` | 横一列に並べる |
| `[Title]` | 副題と下線の付いた見出し |
| `[InfoBox]` | 注意書き。`VisibleIf` で条件表示にもできる |
| `[HorizontalLine]` | 区切り線 |
| `[Indent]` | 字下げして従属関係を示す |
| `[Order]` | 宣言順を変えずに表示順だけ変える |

グループのパスは `"上級者向け/物理"` のように `/` で入れ子にできる。

### 見た目の調整

| 属性 | 何が起きるか |
|---|---|
| `[LabelText]` | ラベルの文言を差し替える（フィールド名は変えない） |
| `[HideLabel]` | ラベルを消して値欄を広げる |
| `[LabelWidth]` | ラベル欄の幅をこのメンバーの間だけ変える |
| `[GUIColor]` | 描画色を変える。危険な設定を赤くするなど |
| `[Suffix]` | 値の右に単位を添える |
| `[InlineButton]` | フィールドの真横に小さなボタンを置く |

### 検証

| 属性 | 何が起きるか |
|---|---|
| `[Required]` | 未設定なら赤く知らせる（参照・文字列・配列） |
| `[ValidateInput]` | 自前のメソッドで確かめる |
| `[MinValue]` / `[MaxValue]` | 範囲外の数値を書き戻して丸める |
| `[AssetOnly]` / `[SceneObjectOnly]` | 参照元が違えば知らせる |

### メンバー

| 属性 | 何が起きるか |
|---|---|
| `[Button]` | メソッドをボタンにする |
| `[OnValueChanged]` | 値が変わった直後にメソッドを呼ぶ |
| `[ShowNonSerialized]` | 保存されないフィールドを表示だけする |
| `[ShowNativeProperty]` | プロパティの現在値を表示だけする |

### 値の描き方

| 属性 | 何が起きるか |
|---|---|
| `[Dropdown]` | 決められた候補から選ぶ |
| `[Tag]` / `[Layer]` / `[SortingLayer]` / `[Scene]` | それぞれの選択欄にする |
| `[ProgressBar]` | 数値をバーで見せる。上限は他のメンバーからも取れる |
| `[Expandable]` | ScriptableObject 参照の中身をその場で編集する |
| `[ResizableTextArea]` | 行数に合わせて伸びる複数行入力 |
| `[FilePath]` / `[FolderPath]` | 「参照…」ボタン付き。プロジェクト相対で保存 |
| `[ShowAssetPreview]` | 参照先のサムネイルを出す |

---

## 独自の `CustomEditor` と併用する

このパッケージは `[CustomEditor(typeof(Object), true, isFallback = true)]` という
**最も曖昧な予備 Editor** として入っている。
そのため、専用のエディタを持つ型では常にそちらが優先され、属性は効かなくなる。
その型でも効かせたいなら `InspectorEditor` を継承する。

```csharp
[CustomEditor(typeof(Spawner))]
public sealed class SpawnerEditor : Inspector.Editor.InspectorEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();                          // 属性つきの描画
        if (GUILayout.Button("プレビュー")) Preview();
    }
}
```

`InspectorGUILayout.Draw(serializedObject, targets)` を直接呼んでもよい。

---

## 設計上の約束

- **属性は `PropertyAttribute` を継承しない** — 継承すると Unity の描画解決に割り込み、
  「1 フィールドに `PropertyDrawer` は 1 つ」の制限に巻き込まれる。
  `[ShowIf]` を足しただけで `Optional<T>` 専用の描画が消える、といった事故を避けている。
- **属性を使っていない型には触らない** — そういう型では Unity の既定のインスペクタをそのまま返す。
  このパッケージを入れただけで既存クラスの見た目が変わることはない。
- **設定を間違えたら、消さずに言う** — `nameof` の綴り違いなどで条件が解けないときは、
  フィールドを**表示したまま**理由を赤字で出す。黙って消えると、消えていること自体に気付けない。
- **型ごとに 1 回だけ走査する** — 走査と並べ替えの結果は型単位で作り置きする。
  Inspector は 1 秒に何十回も描き直されるため、ここを毎回計算すると目に見えて重くなる。
- **外部依存ゼロ** — 姉妹パッケージの Containers も要求しない。

---

## 入れ子と複数選択

- 単一の `[Serializable]` class / struct フィールドは再帰して描く。入れ子側の条件、
  `[OnValueChanged]`、`[InlineButton]`、`[Button]` も入れ子の所有者を基準に解決し、
  struct で変更した値は最上位の保存値まで書き戻す。
- 複数選択の条件は全対象で評価する。全対象で非表示になる場合だけ隠し、結果が混在した場合や
  所有者を解決できない対象がある場合は、設定を見失わないよう表示したまま編集を止めて理由を示す。
- `[Required]`、`[AssetOnly]`、`[SceneObjectOnly]`、`[ValidateInput]` は全対象を個別に検査し、
  問題がある件数を表示する。
- `[ShowNativeProperty]` / `[ShowNonSerialized]` は全対象の値を比較し、異なる場合は `—` で混在を示す。
  読み取り専用メンバーでも `[Required]` `[ValidateInput]` `[Suffix]` `[InlineButton]` を使える。

---

## 現時点でできないこと

- 配列・`List<T>` の要素に付いた属性と、実行時に派生型が決まる `[SerializeReference]` の中は
  独自に再帰せず、Unity の既定の `PropertyField` へ任せる。
- 参照が循環している入れ子と 8 階層を超える入れ子は、理由を表示して Unity の既定描画へ戻す。
- 複数選択で値が混在している `[MinValue]` / `[MaxValue]` は、全対象を同じ値へ揃えないよう丸めない。
- **`[Button]` を付けられるのは引数なしのメソッドだけ。** 引数を入力させる欄は、値の置き場所が無いため作らない。

---

## ライセンス

`LICENSE.md` を参照。第三者コードは含んでいない。
