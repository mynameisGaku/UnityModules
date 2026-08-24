# デバッグ描画（Drawing）

デバッグ用の線と文字を、どこからでも 1 行で描く。外部依存はゼロ、`unsafe` も使わない。

動作確認済み: **Unity 6000.5.7f1** / URP 17.5 / Windows D3D12 / .NET Standard 2.1

Built-in Render Pipeline、HDRP、ほかの OS・グラフィックス API は未検証。

```csharp
using Drawing;

Draw.Line(transform.position, target.position, Color.red, duration: 2f, thickness: 3f);
Draw.Sphere(hit.point, 0.2f);
Draw.Arrow(transform.position, transform.position + velocity);
Draw.Text(head.position, $"HP {hp}");
```

---

## 何のためのライブラリか

Unity には「一時的に何かを見えるようにする」手段が 2 つあるが、どちらも足りない。

| | できること | 足りないこと |
|---|---|---|
| `Debug.DrawLine` | どこからでも呼べる。持続時間を指定できる | **線だけ**。太さなし、文字なし、形なし |
| `Gizmos` / `Handles` | 形も文字も描ける | **`OnDrawGizmos` の中でしか呼べない**。ビルドで消える |

見たいのはたいてい「今まさに起きていること」で、それが分かるのは処理の途中である。
`OnDrawGizmos` まで値を持ち越すために、確認用のフィールドを生やして、
描くためだけのメソッドを書いて、確認が終わったら消す — その往復が毎回発生する。

このパッケージは、その往復をなくす。**呼びたい場所でそのまま呼ぶ。**

```csharp
if (Physics.SphereCast(origin, radius, direction, out var hit, distance))
{
    Draw.Sphere(hit.point, radius, Color.red, duration: 1f);   // ここで呼べる
    Draw.Text(hit.point, hit.collider.name);
}
```

---

## インストール

Package Manager の **Add package from git URL** に、安定版タグを含む次の URL を指定する。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/Drawing#drawing-v1.0.0
```

利用側に asmdef がある場合は `Drawing.Runtime` を参照する。シーンに置くものも、初期化の呼び出しもない。

フォルダーを直接管理したい場合だけ、`Drawing/` を `Assets/Modules/Drawing/` へ配置する。

---

## 描けるもの

| 呼び出し | 内容 |
|---|---|
| `Draw.Line(a, b)` | 2 点を結ぶ線 |
| `Draw.Ray(origin, direction)` | 始点と向き。`Physics.Raycast` に渡した値をそのまま渡せる |
| `Draw.Arrow(from, to)` | 向きの分かる矢印 |
| `Draw.Path(points, closed)` | 折れ線。経路や履歴をそのまま |
| `Draw.Box(center, size, rotation)` | 直方体の枠 |
| `Draw.Bounds(bounds)` | `Renderer.bounds` や `Collider.bounds` をそのまま |
| `Draw.Sphere(center, radius)` | 球の輪郭 |
| `Draw.Circle(center, radius, normal)` | 円 |
| `Draw.Capsule(start, end, radius)` | カプセル。`CapsuleCast` と同じ引数の意味 |
| `Draw.Point(position)` | 位置を示す小さな十字 |
| `Draw.Axis(position, rotation)` | 姿勢を示す 3 軸（赤右・緑上・青前） |
| `Draw.Text(position, text)` | ワールド座標の位置に文字 |
| `Draw.Clear()` | 今出ているものを全部消す |

線と形の呼び出しは、基本的に `color` `duration` `thickness` `depthTest` を省略可能な引数として取る。
`Draw.Axis` は軸ごとに色が決まっているため `color` がなく、`Draw.Text` は線ではないため `thickness` と `depthTest` がない。

```csharp
Draw.Box(center, size, color: Color.cyan, duration: 5f, thickness: 2f, depthTest: false);
```

| 引数 | 既定 | 意味 |
|---|---|---|
| `color` | 白 | 線の色 |
| `duration` | 0 | 残る秒数。0 なら 1 フレーム |
| `thickness` | 1 | 画面上の太さ（ピクセル）。距離が変わっても太さは変わらない。下限は 1.5px |
| `depthTest` | true | `false` にすると壁の向こうでも見える |

`duration` の負数・NaN・Infinity は 0、`thickness` の 0 以下・NaN・Infinity は 1 として扱う。
色に NaN または Infinity が含まれる場合は白として扱う。
座標に NaN または Infinity を含む線や文字は受け付けず、描画メッシュの破損を防ぐ。

### 既定値をまとめて変える

```csharp
using (Draw.Scope(Color.yellow, duration: 3f, depthTest: false))
{
    Draw.Box(bounds.center, bounds.size);
    Draw.Text(bounds.center, "接地判定");
}   // 抜けると元に戻る
```

`Draw.Color` `Draw.Duration` `Draw.Thickness` `Draw.DepthTest` を直接書き換えてもよい。

公開APIは `Draw` と `DrawScope` のみ。すべての呼び出しと既定値の変更は Unity のメインスレッドから行う。

保持できる上限は線分 16384 本、文字 1024 件。上限を超えた分は捨て、種類ごとにコンソールへ 1 回だけ警告する。

---

## リリースビルドでは消える

すべての描画メソッドに `[Conditional("UNITY_EDITOR")]` と `[Conditional("DEVELOPMENT_BUILD")]` が付いている。
製品ビルドでは**呼び出しごとコンパイラが取り除く**ので、`#if` で囲って回る必要がない。

