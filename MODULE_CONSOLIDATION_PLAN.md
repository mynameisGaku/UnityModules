# モジュール統合・追加機能の検討

`dev` に当初存在した 65 パッケージを対象に、重複の実測結果と統合判断、重複しない追加機能候補をまとめる。
案 A の実施後は 60 パッケージ、案 B・C と Input Assist への吸収後は 24 パッケージとなった。
案 D・E は追加検討の結果、配布単位の変更としては採用しない。追加機能はProjectSetupのLayer衝突設定、InputDeviceDisplay、AssemblyDependencyAudit、PlayerOptions、LocalizationKeyAuditに加え、ObjectPool、Haptics、PerfMeterも実装している。
判断基準は [モジュール設計・案内ガイド](MODULE_GUIDE.md) の「配布単位の決め方」に従う。

---

## 1. 統合前の実測

次の値は、統合計画を作成した 65 パッケージ時点の基準値である。現在値ではない。

| 指標 | 値 |
|---|---|
| UPM パッケージ数 | 65 |
| asmdef 数 | 253（1 パッケージあたり約 3.9） |
| Sample Scene 数 | 62 |
| Module Installer の catalog 掲載数 | 43（**21 モジュールが未掲載**） |
| `*Error` enum の種類 | 56（ほぼ 1 パッケージ 1 個） |
| `IsFinite` の private 再実装 | 28 パッケージ |

### 実施結果

| 時点 | UPM パッケージ数 | 内容 |
|---|---:|---|
| 統合前 | 65 | 初期実測 |
| 案 A の Input Command 統合後 | 60 | 6 パッケージを 1 パッケージへ統合 |
| 案 A〜C 完了後 | 24 | Input Assist への 12 パッケージ吸収、Gameplay Rules と Deterministic Simulation の新設を含む |

現在の `dev` は 31 パッケージ、196 asmdef、Module Installer の catalog は 22 entry である。InputDeviceDisplay、AssemblyDependencyAudit、PlayerOptions、LocalizationKeyAudit、ObjectPool、Haptics、PerfMeterは公開tag作成前のためcatalogへ先行登録していない。
24 パッケージへの到達は案 A〜C の結果であり、案 D・E による削減を含まない。

### 純粋計算パッケージが全体の 3 分の 2

| 群 | パッケージ数 | Runtime 実装行数 | パッケージ内の総 file 数 |
|---|---|---|---|
| 入力細分化 | 18 | 3,901 | 842 |
| ゲーム判定・計算 | 19 | 6,339 | 982 |
| 再現可能シミュレーション | 7 | 2,570 | 338 |
| **合計** | **44**（全体の 68%） | **12,810** | **2,162** |

実装 1 行あたり packaging file が 0.17 個ある。README・CHANGELOG・LICENSE・package.json・asmdef 4 種・Sample Scene・.meta が
200〜500 行の計算クラスごとに複製されている状態で、MODULE_GUIDE の
「純粋計算を単体テストできることだけを理由に、配布パッケージを分けない」に反している。

### 統合が必要な最も明確な証拠：名前空間が既にパッケージ境界をまたいでいる

同一 namespace が別々の assembly に分割されている箇所が 5 つある。コード側は既に「同じ塊」だと宣言している。

| namespace | 定義しているパッケージ |
|---|---|
| `GameplayResources` | ResourceMeter, ResourceCostEvaluator |
| `GameplayDecision` | StableScoreSelector, UtilityScoreEvaluator |
| `GameplayTiming` | ChargeCooldown, PeriodicTickPlanner |
| `GameplayAnalysis` | LinearTrendEstimator, SampleStatistics |
| `InputSmoothing` | InputVectorSlewLimiter, InputVectorDirectionLimiter |

---

## 2. 統合案

### 案 A：入力系 19 → 2（最優先）

`InputAssist` は既に細分化モジュールの機能を内部で再実装している。

