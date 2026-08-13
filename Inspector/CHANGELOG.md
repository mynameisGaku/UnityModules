# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [1.0.0] - 2026-08-13

### Added

- 条件属性 7 種。`[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]` `[ReadOnly]` `[ShowInPlayMode]` `[HideInPlayMode]`。
  条件はフィールド・プロパティ・引数なしメソッドのいずれからでも引け、private も基底クラスのものも参照できる。
  bool メンバー、値との一致（複数可）、`ConditionOperator.And` / `Or` による複数条件、`"!"` 接頭辞による反転に対応。
- レイアウト属性 9 種。`[Foldout]` `[BoxGroup]` `[TabGroup]` `[HorizontalGroup]` `[Title]` `[InfoBox]` `[HorizontalLine]` `[Indent]` `[Order]`。
  グループパスは `/` 区切りで入れ子にでき、種類の違うグループ属性を併記すると一番深いパスが所属先になる。
  開閉状態と選択中のタブは型ごとに `EditorPrefs` へ記憶する。
- 見た目の調整属性 6 種。`[LabelText]` `[HideLabel]` `[LabelWidth]` `[GUIColor]` `[Suffix]` `[InlineButton]`。
- 検証属性 6 種。`[Required]` `[ValidateInput]` `[MinValue]` `[MaxValue]` `[AssetOnly]` `[SceneObjectOnly]`。
  数値の範囲は知らせるだけでなく書き戻して丸める。検査メソッドは値あり・値と文言・引数なしの 3 つの形を受け付ける。
- メンバー属性 4 種。`[Button]` `[OnValueChanged]` `[ShowNonSerialized]` `[ShowNativeProperty]`。
  ボタンは選択中の全オブジェクトに対して呼び、`Undo` に控えを取る。
- 値の描き方を変える属性 11 種。`[Dropdown]` `[Tag]` `[Layer]` `[SortingLayer]` `[Scene]` `[ProgressBar]`
  `[Expandable]` `[ResizableTextArea]` `[FilePath]` `[FolderPath]` `[ShowAssetPreview]`。
- 属性を使っていない型は Unity の既定のインスペクタへそのまま委ねる。導入しただけで既存の見た目は変わらない。
- 設定を間違えた属性（存在しないメンバー名、bool と文字列の比較、種類の食い違うグループ指定、
  引数付きメソッドへの `[Button]` など）は、対象を隠さずに理由を Inspector 上へ表示する。
- 独自の `CustomEditor` から使うための `InspectorEditor` 基底クラスと `InspectorGUILayout.Draw`。
- 走査・並べ替え・グループ構築の結果を型ごとに作り置きする `InspectorLayoutCache`。
- 単一の `[Serializable]` class / struct フィールドを再帰して描き、入れ子の条件・変更通知・ボタンを
  入れ子の所有者から解決する。struct の変更は最上位の保存値まで書き戻す。
- 複数選択では条件と検証を全対象で評価する。条件の結果が混在するときは表示を残して編集を止め、
  検証に通らない対象は件数付きで知らせる。
- `[ShowNativeProperty]` / `[ShowNonSerialized]` の複数選択では全対象の値を比較し、異なる場合は混在表示にする。
  `[Required]` `[ValidateInput]` `[Suffix]` `[InlineButton]` も読み取り専用メンバーで機能する。
- `[Expandable(Expanded = true)]` の初期開閉を対象とプロパティごとに一度だけ反映する。
- 設定済みコンポーネントをすぐ確認できる `Inspector Basics` サンプルシーン。

### Notes

- 配列・`List<T>` の要素属性、実行時派生型の `[SerializeReference]`、循環または 8 階層を超える入れ子は
  Unity の既定描画へ戻す。
- 複数選択で値が混在する `[MinValue]` / `[MaxValue]` は、全対象を同じ値へ揃えないよう丸めない。
- `[Header]` `[Space]` `[Tooltip]` `[Range]` `[TextArea]` など Unity 標準の属性は再実装せず、そのまま素通しする。
- `[MinMaxSlider]` と `[SubclassSelector]` は Containers パッケージ側にあるため収録していない。
