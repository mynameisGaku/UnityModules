# Containers

Unity 向けのコンテナ / データ構造ライブラリ。**66 個**のコンテナを 8 カテゴリで収録し、
外部依存はゼロ、`unsafe` も使わない。

対応: **Unity 6000.0 以降** / .NET Standard 2.1 / C# 9

---

## 何のためのライブラリか

Unity で「便利なコンテナ」と言ったとき、実際に足りていないものは 4 種類に分かれる。
このパッケージはその 4 つを埋める。

| 系統 | 埋めるもの | 代表 |
|---|---|---|
| **GC フリー** | `List<T>` が構造体を `ref` で返せない | `FastList<T>` |
| **シリアライズ** | Unity が `Dictionary` も `Type` も保存できない | `SerializableDictionary<K,V>` |
| **ゲームロジック** | 空間分割・時間・抽選が毎回書き直しになる | `SpatialHashGrid<T>` |
| **Unity ライフサイクル** | 破棄済みオブジェクトとシーン跨ぎで壊れる | `UnityObjectMap<K,V>` |

標準にあるもの（`UnityEngine.Pool`、`ArrayPool<T>`、`Span<T>`、`ConcurrentQueue<T>`、
`System.Collections.Immutable`）は**意図的に再実装していない**。
逆に `System.Collections.Generic.PriorityQueue` は .NET 6 以降の型で
netstandard2.1 には存在しないため、API を合わせた実装を同梱している。

---

## インストール

`Assets/` 以下にフォルダごと配置する。アセンブリ定義が同梱されているので、
利用側の asmdef から `Containers.Runtime` を参照する。

```csharp
using Containers;                 // コア・シリアライズ・Unity ライフサイクル
using Containers.Spatial;         // グリッド・木・グラフ
using Containers.Gameplay;        // 抽選・タグ・インベントリ
using Containers.Async;           // スレッド間・await
```

---

## まず使うもの

### `FastList<T>` — 構造体をその場で書き換えられるリスト

```csharp
var particles = new FastList<Particle>(256);
particles.Add(new Particle { Position = Vector3.zero });

// List<T> ではコンパイルが通らない書き方
particles[0].Position += velocity * Time.deltaTime;

particles.RemoveAtSwapBack(3);          // O(1)（順序は変わる）
Simulate(particles.AsSpan());           // Span で渡せる
```

### `SerializableDictionary<K,V>` — Inspector に出せる辞書

```csharp
[Serializable] public sealed class LootByRank : SerializableDictionary<Rank, LootTable> { }

[SerializeField] private LootByRank _loot;
var table = _loot[Rank.Elite];
```

キーが重複すると Inspector 上で行が赤くなり、警告が出る。

### `Optional<T>` — 「未設定」を null 無しで表す

```csharp
[SerializeField] private Optional<float> _speedOverride;

var speed = _speedOverride.GetValueOrDefault(_defaults.Speed);
```

Inspector ではチェックボックスと値が 1 行に並ぶ。

### `UnityObjectMap<K,V>` — 破棄済みキーで漏れない辞書

```csharp
private readonly UnityObjectMap<GameObject, AiState> _states = new();

_states.Set(enemy.gameObject, state);
// enemy が Destroy されたあと：
_states.TryGetValue(enemy.gameObject, out _);   // false。エントリも自動で掃除される
```

素の `Dictionary<GameObject, T>` は、破棄済みキーのエントリが
**誰にも引けないまま永久に残る**。このコンテナはその一点のために存在する。

### `DisposableBag` — 購読解除の書き忘れを構造的に消す

```csharp
private readonly DisposableBag _bag = new();

void OnEnable() => _bag.AddAction(
    () => _health.Changed -= OnHealthChanged,   // 解除
    () => _health.Changed += OnHealthChanged);  // 購読（今すぐ実行される）

void OnDisable() => _bag.Dispose();
```

### `SpatialHashGrid<T>` — 近傍探索

```csharp
var grid = new SpatialHashGrid<Boid>(cellSize: 3f);

foreach (var boid in flock) grid.Update(boid, boid.Position);

using var nearby = TempList<Boid>.Rent();
grid.QueryRadius(self.Position, 3f, nearby.List);
```

`Physics.OverlapSphere` を全員ぶん毎フレーム呼ぶのに比べ、桁で速くなる。

### `PriorityQueue<T,TPriority>` — 標準に無い優先度付きキュー

```csharp
var open = new PriorityQueue<Node, float>();
open.Enqueue(start, 0f);
while (open.TryDequeue(out var node, out _)) { /* A* */ }
```

---

## カテゴリ別の一覧

### コア（GC フリー / 高速）

