# Changelog

このパッケージの変更履歴。書式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョン番号は [Semantic Versioning](https://semver.org/lang/ja/) に従う。

## [1.0.0] — 未リリース

### Added

初回リリース。66 個のコンテナを 8 カテゴリで収録。

**コア（GC フリー / 高速）**
`FastList<T>` / `RingBuffer<T>` / `Deque<T>` / `PriorityQueue<TElement,TPriority>` /
`IndexedPriorityQueue<TElement,TPriority>` / `SparseSet<T>` / `SparseIntSet` / `SlotMap<T>` /
`BitSet` / `FixedBitSet64` / `ChunkedList<T>` / `RentedArray<T>` / `TempList<T>` /
`MultiMap<TKey,TValue>` / `BiMap<TLeft,TRight>` / `DefaultDictionary<TKey,TValue>` /
`Counter<T>` / `LruCache<TKey,TValue>` / `StringId` / `StringKeyMap<TValue>` / `FixedList8<T>`

**シリアライズ対応**
`SerializableDictionary<TKey,TValue>` / `SerializableHashSet<T>` / `Optional<T>` /
`SerializableType` / `SubclassSelectorAttribute` / `FloatRange` / `IntRange` /
`SerializableGuid` / `InterfaceReference<TInterface>` / `EnumMap<TEnum,TValue>` /
`RuntimeSet<T>` / `LayeredConfig<TKey,TValue>` / `SerializableStack<T>` / `SerializableQueue<T>`

**空間・グラフ**
`SpatialHashGrid<T>` / `Grid2D<T>` / `Grid3D<T>` / `HexGrid<T>` / `Hex` / `QuadTree<T>` /
`Octree<T>` / `KdTree<T>` / `DynamicAabbTree<T>` / `Graph<TNode>` / `Trie<TValue>` /
`IntervalTree<TValue>`

**時間・履歴・状態**
`TimerCollection` / `ScheduledEventQueue` / `SnapshotHistory<T>` / `Blackboard` /
`BlackboardKey<T>` / `TypeMap` / `UndoRedoStack` / `PushdownStateStack<TState>` /
`ExpiringCache<TKey,TValue>`

**リアクティブ**
`ObservableList<T>` / `ObservableDictionary<TKey,TValue>` / `ReactiveProperty<T>` /
`VersionedValue<T>` / `TypedEventBus`

**Unity ライフサイクル**
`UnityObjectMap<TKey,TValue>` / `SafeList<T>` / `DisposableBag` / `GameObjectPool<T>` /
`ComponentCache<T>` / `SceneScopedRegistry<T>`

**非同期・スレッド間**
`MainThreadQueue` / `AsyncQueue<T>` / `DoubleBuffer<T>` / `SpscRingBuffer<T>`

**ゲームプレイ**
`WeightedRandomList<T>` / `ShuffleBag<T>` / `GameplayTag` / `GameplayTagContainer` /
`InventoryGrid<TItem>` / `TopNBuffer<TElement,TScore>` / `LoopingList<T>`

**エディタ拡張**
`SerializableDictionary` / `SerializableHashSet` / `Optional` / `SerializableType` /
`SubclassSelector` / `MinMaxSlider` / `SerializableGuid` / `InterfaceReference` /
`EnumMap` / `GameplayTag` の PropertyDrawer。
