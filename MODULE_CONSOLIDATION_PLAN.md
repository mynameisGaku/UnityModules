# モジュール統合・追加機能の検討

`dev` の 65 パッケージを対象に、重複の実測結果と統合案、重複しない追加機能候補をまとめる。
統合は本文書の案 A〜C を実施済みで、案 D・E と追加機能は未着手。
判断基準は [モジュール設計・案内ガイド](MODULE_GUIDE.md) の「配布単位の決め方」に従う。

---

## 1. 現状の実測

| 指標 | 値 |
|---|---|
| UPM パッケージ数 | 65 |
| asmdef 数 | 253（1 パッケージあたり約 3.9） |
| Sample Scene 数 | 62 |
| Module Installer の catalog 掲載数 | 43（**21 モジュールが未掲載**） |
| `*Error` enum の種類 | 56（ほぼ 1 パッケージ 1 個） |
| `IsFinite` の private 再実装 | 28 パッケージ |

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
- **入力コマンド判定（Input Command）** — `ulong tick` + `int commandId` の決定論的なコマンド認識。
  収容：InputCommandBuffer, InputSequenceMatcher, InputChordMatcher, InputCommandArbiter,
  InputAxisConflictResolver, InputStabilizer（6 個）

後者は**統合すると機能が増える**点が重要。6 個は全て `ulong tick` と `int commandId` を扱うのに、
現状は共有型が無いため「stabilizer の出力を buffer に入れ、sequence matcher に流し、arbiter で優先度解決する」という
本来の使い方をするたびに利用者が変換コードを書いている。1 パッケージにすれば pipeline として繋がる。

`InputGate` は Input System の実行状態を所有するので MODULE_GUIDE 通り独立を維持する。

### 案 B：ゲーム判定・計算 19 → 1

19 パッケージ・6,339 行を 1 パッケージへまとめ、既存の namespace をそのまま区分として使う
（`GameplayResources` / `GameplayStats` / `GameplaySelection` / `GameplayDecision` / `GameplayProgression` /
`GameplayAnalysis` / `GameplayTiming` / `GameplayAllocation` / `GameplayDamage` / `GameplayRules`）。

統合により解消される具体的な重複：

- `RollingSampleWindow.Snapshot` が min / max / mean を計算しており、`SampleStatistics` の部分集合になっている。
- `ResourceMeter`（状態を持つ消費）と `ResourceCostEvaluator`（状態を変えない可否判定）が同一 namespace で分離している。
- `StableScoreSelector` と `UtilityScoreEvaluator` は「候補から 1 つ選ぶ」の前段・後段で、単独では使い道が狭い。
- 19 個の `*Error` enum が統一できる。

利用者から見た導入単位は「ゲームルールの数値計算が要る」の 1 つで、個別に導入・更新・削除する理由が無い。

### 案 C：再現可能シミュレーション 7 → 1

SimulationClock, DeterministicRandom, StateFingerprint, ReplayTape, CanonicalPayload, FixedPoint, GenerationalHandle。

これらは単独では意味を成さない。Replay を実現するには clock・random・fingerprint・tape が全て要る。
Module Installer の `deterministic-simulation` bundle が既に 7 個セットで案内しており、
配布単位が bundle と一致していない。

### 案 D：Editor モジュールの共通基盤（コピペの実害）

ProjectSetup / SceneWorkspace / PlayModeTuning / AssetImportAudit / BuildAssistant は
「Preview → Backup → Apply → 適用後検証 → 失敗時復元」という同じ骨格を 5 回書いている。

- `SceneWorkspaceExecutionGuard` と `BuildAssistant/ExecutionGuard` は実質同一コード
  （`Interlocked.CompareExchange` による単一実行 lease）。
- `SceneWorkspaceFingerprint` と `PlayModeTuningFingerprint` はどちらも
  「length-prefixed トークン列の canonical SHA-256」で、これは Runtime 側の `StateFingerprint` と同じ考え方の 3 重実装。