```csharp
Draw.Text(p, $"{ExpensiveDiagnostics()}");   // 製品ビルドでは ExpensiveDiagnostics() も呼ばれない
```

引数の計算も消える点が効く。`Debug.Log` のように文字列を組み立てるコストが残ることはない。

パッケージの `Runtime/Resources` にある描画シェーダ自体は Player データへ含まれる。
通常のリリースビルドでは描画呼び出しが無くなり、`[Drawing]` オブジェクトやメッシュ・マテリアルは生成されない。

---

## 仕組み

毎フレーム、溜まった線を 1 枚のメッシュにまとめて `Graphics.RenderMesh` へ渡している。

URP では `Camera.onPostRender` が呼ばれないため、`GL` で直接描く昔ながらのやり方は使えない。
メッシュを 1 枚投げる形にすると、URP の描画経路へそのまま載せられる。
Unity 6000.5.7f1、URP 17.5、Windows D3D12 で線・形・深度・太さ・色を確認している。

太さは頂点シェーダの中でスクリーン空間に出してから付けている。CPU 側で四角形に広げると
カメラごとに向きを変えられず、シーンビューとゲームビューで太さの向きが食い違う。

`duration: 0` の線は、深度あり・なしの各描画処理へ一度渡すまで保持する。
`duration: 0` の文字はゲームビューの再描画まで保持するが、その時点で表示用カメラが無ければ破棄する。
batch/headless環境などで再描画自体が来ない場合も、追加の次フレームを越えて保持しない。
持続時間つきの線と文字は、描画できていなくても期限に達した時点で破棄する。

---

## できないこと

- **非再生時（編集中）は描かれない。** 描画はランタイム側の更新に乗っているため、
  Play Mode とビルドでのみ動く。`OnDrawGizmos` の代わりにはならない。
- **文字が出るのはゲームビューだけ。** ワールド座標を画面座標へ落として書いているため、
  シーンビューには出ない。線と形はシーンビューにも出る。
- **文字の位置は `Camera.main` を基準にする。** 別のカメラを使いたい場合は `Draw.Camera` に代入する。
- **メインスレッド以外からは呼べない。** 描画バッファーと Unity の描画APIをメインスレッドで更新する設計。
- **面は塗らない。** 輪郭線だけで表す。中身を塗ると奥のものが隠れて、かえって見えなくなるため。
- **Built-in Render Pipeline、HDRP、Windows D3D12 以外の環境は未検証。** 対応を断言せず、導入先で表示を確認する。

---

## ライセンス

`LICENSE.md` を参照。第三者コードは含んでいない。
