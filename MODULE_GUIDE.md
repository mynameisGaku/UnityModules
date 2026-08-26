# モジュール設計・案内ガイド

この文書は、モジュールをどこまでまとめるか、利用者へどの名前で見せるか、README に何を書くかを統一する。

## 配布単位の決め方

内部の計算処理は単独テストできる小ささを保つ。一方、配布パッケージは「利用者が同じ目的で一緒に探し、一緒に導入する機能」をまとめる。

次の条件を満たす場合だけ、独立したモジュールとして配布する。

- 単独で導入・更新・削除する現実的な理由がある。
- Unity API、外部パッケージ、所有者、寿命のいずれかが他と明確に異なる。
- README の「何が楽になるか」を一文で説明できる。
- 単独サンプルで、導入前後の違いを確認できる。

純粋計算を単体テストできることだけを理由に、配布パッケージを分けない。

## 利用者向けの名前

READMEとモジュール一覧では、日本語で目的を先に示す。Package Managerの`displayName`は英語の技術名を使い、README側で日本語の利用目的と対応付ける。

ファイル名、フォルダー名、型名、メンバー名、名前空間、asmdef名、UPM識別子は英語のASCII名を使う。型・関数・fieldの役割、入力、失敗条件を説明するソース内コメントは、利用者が保守しやすい簡潔な日本語で書く。公開API名、Unity API名、規格名は原文の英語を保つ。利用者へ表示するlog、test名、sample UIは既存packageの言語を維持し、同一画面や同一test fixture内で混在させない。

| 利用者向け表示名 | 技術名 | 名前から分かること |
|---|---|---|
| モジュール導入アシスタント | Module Installer | 用途別セットから必要なモジュールをまとめて導入する。 |
| プロジェクト初期設定 | Project Setup | 新規Projectで繰り返す設定とTag・Layer・Sorting Layer・3D/2D Layer Collisionをprofileからまとめて適用する。 |
| Assembly依存チェック | Assembly Dependency Audit | asmdefの参照元・参照先、循環、PlayerからEditorへの逆参照、asmref target整合性、同じfolderのassembly owner候補競合をread-onlyで確認する。 |
| Localization key監査 | Localization Key Audit | required Localeのdirect coverageとtable integrity、宣言済みscopeの静的参照をread-onlyで確認する。 |
| シーン切り替え | SceneFlow | Scene の読込・追加・切替・解放を扱う。 |
| 画面フェード | ScreenTransition | 画面を覆う・戻す演出を扱う。 |
| ゲーム時間制御 | TimeControl | 一時停止・スロー・倍速を扱う。 |
| 入力の一時停止 | InputGate | Gameplay の入力を一時的に止める。 |
| プロジェクト不備確認・修復 | BuildGuard | build対象・直接選択したSceneと選択PrefabのMissing Scriptなどを見つけ、Prefabの構造変更を別flowでreviewする。 |
| 不具合レポート保存 | DiagnosticsContext | 調査用の状態とログを JSON に残す。 |
| Player設定 | Player Options | 音量・表示・品質・frame rateの保存とUnityへの適用を分けて扱う。 |
| 入力補助 | Input Assist | スティック値の補正とbutton gestureをまとめて扱う。 |
| 入力デバイス表示 | Input Device Display | 最後に実入力したdeviceを表示向けfamilyへ分類する。 |
| 入力コマンド判定 | Input Command | 先行入力・順序・同時押し・対向軸を明示tickで判定し、優先順位選択と入力安定化も扱う。 |
| ゲーム判定・計算 | Gameplay Rules | ゲームルールから使う決定論的な数値計算をまとめて扱う。 |
| 再現可能シミュレーション | Deterministic Simulation | 固定刻み・乱数・記録・状態ハッシュで再現性を作る。 |
| オブジェクト再利用 | Object Pool | prefabの生成をpoolへ集約し、spawn・release・統計を明示APIで扱う。 |
| 振動の統一 | Haptics | 端末差のある振動APIをintentとcapabilityの背後へ隠す。 |
| 実行速度計測 | Perf Meter | frame時間と簡易メモリを実行中に数値で計測する。 |

`Control`、`Flow`、`Resolver`、`Evaluator` のような実装上の語だけを表示名にしない。「利用者が何をできるか」を名前にする。

## 統合する領域

