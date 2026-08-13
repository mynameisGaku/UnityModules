# UnityModules

Unity 向けの再利用可能なモジュール置き場。各モジュールは独立したフォルダに入っており、
アセンブリ定義を持つので、必要なものだけをプロジェクトにコピーして使える。

対応: **Unity 6000.0 以降**

---

## モジュール一覧

| モジュール | 内容 | 依存 |
|---|---|---|
| [Containers](Containers/) | コンテナ / データ構造 66 種。GC フリーのコレクション、Inspector に出せるシリアライズ対応型、空間分割、Unity のライフサイクルに耐えるコンテナ。 | なし |
| [DebugMenu](DebugMenu/) | 全画面ランタイムデバッグメニュー。値変更、アクション、監視グラフ、HSV 色編集をキーボードとマウスから操作できる。 | Containers 1.0.0 |
| [Inspector](Inspector/) | Inspector 拡張の属性 43 種。条件による表示・非表示、グループ化とタブ、入力値の検証、メソッドのボタン化。**Unity 6000.5 以降**。 | なし |

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
    ├── DebugMenu/
    │   ├── Runtime/     DebugMenu.Runtime
    │   ├── Editor/      DebugMenu.Editor
    │   └── Tests/       DebugMenu.Tests
    └── Inspector/
        ├── Runtime/     Inspector.Runtime   属性の定義だけ
        ├── Editor/      Inspector.Editor    解釈と描画
        └── Tests/       Inspector.Tests
```

UPM パッケージとして扱う場合は、モジュールのフォルダを `Packages/` 以下に置くか、
`manifest.json` からローカルパスで参照する。

---

## 各モジュールの約束

- **依存を明記する** — リポジトリ内モジュールへの依存と導入順を各 README に書く
- **`unsafe` を使わない** — asmdef の設定を変えずに導入できる
- **アセンブリ定義を持つ** — 使わないモジュールはビルドに入らない
- **`.meta` を含む** — GUID が変わらないので、参照が壊れない
