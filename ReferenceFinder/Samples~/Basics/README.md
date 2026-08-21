# Reference Finder Basics

1. `ReferenceFinderExampleTarget.asset`を選択します。
2. Project windowの右クリックメニューから **Find Asset References** を実行します。
3. **Search Mode**を`Direct`にして、`ReferenceFinderExampleOwner.asset`だけが表示されることを確認します。
4. **Replacement Asset**へ`ReferenceFinderExampleReplacement.asset`を指定し、**Preview Replacement**を押します。
5. `ReferenceFinderExampleOwner.asset`の`_reference`が1件だけ表示されることを確認します。
6. **Search Mode**を`Recursive`にして、検索結果には`ReferenceFinderExampleOwner.asset`と`ReferenceFinderExampleRoot.asset`が表示されることを確認します。

一括RenameのPreviewも試せます。

1. `ReferenceFinderExampleTarget.asset`と`ReferenceFinderExampleReplacement.asset`を選択します。
2. Project windowで右クリックし、**Batch Rename Selected Assets**を選びます。
3. `Find`へ`ReferenceFinderExample`、`Replace`へ`Demo`を入力します。
4. **Preview**を押し、`DemoTarget.asset`と`DemoReplacement.asset`の2件が表示されることを確認します。
5. サンプル名を維持する場合はApplyせずWindowを閉じます。PreviewだけではAssetは変更されません。

実際に**Replace Previewed References**を押すとOwnerの参照がReplacementへ変わります。確認後はUnityのUndoでサンプルを元へ戻せます。

**Search Root**へこのサンプルfolderを指定すると、Project全体ではなく4個のサンプルAssetだけを候補として検索できます。

このサンプルのファイル名、型名、UI表記はすべて英語です。