| InputAssist の実装 | 同じ計算をしている独立パッケージ |
|---|---|
| `InputVectorFilter.ApplyDeadZoneAndCurve` | InputRadialDeadZone |
| `InputVectorFilter.ApplyResponse` | InputVectorResponseCurve |
| `InputVectorFilter` の rise/fall 速度制限 | InputVectorSlewLimiter, InputVectorExponentialSmoother |
| `InputVectorFilter.ClassifyDirection` | InputDirectionQuantizer（`InputDirectionMode` は型名まで一致） |
| `InputButtonTracker` の hold 判定 | InputPressClassifier |
| `InputButtonTracker` の repeat | InputRepeat |
| `InputButtonTracker` の multi-tap | InputMultiTapClassifier |

契約は違う（InputAssist は `float` + `deltaTime` + `[SerializeField]`、細分化側は `double` / `ulong tick` の決定論版）。
これは分ける理由になるが、**分ける単位は 2 つで足りる。19 個は過剰**。

**統合後**

- **入力補助（Input Assist）** — Unity 側から `Vector2` と `deltaTime` を渡す用途。
  吸収：InputRadialDeadZone, InputVectorResponseCurve, InputVectorSlewLimiter, InputVectorExponentialSmoother,
  InputVectorDirectionLimiter, InputVectorWeightedMixer, InputDirectionQuantizer, InputQuantizer,
  InputThresholdClassifier, InputPressClassifier, InputRepeat, InputMultiTapClassifier（12 個）
- **入力コマンド判定（Input Command）** — 明示 tick で進む判定、sample 回数で進む安定化、状態を持たない優先順位選択をまとめた入力コマンド部品群。
  収容：InputCommandBuffer, InputSequenceMatcher, InputChordMatcher, InputCommandArbiter,
  InputAxisConflictResolver, InputStabilizer（6 個）

6 個は入力コマンド判定という導入目的を共有するが、共通の `tick` / `commandId` signature を持つ 1 本の pipeline ではない。
Buffer、Sequence Matcher、Chord Matcher、Axis Conflict Resolver は明示 tick を使い、Stabilizer は sample 回数で進み、
Arbiter は状態を持たない静的選択である。各段は独立したまま必要なものを選んで使い、入出力が異なる段を繋ぐ adapter は利用側が持つ。
統合の効果は新しい facade の追加ではなく、関連する 6 機能を 1 回の導入と 1 つの runtime assembly で利用できることにある。

`InputGate` は Input System の実行状態を所有するので MODULE_GUIDE 通り独立を維持する。

### 案 B：ゲーム判定・計算 19 → 1

19 パッケージ・6,339 行を 1 パッケージへまとめ、既存の namespace をそのまま区分として使う
（`GameplayResources` / `GameplayStats` / `GameplaySelection` / `GameplayDecision` / `GameplayProgression` /
`GameplayAnalysis` / `GameplayTiming` / `GameplayAllocation` / `GameplayDamage` / `GameplayRules`）。

統合により解消される具体的な重複：

- `RollingSampleWindow.Snapshot` が min / max / mean を計算しており、`SampleStatistics` の部分集合になっている。
- `ResourceMeter`（状態を持つ消費）と `ResourceCostEvaluator`（状態を変えない可否判定）が同一 namespace で分離している。
- `StableScoreSelector` と `UtilityScoreEvaluator` は「候補から 1 つ選ぶ」の前段・後段で、単独では使い道が狭い。
- 19 個の公開 `*Error` enum はsource / API互換のため維持しつつ、配布とassembly境界を1つに集約できる。

利用者から見た導入単位は「ゲームルールの数値計算が要る」の 1 つで、個別に導入・更新・削除する理由が無い。

### 案 C：再現可能シミュレーション 7 → 1

SimulationClock, DeterministicRandom, StateFingerprint, ReplayTape, CanonicalPayload, FixedPoint, GenerationalHandle。

これらは単独では意味を成さない。Replay を実現するには clock・random・fingerprint・tape が全て要る。
Module Installer の `deterministic-simulation` bundle が既に 7 個セットで案内しており、
配布単位が bundle と一致していない。

### 案 D：Editor モジュールの共通基盤（検討結果：見送り）

ProjectSetup / SceneWorkspace / PlayModeTuning / AssetImportAudit / BuildAssistant は
「Preview → Backup → Apply → 適用後検証 → 失敗時復元」という同じ骨格を 5 回書いている。

