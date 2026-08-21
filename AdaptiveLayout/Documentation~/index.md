# 画面サイズ・ノッチ対応

## 解決する問題

端末ごとのSafe Areaはscreen pixelで提供されますが、UI ToolkitとRectTransformでは適用方法が異なります。また、画面回転、window resize、Device Simulatorの端末変更で値が変わります。

AdaptiveLayoutは、Safe Areaの取得と検証を共通化し、UI ToolkitとRectTransformへそれぞれ適切な座標で反映します。

## 依存方向

```text
Screen.width / Screen.height / Screen.safeArea
                         ↓
                 SafeAreaSnapshot
                  ↓             ↓
       normalized anchors   panel coordinates
                  ↓             ↓
          RectTransform     VisualElement
```

UIの内容、Scene、入力、時間、音声はこのmoduleへ依存しません。

## UI Toolkit

`SafeAreaVisualElement`は、`Screen.safeArea`の左下原点をUI Toolkitの左上原点へ変換し、`RuntimePanelUtils.ScreenToPanel`でPanel Settingsのscaleを反映します。targetをabsolute positionへ設定し、選択edgeの`left`、`top`、`right`、`bottom`を更新します。

targetの親はscreen-space panel viewportを表す必要があります。world-space panel、custom screen-to-panel function、target textureはv1の保証外です。

## RectTransform

`SafeAreaRectTransform`はSafe Areaをscreen全体に対する0〜1座標へ変換し、targetの`anchorMin`と`anchorMax`へ設定します。`offsetMin`と`offsetMax`は0にします。

targetの親はscreen viewport全体を表す必要があります。Safe Area適用中に同じRectTransformをAnimator、Layout Group、別scriptから変更しないでください。

## Lifecycle

Componentは`OnEnable`で元layoutを取得し、`LateUpdate`でscreen sizeとSafe Areaの変更を検出します。`OnDisable`では既定で元layoutを復元します。global registryや自動生成objectはありません。

## 検証

- EditMode: input validation、四辺inset、normalized anchors、edge selection、公開API面積。
- PlayMode: RectTransform適用、edge selection、source変更追従、無効化復元、UI Toolkit panel coordinateとworld bounds。
- Release gate: local tarball UPM install、sample import、Device Simulator相当のwide/tall/cutout geometry、Mono/IL2CPP Player build。

## 非対象

- world-space UIとRenderTexture向けSafe Area。
- cutoutごとの不規則なpolygon回避。
- responsive breakpoint、font scaling、content reflow。
- Scene loading、screen fade、input blocking。
- Safe Area外backgroundの自動生成。
