# Reference Finder Basics

1. `ReferenceFinderExampleTarget.asset`を選択します。
2. Project windowの右クリックメニューから **Find Asset References** を実行します。
3. **Search Mode**を`Direct`にして、`ReferenceFinderExampleOwner.asset`だけが表示されることを確認します。
4. **Replacement Asset**へ`ReferenceFinderExampleReplacement.asset`を指定し、**Preview Replacement**を押します。
5. `ReferenceFinderExampleOwner.asset`の`_reference`が1件だけ表示されることを確認します。
6. **Search Mode**を`Recursive`にして、検索結果には`ReferenceFinderExampleOwner.asset`と`ReferenceFinderExampleRoot.asset`が表示されることを確認します。

実際に**Replace Previewed References**を押すとOwnerの参照がReplacementへ変わります。確認後はUnityのUndoでサンプルを元へ戻せます。

**Search Root**へこのサンプルfolderを指定すると、Project全体ではなく4個のサンプルAssetだけを候補として検索できます。

このサンプルのファイル名、型名、UI表記はすべて英語です。