| 型 | 何が嬉しいか |
|---|---|
| `FastList<T>` | `ref` 要素アクセス、`AsSpan()`、`RemoveAtSwapBack` |
| `RingBuffer<T>` | 固定容量・両端 O(1)・古い順に上書き |
| `Deque<T>` | 伸びる両端キュー |
| `PriorityQueue<TElement,TPriority>` | netstandard2.1 に無い最小ヒープ |
| `IndexedPriorityQueue<TElement,TPriority>` | 優先度の引き下げに対応（Dijkstra / A*） |
| `SparseSet<T>` / `SparseIntSet` | int id で O(1)、走査は連続メモリ |
| `SlotMap<T>` | 世代付きハンドルで、削除済み参照を検出できる |
| `BitSet` / `FixedBitSet64` | 密なフラグ集合 |
| `ChunkedList<T>` | 伸長時にコピーせず、要素アドレスが動かない |
| `RentedArray<T>` / `TempList<T>` | `using` スコープのプール借用 |
| `MultiMap<K,V>` | 1 キー→複数値、内部リストはプール |
| `BiMap<L,R>` | 双方向に引ける辞書 |
| `DefaultDictionary<K,V>` | 未登録キーで自動生成 |
| `Counter<T>` | 個数付き集合。`TryTake` で安全に消費 |
| `LruCache<K,V>` | 上限付きキャッシュ。ノード確保なし |
| `StringId` / `StringKeyMap<V>` | 文字列を int に畳んで引く |
| `FixedList8<T>` | 構造体に埋め込める最大 8 要素のリスト |

### シリアライズ対応（PropertyDrawer 付き）

| 型 | 何が嬉しいか |
|---|---|
| `SerializableDictionary<K,V>` | Inspector に出る辞書。重複キーを警告 |
| `SerializableHashSet<T>` | Inspector に出る集合。重複を警告 |
| `Optional<T>` | 「未設定 / 上書き」を 1 行で |
| `SerializableType` | `System.Type` を型ピッカーで選ぶ |
| `[SubclassSelector]` | `[SerializeReference]` に派生型ドロップダウン |
| `FloatRange` / `IntRange` | `[MinMaxSlider]` でスライダー編集 |
| `SerializableGuid` | 安定 ID。生成・コピーのボタン付き |
| `InterfaceReference<T>` | インターフェース実装だけ受け付けるフィールド |
| `EnumMap<TEnum,TValue>` | enum 別の値。ボクシングしない |
| `RuntimeSet<T>` | 自己登録型の ScriptableObject 集合 |
| `LayeredConfig<K,V>` | 多層の設定解決。決定した層を返せる |
| `SerializableStack<T>` / `SerializableQueue<T>` | Inspector に出るスタック / キュー |

### 空間・グラフ

`SpatialHashGrid<T>` / `Grid2D<T>` / `Grid3D<T>` / `HexGrid<T>` / `QuadTree<T>` /
`Octree<T>` / `KdTree<T>` / `DynamicAabbTree<T>` / `Graph<TNode>`（BFS・DFS・
Dijkstra・A*・トポロジカルソート）/ `Trie<TValue>` / `IntervalTree<TValue>`

### 時間・履歴・状態

`TimerCollection` / `ScheduledEventQueue` / `SnapshotHistory<T>` / `Blackboard` /
`TypeMap` / `UndoRedoStack` / `PushdownStateStack<TState>` / `ExpiringCache<K,V>`

### リアクティブ

`ObservableList<T>` / `ObservableDictionary<K,V>` / `ReactiveProperty<T>` /
`VersionedValue<T>` / `TypedEventBus`

### Unity ライフサイクル

`UnityObjectMap<K,V>` / `SafeList<T>` / `DisposableBag` / `GameObjectPool<T>` /
`ComponentCache<T>` / `SceneScopedRegistry<T>`

### 非同期・スレッド間

`MainThreadQueue` / `AsyncQueue<T>`（Unity 6 の `Awaitable`）/ `DoubleBuffer<T>` /
`SpscRingBuffer<T>`

### ゲームプレイ

`WeightedRandomList<T>`（Alias 法で O(1) 抽選）/ `ShuffleBag<T>` /
`GameplayTag` + `GameplayTagContainer`（階層タグ）/ `InventoryGrid<TItem>` /
`TopNBuffer<TElement,TScore>` / `LoopingList<T>`

---

## 設計上の約束

- **確保しない列挙** — 全コンテナが構造体列挙子を持ち、`IEnumerable<T>` は明示的実装。
  `foreach` でボクシングが起きない。
- **`ref` で返す** — 配列を土台にしたコンテナは要素を `ref` で返す。
  構造体を入れてもコピーが発生しない（構造体自身が持つ `FixedList8<T>` は
  C# の制約 CS8170 により例外で、値返しになる）。
- **外部依存ゼロ** — `com.unity.collections` も含め、追加パッケージを要求しない。
- **`unsafe` を使わない** — asmdef の設定を変えずに導入できる。

---

## ライセンス

（配布形態に合わせて記載してください。Unity Asset Store 経由の場合は
Asset Store EULA が適用されます。）

第三者コードは含んでいません。