- `*Plan` / `*Planner` / `*PlanRegistry` / `*Snapshot` / `*ApplyResult` / `*CaptureResult` / `*Change` /
  `*UiText` / `I*Gateway` / `Unity*Gateway` の型構成が SceneWorkspace と PlayModeTuning でほぼ一致。

**提案**：利用者向けパッケージは分けたまま、内部共有パッケージ
`com.studiogaku.editor-change-plan`（Plan / Snapshot / Fingerprint / ExecutionGuard / 差分 Preview 表示）を作り、
5 モジュールの依存にする。Module Installer が依存を解決するので利用者の手順は増えない。

あわせて **SceneWorkspace と PlayModeTuning は 1 パッケージに統合**してよい。
どちらも「Editor の状態を Profile 化して安全に戻す」で、探すときも導入するときも一緒になる。

### 案 E：Containers と個別モジュールの重複

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

概念ごとに置き場所を 1 つに決める。判断の目安は決定論要件の有無で、
`ulong tick` で再現性を保証する必要があるものは再現可能シミュレーション側、
そうでない汎用データ構造は Containers 側に寄せる。
`GenerationalHandle` は `Containers.SlotMap` と役割が完全に重なるため、Containers へ寄せて独立配布を終了するのが妥当。

### 統合しない方がよいもの

ProjectSetup / BuildAssistant / BuildGuard / AssetImportAudit / ReferenceFinder / ModuleInstaller /
SceneFlow / ScreenTransition / AdaptiveLayout / TimeControl / StartupFlow / SaveSystem / AudioControl /
DiagnosticsContext / InputGate / Inspector / Drawing / Containers。

いずれも所有する Unity API・寿命・導入理由が独立しており、MODULE_GUIDE の 4 条件を単独で満たす。

### 統合後の姿

**65 → 27 パッケージ**（入力 19→2、ゲーム計算 19→1、決定論 7→1）。
案 D の SceneWorkspace + PlayModeTuning 統合と内部共有パッケージ、案 E の Containers 重複整理は未実施で、
それらを行うと 24 パッケージになる。

---

## 3. 移行手順（既存利用者を壊さない）

MODULE_GUIDE の「公開済みタグと UPM 識別子は削除せず、既存利用者の互換入口として残す」に従う。

1. 統合先パッケージを新 UPM 識別子で公開し、既存 namespace と型名はそのまま残す。
   案 B・C は namespace が既に共通なので、利用者側のコード変更は `using` の追加も不要。
2. 旧パッケージの公開 tag は凍結する。削除も新 version 追加もしない。
3. 旧 README の冒頭に統合先を 1 行で案内する。
4. Module Installer の catalog を統合後の単位へ差し替える。
   **現在 catalog に載っていない 20 モジュール**（Containers, PlayModeTuning, 入力細分化 18 個）は、
   統合後の単位で初めて掲載できる状態になる。
5. README の「詳細モジュール一覧」と「旧入力モジュールとの関係」を更新する。

## 4. 統合と同時に直したい不整合

- **catalog 未掲載 21 件** — Containers と PlayModeTuning は README には載っているが Module Installer から導入できない。
- **不要な依存宣言** — 純粋計算 26 パッケージが `com.unity.modules.uielements` を宣言しているが、
  Runtime 側で `UnityEngine` を参照している file は 0。Sample Scene のためだけの依存が本体に付いている。
  統合すれば 26 個の誤った依存が 3 個に減る。
- **XML doc コメントの言語** — MODULE_GUIDE は「ソースコード、ソース内コメントは英語だけ」と定めているが、
  Runtime / Editor 配下に日本語コメントを含む file が 55 パッケージに存在する
  （Containers 81 file、Inspector 50 file が最多）。統合時に方針をどちらかへ確定させる。

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

統合前の 44 個の公開tagは削除しない。既存利用者の `?path=/<Folder>#<tag>` はそのまま動き続ける。

