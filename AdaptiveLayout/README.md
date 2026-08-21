# 画面サイズ・ノッチ対応（AdaptiveLayout）

## 30秒で分かる

スマートフォンのノッチ、角丸、ホームインジケーター、テレビのオーバースキャンで操作UIが隠れないように、Unityの`Screen.safeArea`をUIレイアウトへ自動反映します。

画面回転や解像度変更も監視するため、端末ごとの余白計算、UI Toolkitの上下反転、RectTransformのanchor計算を画面ごとに書く必要がありません。

## できること

- UI Toolkitの指定`VisualElement`をSafe Area内へ配置する。
- uGUIなどで使う`RectTransform`のanchorをSafe Areaへ合わせる。
- 左・上・右・下から、守りたいedgeだけを選ぶ。
- 画面回転、解像度変更、Safe Area変更へ自動追従する。
- Componentを無効にしたとき、導入前のinline styleまたはRectTransform値へ戻す。
- UnityのDevice Simulatorで実端末へbuildする前に確認する。

## 使わない方がよい場合

- world-space UIやRenderTexture上のUIを端末Safe Areaへ合わせたい。
- backgroundや演出までSafe Area内へ縮めたい。通常は操作contentだけを対象にし、backgroundは画面全体へ残します。
- Scene切り替え、入力停止、画面fade、独自のresponsive breakpointも同時に管理したい。

これらはv1の対象外です。

## 3分で試す

1. Package Managerの **Add package from git URL** へ次を入力します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/AdaptiveLayout#adaptive-layout-v1.0.0
   ```

2. Package ManagerのSamplesから **Adaptive Layout Basics** をImportします。
3. `AdaptiveLayoutBasics.unity`を開いてPlayします。
4. Game viewをDevice Simulatorへ切り替え、ノッチのある端末を選びます。
5. 端末をRotateし、緑のSafe ContentがSafe Area内へ追従することを確認します。

利用側にasmdefがある場合は`AdaptiveLayout.Runtime`を参照します。

## UI Toolkitで使う

1. `UIDocument`と同じGameObjectへ`SafeAreaVisualElement`を追加します。
2. Safe Area内へ置きたいcontentへ一意な`name`を設定します。
3. **Target Element Name**へその名前を入力します。
4. backgroundはtargetの外側へ置き、画面全体を描画させます。

```csharp
using AdaptiveLayout;
using UnityEngine;

public sealed class SafeAreaOwner : MonoBehaviour
{
    [SerializeField] private SafeAreaVisualElement _safeArea;

    private void OnEnable()
    {
        _safeArea.Edges = SafeAreaEdges.All;
        _safeArea.Refresh();
    }
}
```

`Screen.safeArea`は左下原点、UI Toolkitは左上原点です。この座標変換とPanel Settingsのscale変換はComponent内で行います。

## RectTransformで使う

1. 画面全体へstretchした親`RectTransform`を作ります。
2. その直下へSafe Area内のcontent用`RectTransform`を作ります。
3. contentへ`SafeAreaRectTransform`を追加します。

ComponentはSafe Areaを0〜1のanchorへ変換し、offsetを0にします。対象のRectTransformをほかのlayout componentやscriptから同時に書き換えないでください。

## 実行するとどうなるか

Safe Areaが画面全体なら、targetも親全体を使います。ノッチなどがある場合は、選択したedgeだけが内側へ移動します。`Current`から最後に適用したscreen size、safe rectangle、四辺のpixel insetを確認できます。

## よくある問題

### UI Toolkitのtargetが動かない

`Target Element Name`と実際の`VisualElement.name`が一致しているか、`UIDocument`に`PanelSettings`が設定されているかを確認してください。Componentはtargetとpanelが利用可能になるまで待ちます。

### 背景まで小さくなった

document rootではなく、操作buttonや重要情報をまとめた子要素をtargetにしてください。backgroundはtargetの兄弟として画面全体へ残します。

### RectTransformが別の位置へ戻る

Animator、Layout Group、別scriptなど、同じanchorとoffsetを書き換えるownerが存在しないか確認してください。このComponentは対象layoutのownerになります。

### Device Simulatorと実機が違う

Device SimulatorはSafe Areaと回転の基本確認用です。性能や描画能力を完全には再現しないため、最終確認は対象端末でも行ってください。

## 詳しい契約

- `SafeAreaEdges`は適用する四辺を選びます。
- `SafeAreaSnapshot`はscreen size、safe rectangle、四辺のinsetを保持します。
- `SafeAreaRectTransform`は親がscreen viewport全体を表す前提でnormalized anchorを適用します。
- `SafeAreaVisualElement`はtargetの親をviewport基準に使い、absolute insetを適用します。
- 無効なscreen size、非有限または範囲外のSafe Area、未接続panel、0 sizeの親には書き込みません。
- global singleton、自動生成GameObject、Scene常駐ownerを作りません。

より詳しい設計と検証範囲は[Documentation](Documentation~/index.md)を参照してください。
