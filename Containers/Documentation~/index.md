# Containers — ドキュメント

Unity 6000.0 以降向けのコンテナ / データ構造ライブラリ。外部依存なし、`unsafe` 不使用。

- [なぜこのライブラリなのか](#なぜこのライブラリなのか)
- [導入](#導入)
- [設計上の約束](#設計上の約束)
- [カテゴリ別ガイド](#カテゴリ別ガイド)
- [よくある落とし穴](#よくある落とし穴)
- [標準にあるので同梱していないもの](#標準にあるので同梱していないもの)

---

## なぜこのライブラリなのか

Unity で「コンテナが足りない」と感じる場面は、実際には性質の違う 4 つに分かれます。

**1. `List<T>` が構造体を `ref` で返せない**

```csharp
List<Particle> list = ...;
list[0].Position += delta;   // コンパイルエラー（CS1612）
```

`List<T>` の添字は値を返すので、構造体の要素をその場で書き換えられません。回避策は
「読んで、書き換えて、書き戻す」ですが、要素が大きいほどコピーが効きます。
`FastList<T>` は `ref` で返すため、上の 1 行がそのまま通ります。

**2. Unity のシリアライザが辞書を保存できない**

`Dictionary<K,V>` も `System.Type` も `Nullable<T>` も、Unity は保存しません。
そのため enum → プレハブのような対応表が「List ＋ 線形探索」で書かれがちです。
`SerializableDictionary<K,V>` は実行時は本物の辞書のまま、ディスク上は 2 本のリストとして持ちます。

**3. ゲームロジックそのものがデータ構造になっている**

近傍探索、経路探索、重み付き抽選、階層タグ。どれも毎回書き直され、毎回同じところで間違えます。

**4. Unity のライフサイクルが前提を壊す**

これが最も Unity 固有の問題です。破棄済みの `UnityEngine.Object` は `== null` が `true` を返すのに、
マネージド参照としては生きています。素の `Dictionary<GameObject, T>` に入れると、

- `key == null` が真なので、そのエントリは**誰にも引けない**
- しかし参照は生きているので、**GC に回収されない**
- 値の側のオブジェクトグラフごと道連れにする

`UnityObjectMap<K,V>` はこの一点のために存在します。

---

## 導入

`Assets/` 以下にフォルダごと配置します。アセンブリ定義が同梱されているので、
利用側の asmdef から `Containers.Runtime` を参照してください。

```csharp
using Containers;            // コア・シリアライズ・Unity ライフサイクル
using Containers.Spatial;    // グリッド・木・グラフ
using Containers.Gameplay;   // 抽選・タグ・インベントリ
using Containers.Async;      // スレッド間・await
```

アセンブリ構成：

| アセンブリ | 内容 | プラットフォーム |
|---|---|---|
| `Containers.Runtime` | 全コンテナ | すべて |
| `Containers.Editor` | PropertyDrawer 10 種 | Editor のみ |
| `Containers.Tests` | 単体テスト | `UNITY_INCLUDE_TESTS` |

---

## 設計上の約束

**確保しない列挙**
全コンテナが構造体列挙子を持ち、`IEnumerable<T>` は明示的実装です。`foreach` でボックス化しません。
インターフェース経由で回すと従来どおり確保が起きるので、ホットパスでは具体型で受けてください。

```csharp
FastList<Enemy> enemies = ...;
foreach (var e in enemies) { }              // 確保なし
IEnumerable<Enemy> boxed = enemies;
foreach (var e in boxed) { }                // 列挙子がボックス化される
```

**`ref` で返す**
配列を土台にしたコンテナは要素を `ref` で返します。例外は `FixedList8<T>` で、
C# の制約（CS8170：構造体は自分のフィールドへの `ref` を返せない）により値返しです。

**外部依存ゼロ**
`com.unity.collections` を含め、追加パッケージを要求しません。
Job System や Burst と組み合わせるなら、自作より先にそのパッケージの導入を検討してください。

**`unsafe` を使わない**
asmdef の "Allow 'unsafe' Code" を有効にせずに導入できます。

---

## カテゴリ別ガイド

### コア — GC フリー / 高速

| 型 | 選ぶ理由 |
|---|---|
| `FastList<T>` | 構造体をその場で書き換える／`Span` で渡す／O(1) 削除 |
| `RingBuffer<T>` | 直近 N 件だけ欲しい（入力履歴・計測・ロールバック） |
| `Deque<T>` | 両端から出し入れし、容量が伸びてほしい |
| `PriorityQueue<E,P>` | 標準に無い。A*・時刻順スケジューラ |
| `IndexedPriorityQueue<E,P>` | 優先度を下げたい（Dijkstra を重複投入なしで） |
| `SparseSet<T>` | int id で O(1)、走査は連続メモリ |
| `SlotMap<T>` | 削除をまたいでも安全なハンドルを配りたい |
| `BitSet` / `FixedBitSet64` | 密なフラグ集合 |
| `ChunkedList<T>` | 伸びても要素アドレスを動かしたくない |
| `RentedArray<T>` / `TempList<T>` | 一時バッファの確保をなくす |
| `LruCache<K,V>` | 上限付きキャッシュ（無制限はリーク） |

**セルサイズや容量の目安**

- `RingBuffer` — 保持したい秒数 × フレームレート
- `LruCache` — 実測のワーキングセット。上限を切ることに意味がある
- `ChunkedList` — `chunkSizeLog2: 10`（1024 要素）が既定で妥当

### シリアライズ対応

すべて PropertyDrawer 付きです。ジェネリック型は Unity にシリアライズさせるため
**具体型で継承**する必要があります。

```csharp
[Serializable] public sealed class LootByRank : SerializableDictionary<Rank, LootTable> { }
```

`[SubclassSelector]` は `[SerializeReference]` と併用します。候補になるのは
`[Serializable]` かつ引数なしコンストラクタを持つ非抽象クラスで、
`UnityEngine.Object` 派生は（Unity が managed reference として保存できないため）除外されます。

### 空間・グラフ

**どれを使うか**

| 状況 | 選ぶもの |
|---|---|
| 大量が毎フレーム動く、近傍を訊く | `SpatialHashGrid<T>` |
| 盤面・タイル・フローフィールド | `Grid2D<T>` / `Grid3D<T>` |
| ヘックス盤面 | `HexGrid<T>` |
| 分布が偏る、静的寄り | `QuadTree<T>` / `Octree<T>` |
| 動かない点群の最近傍 | `KdTree<T>` |
| 大きさのある対象へのレイ／重なり | `DynamicAabbTree<T>` |
| 経路・依存関係・解放順 | `Graph<TNode>` |

`Graph.TryFindPath` にヒューリスティックを渡すと A*、渡さないと Dijkstra になります。
**ヒューリスティックは残りコストを過大評価してはいけません**（過大評価すると最小コストでない経路が返ります）。

### 時間・履歴・状態

`TimerCollection` と `ScheduledEventQueue` は用途が近いですが基準が違います。

- `TimerCollection` — 各タイマーが残り時間を持ち、毎回全件を減算。キャンセル・延長・進捗表示が要るとき
- `ScheduledEventQueue` — 絶対時刻でヒープに積む。件数が増えてもコストが変わらず、**実行順が完全に決定論的**

決定論が要る場面（リプレイ、ロールスペップ同期）では後者を使ってください。
同時刻のイベントは投入順に実行されます。

### リアクティブ

`ReactiveProperty<T>` と `VersionedValue<T>` の使い分け：

| | `ReactiveProperty<T>` | `VersionedValue<T>` |
|---|---|---|
| 通知 | 即時（イベント） | ポーリング（版数） |
| 確保 | デリゲート分 | ゼロ |
| 解除漏れ | あり得る | 原理的に起きない |
| 向く場面 | 稀な事象 | 毎フレーム見る UI |

### Unity ライフサイクル

このカテゴリが本ライブラリの核です。詳細は[なぜこのライブラリなのか](#なぜこのライブラリなのか)を参照。

`GameObjectPool<T>` は Unity 標準の `UnityEngine.Pool.ObjectPool<T>` の代わりではなく**その上の層**です。
標準側は素の C# オブジェクト用で、プレハブ・有効化・親子関係を知りません。

---

## よくある落とし穴

**`foreach` をインターフェース経由で回している**
`IEnumerable<T>` で受けると列挙子がボックス化されます。ホットパスでは具体型で受けてください。

**`RemoveAtSwapBack` の後に順序を期待している**
O(1) 削除は末尾の要素を穴に移します。順序が要るなら `RemoveAt` を使ってください。

**`SlotHandle.IsValid` を生存確認だと思っている**
`IsValid` は「`None` でない」だけを見ます。生きているかは `SlotMap.IsAlive` で確認してください。

**`SpatialHashGrid.QueryRadius` の結果を厳密だと思っている**
セル単位で正確、距離では不正確です。厳密に絞るなら `QueryRadiusExact` を使ってください。

**`RuntimeSet<T>` が再生停止後も中身を持っている**
ScriptableObject の性質です。有効化時にクリアしていますが、
エディタ実行の途中でアセットを直接見ると前回の残りが見えることがあります。

**`StringId` を保存している**
識別子はプロセスごとに変わります。永続化するのは元の文字列です。

---

## 標準にあるので同梱していないもの

この構成（netstandard2.1 / API Compatibility Level .NET Standard 2.1）で参照可能なため、
意図的に再実装していません。

| 標準にあるもの | 出どころ |
|---|---|
| `ObjectPool<T>` / `ListPool` / `HashSetPool` | `UnityEngine.Pool` |
| `ArrayPool<T>` | `System.Buffers` |
| `Span<T>` / `Memory<T>` | `System.Memory` |
| `ImmutableArray` ほか | `System.Collections.Immutable` |
| `ConcurrentQueue` ほか | `System.Collections.Concurrent` |
| `SortedSet` / `SortedDictionary` / `LinkedList` | BCL |
| `ConditionalWeakTable` | BCL |

**例外**：`System.Collections.Generic.PriorityQueue` は .NET 6 以降の型で
netstandard2.1 には存在しないため、API を合わせた実装を同梱しています。
将来ランタイムが上がったら、このファイルを削除するだけで移行できます。