`Containers` は公開tagが 1 つも無いため、まだ catalog へ載せられない。
`containers-v1.0.0` を作成した時点で catalog へ追加する。
`ThreatScoreResolver` も単独tagが無く、`GameplayRules` の一部として初めて公開される。

---

## 5. 追加機能の候補（既存と重複しないもの）

MODULE_GUIDE の「新規モジュールの優先基準」に照らし、repo 全体を検索して**実装が 0 件**であることを確認した領域だけを挙げる。

### 優先度：高

**1. 入力デバイス切替の検出と表示切替（新規モジュール）**

`Gamepad.current` / `Keyboard.current` / `Touchscreen` の参照が repo 全体で 0 件。
InputAssist は入力値の整形、InputGate は停止で、「今どのデバイスで遊んでいるか」を扱うモジュールが無い。
最後に操作されたデバイスの種別（Keyboard / Xbox / PlayStation / Switch / Touch）を判定し、
UI のボタン表記を差し替える口を提供する。優先基準の「入力方式による Unity 固有の差異」に直接あたり、
実機で持ち替えるまで気づかない典型的な問題を潰せる。

**2. ユーザー設定（オプション）の保存（新規モジュール）**

`Screen.SetResolution` 0 件、`Application.targetFrameRate` 0 件、`QualitySettings` は Drawing の色空間判定 1 箇所のみ。
音量・解像度・リフレッシュレート・品質・キーコンフィグを、型付き default・version migration・変更通知つきで扱う。
PlayerPrefs を直に使うと毎回 default と migration を書き直すことになる。
SaveSystem はゲーム進行の slot・破損検出・backup が責務なので重複しない。

**3. 物理レイヤー衝突マトリクスの適用（ProjectSetup へ追加）**

`ProjectSetupSettingKey` は Tags / Layers / SortingLayers まで対応済みだが、
Physics・Physics2D の Layer Collision Matrix が未対応。
Layer を profile から作った直後に、Inspector のチェックボックスを手作業で押す作業だけが残っている。
新規パッケージではなく ProjectSetup の setting key 追加が正しい形。

**4. asmdef 依存の可視化と循環検出（新規モジュール）**

ProjectSetup が asmdef を作るところまでは面倒を見るが、その後の依存関係の劣化
（循環参照、Editor→Runtime の逆参照、不要な参照によるコンパイル時間の増加）を見る手段が無い。
「実機や Player build まで進まないと発見しにくい問題」に該当する。
asmdef 249 個を持つこの repo 自体が最初の利用者になる。

### 優先度：中

**5. Prefab override の逸脱検査（BuildGuard へ追加）**

BuildGuard は Missing Script と削除済み Object Reference を見るが、
Scene 上の Prefab instance に意図せず残った override は見ていない。
「Scene・Prefab・Build 設定の見落とし」に該当し、既存の scanner 構成へ検査項目として足せる。

**6. build 前チェックの一本化（BuildGuard × BuildAssistant の接続）**

BuildGuard に `BuildGuardPreflightProcessor` がある。
BuildAssistant の Preview 段階で、BuildGuard の検査・AssetImportAudit の逸脱・ProjectSetup の設定差分を
1 つの preflight 結果としてまとめて出す。新規モジュールを作らずに、既にある 3 つを繋ぐだけで効果が出る。

**7. 未翻訳・未使用の文字列キー検査（新規 Editor モジュール）**

Localization 関連の実装は repo 全体で 0 件だが、翻訳の仕組み自体は Unity 公式パッケージがあるので作らない。
重複しないのは「キーの過不足を build 前に検出する Editor 検査」の側。

### 見送り

- Tween / Easing、状態遷移、経路探索、Timeline 連携 — Unity 公式または既存資産と重複しやすく、
  「Unity 固有の面倒を減らす」という優先基準から外れる。
- 実機 HUD（FPS・メモリ・GC） — Unity Profiler と重複気味。
  DiagnosticsContext の JSON へ計測値を足す方が責務が濁らない。