公開済みタグと UPM 識別子は削除せず、既存利用者が旧配布単位を継続利用できる入口として残す。
統合時に C# の名前空間・型名・memberを維持して source / API 互換にしても、runtime assembly 名が変わるため binary 互換ではない。
移行時は自作 asmdef の References を更新し、旧 assembly を参照する precompiled DLL を再buildする。旧配布単位と統合後パッケージは同時導入しない。
統合後の推奨入口は次のとおり。

### 入力補助（Input Assist）

入力値の補正、平滑化、方向分割、応答curve、閾値判定、長押し・連打・多重tapを一つの導入単位へまとめる。
Unity 側から `Vector2` と `deltaTime` を渡す `float` 契約と、確保を伴わない `double` 契約の両方を同じパッケージが持つ。

### 入力コマンド判定（Input Command）

先行入力、順序判定、同時押し、優先順位、対向軸解決、チャタリング除去をまとめる。
明示 tick を使う段、sample 回数で進む安定化、状態を持たない優先順位選択は独立した部品として提供する。
共通 facade や自動 pipeline は作らず、関連機能を一つの導入単位と runtime assembly から選んで使えるようにする。

`InputGate` は Input System の実行状態を所有するため、「入力の一時停止」として独立を維持する。

### 入力デバイス表示（Input Device Display）

Input Systemのglobalな実入力から最後に操作されたdeviceを表示familyへ分類し、UIが文字・glyph・styleを選ぶための状態を提供する。
Input Assistの値整形やInputGateのAction Map停止とはownerと寿命が異なるため、独立packageとして維持する。
manufacturer文字列の推測、入力消費、rebind、pairing、player別追跡、glyph assetの所有は行わない。

### Assembly依存チェック（Assembly Dependency Audit）

`Assets`と導入済み`Packages`のasmdefをread-onlyで走査し、参照元・assembly・参照先の3列graphと構造上の問題を表示する。
asmrefは別一覧で不正JSON、欠落・未解決・曖昧なtargetを検査し、同じfolderのasmdef／asmref owner候補競合は各assetへ報告する。asmdef依存graphへ推測したedgeは追加しない。
Project Setupはasmdefの作成までを所有し、このmoduleは作成後の参照関係だけを監査するため、変更責務を分離して独立packageとして維持する。
未使用参照やcompile時間の推定、asmdef／asmrefの書換え、build停止は行わない。

### Localization key監査（Localization Key Audit）

Unity LocalizationのShared Table Dataをtyped loadする前にraw serialized representationを検証し、String／Asset Table ownerを分類してからrequired LocaleのString direct table／entry／value、duplicate・orphan integrity、1回につき宣言済み`Assets`または1つのregistered packageだけをrootとするGUID＋key ID参照を手動で表示する。同じroot内では複数pathを宣言できる。
Package scopeは登録名を`PackageInfo.resolvedPath`へexactに対応付け、logical pathだけを結果へ残す。bare `Packages`、直接指定した`Library/PackageCache`、未登録名、root混在、曖昧なshort-name pathをfilesystem access前に拒否し、normalized duplicate target、root／ancestor／child reparse、root escapeではpartial resultを返さない。physical pathはUI、結果、error、clipboardへ露出せず、読取errorはlogical pathとexception typeだけを示す。
欠落または空のcollection GUIDをloadするとUnity Localization 1.5.12がassetをdirtyにし得るため、raw preflightに失敗した場合はtyped APIを呼ばず監査全体を停止する。
監査result全体のfindingを`Terminal`、`Required Locale Coverage`、`Static References`、`Integrity`へexact 1つずつ分類し、Search、Category filter、500件表示上限に依存しない件数として示す。resultまたはcoverageがincompleteなら、0件のカテゴリも安全や問題なしの証明には使わない。
fallback後のruntime翻訳可否やkeyの未使用は断定せず、build callback、autofix、entry追加、値の書換え、削除は行わない。
Asset Tableのentryやlocalized assetはdirect coverageせず、Asset Tableだけが所有するShared Table DataをString key findingへ混在させない。

### Player設定（Player Options）

音量、quality、resolution、window mode、refresh rate、target frame rateを一つの型付きsnapshotとして扱い、Load・Set・Apply・Saveを別操作にする。
application bootstrapがserviceを一つ明示所有し、singleton、自動GameObject、static eventは作らない。
SaveSystemのslot・backup・破損復旧、AudioControlのvoice pool、Input Systemのrebindとは責務を分ける。PlayerPrefsの強い耐久性、key binding、cloud同期、vSync変更は対象外にする。