- `SceneWorkspaceExecutionGuard` と `BuildAssistant/ExecutionGuard` は実質同一コード
  （`Interlocked.CompareExchange` による単一実行 lease）。
- `SceneWorkspaceFingerprint` と `PlayModeTuningFingerprint` はどちらも
  「length-prefixed トークン列の canonical SHA-256」で、これは Runtime 側の `StateFingerprint` と同じ考え方の 3 重実装。
- `*Plan` / `*Planner` / `*PlanRegistry` / `*Snapshot` / `*ApplyResult` / `*CaptureResult` / `*Change` /
  `*UiText` / `I*Gateway` / `Unity*Gateway` の型構成が SceneWorkspace と PlayModeTuning でほぼ一致。

共通化候補はあるが、この repository は各パッケージを Git URL の `?path=` で独立配布している。
未公開の同一 repository 内パッケージを共有依存へ加えると、現在の tag と catalog だけでは依存先の導入を保証できず、
利用者向けの導入入口と release 手順が増える。現時点では新しい共有パッケージを作らない。

SceneWorkspace は Scene 集合の復元、PlayModeTuning は Play 中の設定変更計画を所有し、変更対象と失敗時の復元契約が異なる。
用途別 bundle で一緒に案内できるため、配布パッケージは統合しない。共通化は各パッケージ内で同一契約を確認できる箇所に限定する。

### 案 E：Containers と個別モジュールの重複（検討結果：配布統合しない）

Containers（66 型・13,069 行）が、独立パッケージと同じ概念を別実装で持っている。

| Containers の型 | 同概念の独立パッケージ |
|---|---|
| `SlotHandle` / `SlotMap<T>` | **GenerationalHandle**（`GenerationHandle` / `GenerationHandlePool`。世代番号で解放済み handle を弾く点まで同一） |
| `RingBuffer<T>` | RollingSampleWindow, InputCommandBuffer の内部リング |
| `WeightedRandomList` | WeightedChoiceTable（Containers 側は Alias 法で O(1)） |
| `ScheduledEventQueue` | PeriodicTickPlanner |
| `TimerCollection` | ChargeCooldown, TimedStackResolver |
| `SnapshotHistory<T>` | ReplayTape |
| `InventoryGrid` | StackTransferPlanner |

表の型は概念が近くても、公開 API、状態表現、上限、決定論契約が同一とは限らない。
案 A〜C で旧個別パッケージはすでに統合先へ移っており、Containers へ再移動してもパッケージ数は減らず、
assembly 名をもう一度変えて移行負担を増やす。現時点では配布先を変更しない。
将来、完全に同じ契約と移行経路を実証できた型だけを、公開 API を保った内部実装の共通化候補として再評価する。

### 統合しない方がよいもの

ProjectSetup / BuildAssistant / BuildGuard / AssetImportAudit / ReferenceFinder / ModuleInstaller /
SceneFlow / ScreenTransition / AdaptiveLayout / TimeControl / StartupFlow / SaveSystem / PlayerOptions / LocalizationKeyAudit /
ObjectPool / Haptics / PerfMeter / AudioControl /
DiagnosticsContext / InputGate / Inspector / Drawing / Containers。

いずれも所有する Unity API・寿命・導入理由が独立しており、MODULE_GUIDE の 4 条件を単独で満たす。

### 統合後の姿

**65 → 60 → 24 パッケージ**となった。

- 65 → 60: Input Command が旧 6 パッケージを 1 つへ統合。
- 60 → 24: Input Assist が旧 12 パッケージを吸収し、Gameplay Rules が旧 19 パッケージ、
  Deterministic Simulation が旧 7 パッケージをそれぞれ 1 つへ統合。

案 D・E はこの数に含まれず、追加のパッケージ削減策としても採用しない。

---

## 3. 移行手順（既存利用者を壊さない）

MODULE_GUIDE の「公開済みtagは旧配布単位を継続利用する入口として残し、source / API互換とbinary互換を区別する」に従う。

