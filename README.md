# UnityModules

Unity 向けの再利用可能なモジュール置き場。各モジュールは独立したフォルダに入っており、
アセンブリ定義を持つので、必要なものだけをプロジェクトにコピーして使える。

対応: **Unity 6000.0 以降**

---

## モジュール一覧

| モジュール | 内容 | 依存 |
|---|---|---|
| [Containers](Containers/) | コンテナ / データ構造 66 種。GC フリーのコレクション、Inspector に出せるシリアライズ対応型、空間分割、Unity のライフサイクルに耐えるコンテナ。 | なし |
| [Inspector](Inspector/) | Inspector 拡張の属性 43 種。条件による表示・非表示、グループ化とタブ、入力値の検証、メソッドのボタン化。**Unity 6000.5 以降**。 | なし |
| [Drawing](Drawing/) | 実行中の線・矢印・箱・球・経路・文字をコード1行で描くデバッグ可視化。Development Build専用呼び出しと持続時間に対応。**Unity 6000.5 以降**。 | なし |
| [Save System](SaveSystem/) | 型付きJSON保存、複数スロット、破損検出、可能な環境での原子的置換、1世代バックアップ復旧。依存なし。**Unity 6000.5 以降**。 | なし |
| [Scene Flow](SceneFlow/) | 完全なSceneパスでSingle・Additive読込、有効Scene切替、Unloadを直列化し、開始前条件と完了後状態を結果で返す。**Unity 6000.5 以降**。 | なし |
| [Screen Transition](ScreenTransition/) | UI Toolkitの全画面オーバーレイでCover・Revealを非スケール時間に実行し、色・時間・補間方法・完了結果を明示する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Time Control](TimeControl/) | Scene所有のControllerが複数leaseの相対倍率を最小値で集約し、pause・slow motion・単独fast-forwardをTime.timeScaleへ安全に反映する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Diagnostics Context](DiagnosticsContext/) | 明示追加したcontext・breadcrumbと実行中のUnity Warning・Error・Assert・Exceptionを有界に保持し、手動操作時だけJSON reportへ書き出す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [Build Guard](BuildGuard/) | Player build対象Sceneのactive・inactive階層とPrefab instanceを検査し、Missing MonoBehaviourを階層path・件数付きでbuild前に拒否するEditor専用module。**Unity 6000.5 以降**。 | なし |

---

## 使い方

使いたいモジュールのフォルダを、プロジェクトの `Assets/` 以下にコピーする。
アセンブリ定義が同梱されているので、利用側の asmdef からそれを参照する。

```
Assets/
└── Modules/
    ├── Containers/
    │   ├── Runtime/     Containers.Runtime
    │   ├── Editor/      Containers.Editor
    │   └── Tests/       Containers.Tests
    ├── Inspector/
    │   ├── Runtime/     Inspector.Runtime   属性の定義だけ
    │   ├── Editor/      Inspector.Editor    解釈と描画
    │   └── Tests/       Inspector.Tests
    ├── Drawing/
    │   ├── Runtime/     Drawing.Runtime
    │   └── Tests/       Drawing.Tests
    ├── SaveSystem/
    │   ├── Runtime/     SaveSystem.Runtime
    │   ├── Tests/       SaveSystem.Tests
    │   └── Samples~/    SaveSystem.Samples
    ├── SceneFlow/
    │   ├── Runtime/     SceneFlow.Runtime
    │   ├── Editor/      SceneFlow.Editor
    │   ├── Tests/       SceneFlow.Tests / SceneFlow.Editor.Tests / SceneFlow.PlayMode.Tests
    │   └── Samples~/    SceneFlow.Samples
    ├── ScreenTransition/
    │   ├── Runtime/     ScreenTransition.Runtime
    │   ├── Tests/       ScreenTransition.Tests / ScreenTransition.PlayMode.Tests
    │   └── Samples~/    ScreenTransition.Samples / ScreenTransition.Samples.PlayMode.Tests
    ├── TimeControl/
    │   ├── Runtime/     TimeControl.Runtime
    │   ├── Tests/       TimeControl.Tests / TimeControl.PlayMode.Tests
    │   └── Samples~/    TimeControl.Samples / TimeControl.Samples.PlayMode.Tests
    ├── DiagnosticsContext/
        ├── Runtime/     DiagnosticsContext.Runtime
        ├── Tests/       DiagnosticsContext.Tests / DiagnosticsContext.PlayMode.Tests
        └── Samples~/    DiagnosticsContext.Samples / DiagnosticsContext.Samples.PlayMode.Tests
    └── BuildGuard/
        ├── Editor/      BuildGuard.Editor
        ├── Tests/       BuildGuard.Tests
        └── Samples~/    Build Guard Basics
```

UPM パッケージとして扱う場合は、モジュールのフォルダを `Packages/` 以下に置くか、
`manifest.json` からローカルパスで参照する。

---

## 各モジュールの約束

- **依存を明記する** — リポジトリ内モジュールへの依存と導入順を各 README に書く
- **`unsafe` を使わない** — asmdef の設定を変えずに導入できる
- **アセンブリ定義を持つ** — 使わないモジュールはビルドに入らない
- **`.meta` を含む** — GUID が変わらないので、参照が壊れない
