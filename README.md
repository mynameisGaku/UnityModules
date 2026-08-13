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
    └── SaveSystem/
        ├── Runtime/     SaveSystem.Runtime
        ├── Tests/       SaveSystem.Tests
        └── Samples~/    SaveSystem.Samples
```

UPM パッケージとして扱う場合は、モジュールのフォルダを `Packages/` 以下に置くか、
`manifest.json` からローカルパスで参照する。

---

## 各モジュールの約束

- **外部依存を持たない** — 追加パッケージを要求しない
- **`unsafe` を使わない** — asmdef の設定を変えずに導入できる
- **アセンブリ定義を持つ** — 使わないモジュールはビルドに入らない
- **`.meta` を含む** — GUID が変わらないので、参照が壊れない