1. 統合先パッケージを新 UPM 識別子で公開し、既存 namespace、型名、member、動作をそのまま残す。
   これは source / API 互換であり、assembly 名を維持する binary 互換ではない。
   自作 asmdef の References は統合後の runtime assembly 名へ変更し、旧 assembly を参照する precompiled DLL は再buildする。
2. 旧パッケージの公開 tag は凍結する。削除も新 version 追加もしない。
3. 旧 README の冒頭に統合先を 1 行で案内する。
4. Module Installer の catalog を統合後の単位へ差し替える。
   **統合前に catalog に載っていなかった 21 モジュール**（Containers, PlayModeTuning, 入力細分化 18 個, ThreatScoreResolver）は、
   統合後の単位で初めて掲載できる状態になる。
5. README の「詳細モジュール一覧」と「旧入力モジュールとの関係」を更新する。

## 4. 統合と同時に直したい不整合

- **catalog 未掲載 21 件** — Containers、PlayModeTuning、入力細分化18件、ThreatScoreResolverはModule Installerから導入できなかった。
- **Sample用の依存宣言** — 統合前のInput Command 6件、Gameplay Rules 19件、Deterministic Simulation 7件の計32packageは、
  engine-freeなRuntimeに対してSample Scene用の`com.unity.modules.uielements`を宣言していた。
  統合により同じ宣言は3packageへ集約されたが、Runtime実装の直接依存ではないことを文書で区別する。
- **ソース内コメントの言語** — 実装には日本語コメントが広く存在するため、MODULE_GUIDEを現行規約へ合わせた。
  識別子とpathは英語のASCII名を維持し、型・関数・fieldの役割、入力、失敗条件を説明するコメントは簡潔な日本語で書く。

---

## 4-2. dev から main へ上げる前に必要な作業

Module Installer の catalog は、統合後のパッケージを**まだ存在しない公開tag**で参照している。
`dev` では未公開tagを指していてよいが、`main` へ上げる前に次のtagを作成する必要がある。
tag が無いまま公開すると、catalog からの導入が失敗する。

| 作成する tag | 対象フォルダー |
|---|---|
| `input-assist-v2.0.0` | `InputAssist/` |
| `input-command-v1.0.0` | `InputCommand/` |
| `gameplay-rules-v1.0.0` | `GameplayRules/` |
| `deterministic-simulation-v1.0.0` | `DeterministicSimulation/` |
| `module-installer-v1.5.0` | `ModuleInstaller/` |

今回の統合対象で公開済みだった旧packageの43個のtagは削除しない。既存利用者の`?path=/<Folder>#<tag>`はそのまま動き続ける。
旧パッケージと統合後パッケージは同じ型を別 assembly に含むため、同一 project へ同時導入はしない。

`Containers` は公開tagが 1 つも無いため、まだ catalog へ載せられない。
`containers-v1.0.0` を作成した時点で catalog へ追加する。
`ThreatScoreResolver` も単独tagが無く、`GameplayRules` の一部として初めて公開される。

---

## 5. 追加機能の候補（既存と重複しないもの）

MODULE_GUIDE の「新規モジュールの優先基準」に照らし、repo 全体を検索して**実装が 0 件**であることを確認した領域だけを挙げる。

### 優先度：高

**1. 入力デバイス切替の検出と表示切替（InputDeviceDisplay 1.0.0で実装）**

Input Systemのglobalな実入力eventを観測し、最後に操作されたdeviceをKeyboard／Mouse、Xbox、PlayStation、Switch、一般Gamepad、Touchの表示familyへ分類する。
既知gamepadはInput Systemの型階層で判定し、project固有deviceは厳密layout overrideを標準分類より先に評価する。manufacturerやproductの自由記述文字列からは推測しない。
Input Assistの値整形、InputGateのAction Map停止、Input Systemのpairingやrebindを変更せず、利用側UIが所有する文字・glyph・画像・styleを選ぶための状態だけを提供する。
追跡はapplication全体で1つとし、player別追跡、入力消費、永続化、glyph assetは対象外にした。

**2. ユーザー設定（オプション）の保存（PlayerOptions 1.0.0で実装）**