### オブジェクト再利用（Object Pool）

1つのprefabを上限付きidleとして保持し、spawn・release・preload・trimを明示APIで扱う。
application ownerがpoolを生成して所有し、singleton、static event、自動GameObjectは作らない。
AudioControlのAudioSource専用pool、Containersの純粋データ構造、DeterministicSimulationのhandle poolとは所有する対象が異なるため独立packageとする。複数prefabのregistry、非同期load、addressable連携は対象外にする。

### 振動の統一（Haptics）

intent指定の再生要求とdriver capabilityの報告に分け、Android・iOS・Desktopの差をserviceの背後へ隠す。
ネイティブプラグインを同梱しない実装範囲で動作し、capabilityが無い環境では安全に無動作になる。
queue、scheduling、Core Haptics波形、デバイスごとの個別調整は対象外にする。

### 実行速度計測（Perf Meter）

有界リングバッファのframe時間統計、spike計数、簡易メモリsnapshotを提供する。
GameplayRulesのstatistics群は値列だけを受け取る純粋計算であり、このmoduleはframe取得のownershipを持つ点で責務が異なるため独立packageとする。GPU時間、Profiler marker分析、ログ蓄積と出力、alert通知は対象外にする。

### ゲーム判定・計算（Gameplay Rules）

リソース、コスト、数値条件、能力補正、スコア選択、ダメージ軽減、敵対度、スタック、定期処理など、
ゲームルールから使う純粋計算を用途別の名前空間にまとめる。名前空間が配布境界をまたがない状態を保つ。

### 再現可能シミュレーション（Deterministic Simulation）

固定刻み時計、再現可能な乱数、固定小数点、入力記録、正規化データ、状態ハッシュ、世代付きハンドルをまとめる。

統合後も内部クラスは単独実行・単独テスト可能に保つ。まとめるのは責務ではなく、利用者が導入する単位である。

### 統合の判定基準

同じ名前空間が複数の配布パッケージに分かれている状態は、統合が必要な合図として扱う。
配布パッケージを分けるのは、単独で導入・更新・削除する理由がある場合だけに限る。

### 導入入口

新規利用者には、公開tagを個別に探させず「モジュール導入アシスタント」の用途別セットを案内する。
入力補助、入力コマンド判定、ゲーム判定・計算、再現可能シミュレーションは、そのセットからまとめて追加できる状態を保つ。
既存tagと個別導入は旧配布単位を継続利用する入口として残す。統合後パッケージとの同時導入は不可とする。

## README の必須構成

各 README は次の順序にする。

1. **30 秒で分かる説明** — Unity で何が面倒で、このモジュールが何を代行するか。
2. **できること** — 利用者が得る結果を 3〜6 項目で示す。
3. **使わない方がよい場合** — 責務外の機能を短く示す。
4. **3 分で試す** — Package Manager からの導入、Sample import、必要な設定を番号順で示す。
5. **最小コード** — コピーして動かせる一つの例だけを先に示す。
6. **実行するとどうなるか** — 成功時の Scene、Inspector、Console、生成ファイルなどを示す。
7. **よくある問題** — Missing reference、設定不足、対応 version、他 module との違いを示す。
8. **詳しい契約** — 公開 API、失敗条件、非対象、テスト範囲を後半へ置く。

冒頭で型名を列挙しない。最初に「何が楽になるか」を伝え、型名と厳密な契約は必要になった利用者が後半で読めるようにする。

## 新規モジュールの優先基準

今後は、次のいずれかを直接減らす機能を優先する。

- Inspector や Project Settings の手作業。
- Scene、Prefab、Build 設定の見落とし。
- 端末・解像度・入力方式による Unity 固有の差異。
- 毎回同じ MonoBehaviour や Editor script を書く作業。
- 実機や Player build まで進まないと発見しにくい問題。

Project SettingsとTag・Layer・Sorting Layer・Physics／Physics 2D Layer Collisionの反復作業は「プロジェクト初期設定」へまとめる。Build Guardの壊れた参照検査・修復とPrefab構造変更review、Module Installerのpackage導入とは責務を分け、設定の差分Preview・backup・適用・復元を一つの作業単位として提供する。
