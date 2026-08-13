# Drawing — ドキュメント

デバッグ用の線と文字を 1 行で描く。外部依存なし、`unsafe` 不使用。

動作確認済み環境は Unity 6000.5.7f1、URP 17.5、Windows D3D12。
Built-in Render Pipeline、HDRP、ほかの OS・グラフィックス API は未検証です。

- [なぜこのライブラリなのか](#なぜこのライブラリなのか)
- [導入](#導入)
- [API](#api)
- [仕組み](#仕組み)
- [よくある落とし穴](#よくある落とし穴)
- [できないこと](#できないこと)

---

## なぜこのライブラリなのか

Unity で「一時的に何かを見えるようにする」手段は 2 つあり、どちらも肝心なところで足りません。

**`Debug.DrawLine` は線しか描けません。** 太さも文字も形もありません。
当たり判定の球を見たければ、円を自分で分割して線に落とす必要があります。

**`Gizmos` は `OnDrawGizmos` の中でしか呼べません。** これが一番効きます。
見たいのはたいてい「今まさに起きていること」で、それが分かるのは処理の途中だからです。

```csharp
private Vector3 _debugHitPoint;      // 見るためだけのフィールド
private bool _debugHasHit;

private void FixedUpdate()
{
    _debugHasHit = Physics.SphereCast(origin, radius, dir, out var hit, distance);
    _debugHitPoint = hit.point;      // 持ち越す
}

private void OnDrawGizmos()          // 描くためだけのメソッド
{
    if (!_debugHasHit) return;
    Gizmos.DrawWireSphere(_debugHitPoint, radius);
}
```

確認が終わったらこれを全部消します。次に何かを見たくなったらまた書きます。
この往復が、確認そのものより手間になっています。

このパッケージでは、同じことがこう書けます。

```csharp
if (Physics.SphereCast(origin, radius, dir, out var hit, distance))
{
    Draw.Sphere(hit.point, radius, Color.red, duration: 1f);
}
```

持ち越すフィールドも、描くためのメソッドも要りません。消すときは 1 行消すだけです。

---

## 導入

Package Manager の **Add package from git URL** に、安定版タグを含む次の URL を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/Drawing#drawing-v1.0.0
```

利用側に asmdef がある場合は `Drawing.Runtime` を参照します。
フォルダーを直接管理したい場合だけ、`Drawing/` を `Assets/Modules/Drawing/` へ配置します。

```csharp
using Drawing;
```

シーンに置くものも、初期化の呼び出しもありません。最初に `Draw` を呼んだ時点で、
表示に必要な常駐オブジェクトが（ヒエラルキーに出さない形で）用意されます。

---

## API

公開APIは `Draw` と `DrawScope` の 2 型です。
描画の呼び出しと `Draw.Color` などの既定値変更は、すべて Unity のメインスレッドから行ってください。

### 形

| 呼び出し | 内容 |
|---|---|
| `Draw.Line(a, b)` | 2 点を結ぶ線 |
| `Draw.Ray(origin, direction)` | 始点と向き |
| `Draw.Arrow(from, to, headSize)` | 矢印 |
| `Draw.Path(points, closed)` | 折れ線 |
| `Draw.Box(center, size, rotation)` | 直方体の枠 |
| `Draw.Bounds(bounds)` | `Bounds` の枠 |
| `Draw.Sphere(center, radius)` | 球（直交する 3 円） |
| `Draw.Circle(center, radius, normal)` | 円 |
| `Draw.Capsule(start, end, radius)` | カプセル |
| `Draw.Point(position, size)` | 小さな十字 |
| `Draw.Axis(position, rotation, size)` | 3 軸。赤が右、緑が上、青が前 |
| `Draw.Text(position, text)` | ワールド座標の位置に文字 |

`Draw.Capsule` の `start` / `end` は**半球の中心**で、両端の先端ではありません。
`Physics.CapsuleCast` に渡す 2 点と同じ意味なので、キャストの引数をそのまま渡せます。

### 共通の引数

| 引数 | 既定 | 意味 |
|---|---|---|
| `color` | `Draw.Color`（白） | 線の色 |
| `duration` | `Draw.Duration`（0） | 残る秒数。0 なら 1 フレーム |
| `thickness` | `Draw.Thickness`（1） | 画面上の太さ（ピクセル） |
| `depthTest` | `Draw.DepthTest`（true） | `false` で壁の向こうも見える |

`Draw.Axis` には `color` がなく、`Draw.Text` には `thickness` と `depthTest` がありません。
`duration` の負数・NaN・Infinity は 0、`thickness` の 0 以下・NaN・Infinity は 1 として扱います。
色に NaN または Infinity が含まれる場合は白として扱います。
座標に NaN または Infinity を含む線や文字は受け付けません。

保持できる上限は線分 16384 本、文字 1024 件です。上限を超えた分は捨て、
線と文字の種類ごとにコンソールへ 1 回だけ警告します。

太さは**ピクセル単位**です。カメラから離れても細くなりません。
遠くの線を見失わないためで、世界の大きさに合わせたいという用途は想定していません。

下限は 1.5 ピクセルです。ちょうど 1 ピクセル幅にすると、四角形が画素の中心をまたいだ位置では
どの画素も塗られず、線が虫食いになります（実測で全長の 45% しか出ませんでした）。
髪の毛ほど細い線より、確実に見える線のほうが役に立つので、少しだけ広げてあります。

### スコープ

```csharp
using (Draw.Scope(Color.yellow, duration: 3f, depthTest: false))
{
    Draw.Box(bounds.center, bounds.size);
    Draw.Text(bounds.center, "接地判定");
}
```

抜けると元の既定値へ戻ります。構造体を返すので確保は起きません。

なお `Draw.Scope` 自体はリリースビルドでも残ります（値を返すメソッドは
`[Conditional]` で消せないため）。ただし中で描いている呼び出しは消えるので、
残るのは静的フィールドの読み書きだけです。

`Runtime/Resources` の描画シェーダ自体は Player データへ含まれます。
通常のリリースビルドでは描画呼び出しが無くなり、`[Drawing]` オブジェクトやメッシュ・マテリアルは生成されません。

---

## 仕組み

### 1 枚のメッシュにまとめて投げる

毎フレーム、溜まった線を四角形の集まりとして 1 枚のメッシュに組み、
`Graphics.RenderMesh` に渡しています。

URP では `Camera.onPostRender` が呼ばれません。`GL` で直接描く方法を避け、
メッシュを 1 枚投げることで URP の描画経路へ載せています。

シェーダは `LightMode` タグを持たず、URP では `SRPDefaultUnlit` として扱われます。
Unity 6000.5.7f1、URP 17.5、Windows D3D12 で線・形・深度・太さ・色を確認しています。

### 太さはシェーダで付ける

頂点には線の端点をそのまま入れ、太さぶんの広がりは頂点シェーダの中で、
クリップ座標をスクリーン座標に落としてから付けています。

CPU 側で四角形に広げることもできますが、その場合は「どのカメラに向けて広げるか」を
先に決める必要があります。シーンビューとゲームビューが同時に映っているときに、
片方で太さの向きが狂います。

### 消えるタイミング

`duration` を指定しなかった線は、深度あり・なしの各描画処理へ一度渡すまで保持します。
これにより、更新処理の後で積んだ線が次の更新処理で表示前に消えることを防ぎます。

`duration` を指定しなかった文字は、ゲームビューの再描画処理まで保持します。
再描画時に `Draw.Camera` も `Camera.main` も無ければ、その 1 フレーム文字は破棄し、
後からカメラが現れたときに古い文字がまとめて出ることを防ぎます。
batch/headless環境などで再描画自体が来ない場合も、追加の次フレームを越えて保持しません。

持続時間つきの線と文字は、描画処理へまだ渡せていなくても期限に達した時点で破棄します。

寿命の判定は `Time.unscaledTime` です。`Time.timeScale` を 0 にしても、
指定した秒数どおりに消えます。止めて眺めるのが目的なので、止めた瞬間に
残り時間まで止まってほしくないためです。

---

## よくある落とし穴

**持続時間を付けたまま毎フレーム呼ぶ**

```csharp
private void Update() => Draw.Sphere(p, 1f, duration: 5f);   // 毎フレーム 5 秒ぶん積み上がる
```

球 1 つが 96 本の線になるので、1 秒で 5000 本を超えます。
線分の上限（16384 本）に達すると以降を捨て、コンソールに 1 回だけ警告を出します。
毎フレーム呼ぶなら `duration` は付けないでください（既定の 1 フレームで十分見えます）。

**文字がシーンビューに出ない**

仕様です。文字はワールド座標を画面座標へ落として書いているため、ゲームビューにのみ出ます。
線と形はシーンビューにも出ます。

**編集中（非再生）に何も出ない**

仕様です。`OnDrawGizmos` の代わりにはなりません。編集中に見たいものは Gizmos を使ってください。

**文字が思った場所に出ない**

`Camera.main` を基準にしています。MainCamera タグの付いたカメラが無い、あるいは
別のカメラで見ている場合は `Draw.Camera` に代入してください。

---

## できないこと

- 非再生時（編集中）の描画。
- シーンビューへの文字表示。
- 面の塗りつぶし。輪郭線だけで表します。中身を塗ると奥のものが隠れて、かえって見えなくなるためです。
- 世界の大きさに追従する線の太さ。太さは常に画面上のピクセル単位です。
- メインスレッド以外からの呼び出し。
- Built-in Render Pipeline、HDRP、Windows D3D12 以外の環境は未検証です。