音量・解像度・window mode・refresh rate・target frame rate・品質を一つの型付きsnapshotにし、Load・Set・Apply・Saveを別操作として実装した。
schema付きの単一JSON文書をPlayerPrefsへ保存し、破損値と未来schemaは自動上書きしない。品質はindexと一意な名前を照合し、Applyの部分結果とrollback失敗もfield maskで返す。
PlayerPrefsの強い耐久性、key binding、cloud同期、vSync変更は対象外とした。SaveSystemのslot・破損検出・backup、AudioControlのvoice pool、Input Systemのrebindとは責務が重複しない。

**3. 物理レイヤー衝突マトリクスの適用（ProjectSetup 1.16.0で実装）**

Physics・Physics2Dを別々の名前付きpair ruleとしてprofileへ追加した。
同じApplyで作るLayerは実slotを再取得してから解決し、Previewではpairごとの`Collide`／`Ignore`を表示する。
backup schema v16は名前のないslotを含む32行matrix全体を保持し、rollbackとRestoreで正確に戻す。
新規packageは増やさず、ProjectSetupのsetting keyとして責務を維持した。

**4. asmdef 依存の可視化と循環検出（AssemblyDependencyAudit 1.0.0で実装）**

ProjectSetupがasmdefを作る責務とは分け、`Assets`と導入済み`Packages`のasmdefをread-onlyで走査するEditor専用moduleを追加した。
参照元・assembly・参照先の3列graph、循環、未解決・曖昧・自己参照、Playerで有効なassembly→Editor専用assemblyの逆参照、platform指定の矛盾を表示する。
現在の196 asmdefを持つこのrepo自体を最初の利用者とし、未使用参照やcompile時間は推測せず、asmdefの書換えやbuild停止も行わない。

### 優先度：中

**5. Prefab override の逸脱検査（BuildGuard 1.5.0で実装）**

enabled build Sceneのoutermost connected Prefab instanceからAdded／Removed GameObject・Componentだけを抽出し、専用windowで最大1,000件の安定snapshotとしてreviewする。
Property Modificationは意図を判定しないため除外し、finding選択時は同じscannerでidentityを再確認してstaleな移動を拒否する。
Missing Script／Missing Object Referenceのbuild blockerとは接続せず、Apply、Revert、自動保存、dirty化、Player build停止を行わないmanual reviewへ限定した。

**6. build 前チェックの一本化（既存のactual build接続を維持）**

BuildAssistantは通常の`BuildPipeline.BuildPlayer`を呼ぶため、BuildGuardが導入済みなら`BuildGuardPreflightProcessor`と`BuildGuardSceneProcessor`がactual buildへ既に適用される。
BuildAssistantのPreviewへ別moduleの結果を集約すると、package間のhard dependency、staleな重複scan、blockerとadvisoryの混在を招くため追加実装は見送る。
AssetImportAuditは期待するimport policy、ProjectSetupは利用者が選んだprofileを前提とするmanual Previewとして独立を維持し、Structural Prefab Overrideもbuild blockerへ昇格しない。

**7. Localization keyのdirect coverage／integrity監査（LocalizationKeyAudit 1.0.0で実装）**

当初案の「未翻訳・未使用keyをbuild前に検出」は、locale fallback、dynamic lookup、Smart String内のnested参照、Addressablesや外部dataを網羅できず、安全に断定できないため採用しなかった。
代わりにUnity Localization 1.5.12をhard dependencyとするEditor専用moduleを追加し、明示されたrequired Localeのdirect table／entry／value、duplicate・orphan integrity、宣言済み`Assets` scopeで認識できるGUID＋key ID参照だけを手動監査する。
Shared Table Dataはtyped load前にraw serialized representationを全件preflightし、read-only保証を確立できない場合はtyped APIを呼ばず全体を停止する。結果はadvisoryであり、runtime翻訳可否やkeyの未使用を断定せず、build blocker、autofix、削除を行わない。

### 見送り

- Tween / Easing、状態遷移、経路探索、Timeline 連携 — Unity 公式または既存資産と重複しやすく、
  「Unity 固有の面倒を減らす」という優先基準から外れる。
- 実機 HUD（FPS・メモリ・GC） — Unity Profiler と重複気味。
  DiagnosticsContext の JSON へ計測値を足す方が責務が濁らない。
