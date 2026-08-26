# Unity作業を楽にするモジュール集

Unity で繰り返し発生する設定、実装、確認作業を減らすためのモジュール集。
利用者向けの名前は日本語で目的を示し、フォルダー名・名前空間・UPM 識別子には互換性のため英語の技術名を残している。

対応versionはpackageごとに異なります。30packageは**Unity 6000.5.7f1以降**、Containersは**Unity 6000.0以降**です。

---

## 困りごとから選ぶ

| 困りごと | 推奨モジュール | まずできること |
|---|---|---|
| Git URLを1件ずつ追加せず、用途別にモジュールをまとめて導入・更新したい | [モジュール管理（Module Manager）](ModuleInstaller/) | 普段使う4workflowから選び、用途・最初の操作・変更範囲を確認して導入する。専門向けcollectionと個別moduleは折りたたみから選べる。 |
| 新しいProjectごとに基本フォルダー、asmdef、Git設定、Player識別子、build方式、managed code削除強度、IL2CPP最適化、開始Scene、Layer衝突を手作業したくない | [プロジェクト一括設定（Project Setup）](ProjectSetup/) | `Assets`配下の基本フォルダー、Runtime・Editor・test用asmdef、Unity向け`.gitignore`と`.gitattributes`、Project Settings、build target別Application Identifier・Scripting Backend・API Compatibility Level・Managed Stripping Level・IL2CPP Code Generation、Root Namespace、Play Mode開始Scene、条件付きコンパイル記号、Tag・Layer、Physics／Physics 2D Layer Collision、Build Scenesをbackup付きでまとめて適用する。 |
| asmdefの参照元・参照先、循環、PlayerからEditorへの逆参照を確認したい | [Assembly依存チェック（Assembly Dependency Audit）](AssemblyDependencyAudit/) | `Assets`と導入済み`Packages`のasmdefをread-onlyで走査し、3列graphと構造上の問題を表示する。 |
| 必須Localeのtable・direct value不足とLocalization key参照の範囲を安全に確認したい | [Localization key監査（Localization Key Audit）](LocalizationKeyAudit/) | raw Shared Table Dataを先に検証し、String／Asset Table ownerを分離してrequired LocaleのString direct coverage、table integrity、宣言済み`Assets` scopeのGUID＋key ID参照をread-onlyで表示する。未使用やruntime翻訳可否は断定しない。 |
| Build Profile・Scene・出力先を毎回確認し、ビルド結果や容量差を手作業で残したくない | [ビルド実行アシスタント（Build Assistant）](BuildAssistant/README.md) | ① Profile、② Output、③ Preview、④ Confirm、⑤ Buildの順に確認し、既存結果を上書きせずDesktop Standalone buildと最大20件の履歴を残す。Build Guard併用時はactual build callbackで壊れたSceneも検査する。 |
| 複数Sceneの順番・読込状態・Active Sceneを用途ごとに毎回戻したくない | [シーン作業セット（Scene Workspace）](SceneWorkspace/) | ProfileへSceneの順番・Loaded・Activeを保存し、Previewと確認後に安全に切り替える。 |
| Play Mode中に調整したInspectorの値を終了後も残すため、メモして入力し直したくない | [プレイ中の調整を反映（Play Mode Tuning）](PlayModeTuning/) | 残したいComponentの項目を先に選び、Play中に手動で取り込み、終了後のPreviewと確認を経てSceneへ反映する。 |
| Scene の読込順、Additive、Unload を安全に扱いたい | [シーン切り替え（SceneFlow）](SceneFlow/) | 4 種類の Scene 操作を直列化し、失敗理由を結果で受け取る。 |
| Scene 切り替え中に画面を隠したい | [画面フェード（ScreenTransition）](ScreenTransition/) | UI Toolkit の全画面 Cover・Reveal を実行する。 |
| ノッチや画面回転でUIが欠けるのを防ぎたい | [画面サイズ・ノッチ対応（AdaptiveLayout）](AdaptiveLayout/) | UI ToolkitとRectTransformを`Screen.safeArea`へ自動追従させる。 |
| Pause、Slow、Fast を複数機能から安全に使いたい | [ゲーム時間制御（TimeControl）](TimeControl/) | lease を重ねて `Time.timeScale` を競合なく制御する。 |
| BGM・SE の同時再生数や fade をまとめたい | [音声再生管理（AudioControl）](AudioControl/) | AudioSource pool、優先度、停止 handle、fade を管理する。 |
| セーブ枠、破損、バックアップを毎回実装したくない | [セーブデータ管理（SaveSystem）](SaveSystem/) | 型付き JSON、複数 slot、破損検出、backup 復旧を使う。 |
| 音量・quality・resolution・window mode・target frame rateの保存と適用を毎回書きたくない | [Player設定（Player Options）](PlayerOptions/) | 型付きsnapshotをLoad・Set・Apply・Saveに分け、未来schemaや破損値を自動上書きせず扱う。 |
| SceneやPrefabのMissing Script・削除済み参照を直し、Prefabの構造変更も別flowで確認したい | [プロジェクト不備確認・修復（Build Guard）](BuildGuard/) | build対象Sceneと選択Prefabの壊れた参照を検査し、Missing ScriptはUndo付きで除去する。Prefabの追加・削除GameObject／Componentはbuildを止めないreview windowで確認する。 |
| Texture Import Settingsを大量のAssetへ一括確認・適用したい | [アセット設定チェック（Asset Import Audit）](AssetImportAudit/) | Assets配下をPreviewし、共通設定とStandalone・Android・iOS別Overrideを選択または全件へ適用する。Preview後の外部変更は拒否する。 |
| Assetの利用箇所を確認・置換し、複数の名前もまとめて整理したい | [アセット整理・参照管理（Reference Finder）](ReferenceFinder/) | 直接・間接参照の検索、安全な参照置換、GUIDを維持する一括RenameをPreview後に実行する。 |
| 不具合調査用の状態とログを手動保存したい | [不具合レポート保存（DiagnosticsContext）](DiagnosticsContext/) | context、breadcrumb、Unity log を有界 JSON に書き出す。 |
| スティック補正とTap・Hold・Repeatをまとめて扱いたい | [入力補助（Input Assist）](InputAssist/) | dead zone、感度curve、滑らかさ、4/8方向、button gestureを1つの導入で処理する。 |
| Keyboard・Mouse・Gamepad・Touchで操作案内を切り替えたい | [入力デバイス表示（Input Device Display）](InputDeviceDisplay/) | 最後に実入力したdeviceを表示向けfamilyへ分類し、利用側UIの文字・画像・style切替に使う。 |
| 先行入力、コマンド入力、同時押し、入力の優先順位を扱いたい | [入力コマンド判定（Input Command）](InputCommand/) | 明示tickを使うbuffer・順序・同時押し・対向軸と、優先順位選択・sample基準の入力安定化を1つの導入で利用する。 |
| リソース、能力補正、抽選、しきい値などゲームの数値計算を毎回書きたくない | [ゲーム判定・計算（Gameplay Rules）](GameplayRules/) | 用途別namespaceから、決定論的で状態を壊さない計算を選んで使う。 |
| Replayやlockstepのために計算を再現可能にしたい | [再現可能シミュレーション（Deterministic Simulation）](DeterministicSimulation/) | 固定刻み、再現可能な乱数、記録tape、状態fingerprintを1つの導入で揃える。 |
| Gameplay 入力だけ一時的に止めたい | [入力の一時停止（InputGate）](InputGate/) | PlayerInput の Action Map を入れ子で停止・復元する。 |
| Inspector の表示整理や入力検証を減らしたい | [インスペクター入力補助（Inspector）](Inspector/) | 条件表示、group、tab、検証、button 属性を使う。 |
| 実行中の位置・範囲・経路を見たい | [デバッグ描画（Drawing）](Drawing/) | 線、矢印、箱、球、経路、文字をコードから描く。 |
| InstantiateとDestroyの繰り返しをやめてGCスパイクを抑えたい | [オブジェクト再利用（Object Pool）](ObjectPool/) | 1つのprefabを上限付きpoolで再利用し、spawn・release・統計を明示APIで扱う。 |
| iOS・Androidで異なる振動APIをintent指定の1つの呼び出しにまとめたい | [振動の統一（Haptics）](Haptics/) | capability報告に合わせて劣化再生を行い、未対応platformでは安全に無動作になる。 |
| フレーム時間や簡易メモリを実行中に数値で確認したい | [実行速度計測（Perf Meter）](PerfMeter/) | 有界windowのframe統計・spike計数・簡易メモリsnapshotをGC確保なしで取る。 |

導入前に「何ができるか」と「最短の使い方」を知りたい場合は、各 README の冒頭から読む。命名・統合・README の基準は [モジュール設計・案内ガイド](MODULE_GUIDE.md) にまとめている。

## 整理方針

小さな計算処理は単独テスト可能なまま保ち、配布単位は利用目的でまとめる。同じ namespace が複数の配布 package に分かれている状態は、統合が必要な合図として扱う。

入力加工系は「入力補助」、tick 基準のコマンド判定は「入力コマンド判定」、ゲームの数値計算系は「ゲーム判定・計算」、再現性の基盤は「再現可能シミュレーション」へ統合済み。公開済みのフォルダー名と tag は既存利用者のために残す。

新規機能は、Unity 固有の設定・Scene・Prefab・Build・端末差の面倒を直接減らすものを優先する。判断基準は [モジュール設計・案内ガイド](MODULE_GUIDE.md)、統合の実測根拠と今後の候補は [モジュール統合・追加機能の検討](MODULE_CONSOLIDATION_PLAN.md) にまとめている。

---

## 詳細モジュール一覧

| モジュール | 内容 | 依存 |
|---|---|---|
| [モジュール管理（Module Manager）](ModuleInstaller/) | Project Maintenance、Scene and UI、Game Services、Input Supportの4workflowを先に示し、`Quick guide`で用途・導入後の最初の操作・変更範囲を確認してから固定公開tagをまとめて追加する。決定論・ゲーム計算は専門向けcollectionへ分離し、個別moduleは公開tagのREADMEを開いてから導入できる。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [プロジェクト一括設定（Project Setup）](ProjectSetup/) | `Assets`配下の基本フォルダー、Runtime・Editor・test用asmdef、Unity向け`.gitignore`と`.gitattributes`、Project Settings、build target別Application Identifier・Scripting Backend・API Compatibility Level・Managed Stripping Level・IL2CPP Code Generation、Root Namespace、新規scriptの改行方式、複製時の命名規則、Play Mode Start Scene、条件付きコンパイル記号、Tag・Layer・Sorting Layer、Physics／Physics 2D Layer Collision、Build Scenesをprofile化する。差分Preview、backup、適用、復元を一つのEditor画面で行い、既存fileは上書きしない。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements / physics / physics2d 1.0.0 |
| [Assembly依存チェック（Assembly Dependency Audit）](AssemblyDependencyAudit/) | `Assets`と導入済み`Packages`のasmdefをread-onlyで走査し、参照元・assembly・参照先の3列graph、循環、未解決・曖昧・自己参照、PlayerからEditor専用assemblyへの逆参照、platform指定の矛盾を表示するEditor専用module。未使用参照やcompile時間は推測しない。**Unity 6000.5.7f1以降**。 | なし |
| [Localization key監査（Localization Key Audit）](LocalizationKeyAudit/) | Unity LocalizationのShared Table Dataをtyped load前にraw検証し、String／Asset Table ownerを分離してrequired LocaleのString direct table／entry／value、duplicate・orphan integrity、宣言済み`Assets` scopeのGUID＋key ID参照を手動表示するread-only advisory module。Asset Table entry、fallback後のruntime翻訳、keyの未使用は断定せず、build停止・autofix・削除を行わない。**Unity 6000.5.7f1以降**。 | com.unity.localization 1.5.12 |
| [ビルド実行アシスタント（Build Assistant）](BuildAssistant/README.md) | 有効なBuild Profile・Scene・出力先をPreviewし、実行直前に差分を再確認して新規フォルダーへDesktop Standalone buildを実行する。結果、容量内訳、前回差分を最大20件保存し、新しいJSONへ書き出すEditor専用module。Build Guardが導入済みなら、通常の`BuildPipeline.BuildPlayer` callbackによりactual build Sceneのblocker検査も自動適用される。Previewには他moduleのpolicy結果を混在させない。**Unity 6000.5.7f1以降**。 | なし |
| [シーン作業セット（Scene Workspace）](SceneWorkspace/) | 複数Sceneの順番・Loaded・ActiveをProfile化し、差分Preview、古くなった計画の拒否、適用後検証、失敗時の復元結果を1つのEditor画面で扱うEditor専用module。**Unity 6000.5.7f1以降**。 | なし |
| [プレイ中の調整を反映（Play Mode Tuning）](PlayModeTuning/) | 保存済みSceneのMonoBehaviourから残したい最上位serialized propertyを選び、Play Mode中に手動で取り込み、終了後の差分Preview、古くなった計画の拒否、適用後検証、失敗時の復元結果を1つのEditor画面で扱うEditor専用module。**Unity 6000.5.7f1以降**。 | なし |
| [汎用データ構造（Containers）](Containers/) | コンテナ / データ構造 66 種。GC フリーのコレクション、Inspector に出せるシリアライズ対応型、空間分割、Unity のライフサイクルに耐えるコンテナ。**Unity 6000.0以降**。 | なし |
| [オブジェクト再利用（Object Pool）](ObjectPool/) | 1つのprefabをidle上限・active上限・reuse順序(Lifo/Fifo)付きで再利用する。spawn・release・preload・trimをTry+error enumで扱い、生成／再利用／破壊の統計を持つ。外部破壊検知と他pool混線拒否を備える。**Unity 6000.5.7f1以降**。 | なし |
| [インスペクター入力補助（Inspector）](Inspector/) | Inspector 拡張の属性 43 種。条件による表示・非表示、グループ化とタブ、入力値の検証、メソッドのボタン化。**Unity 6000.5.7f1以降**。 | なし |
| [デバッグ描画（Drawing）](Drawing/) | 実行中の線・矢印・箱・球・経路・文字をコード1行で描くデバッグ可視化。Development Build専用呼び出しと持続時間に対応。**Unity 6000.5.7f1以降**。 | なし |
| [実行速度計測（Perf Meter）](PerfMeter/) | 有界リングバッファでframe時間を収集し、Average・StandardDeviation・Median・Percentile・spike計数などの統計を決定論的に返す。簡易メモリsnapshot取得とoverlay表示用Componentを含む。**Unity 6000.5.7f1以降**。 | なし |
| [セーブデータ管理（SaveSystem）](SaveSystem/) | 型付きJSON保存、複数スロット、破損検出、可能な環境での原子的置換、1世代バックアップ復旧。依存なし。**Unity 6000.5.7f1以降**。 | なし |
| [Player設定（Player Options）](PlayerOptions/) | application所有のserviceが音量、quality、resolution、window mode、refresh rate、target frame rateを一つの型付きsnapshotで扱う。Load・Set・Apply・Saveを分離し、未来schema・破損文書は保全する。PlayerPrefsに強い耐久性やtransactionを主張せず、key binding・cloud同期・vSync変更は含めない。**Unity 6000.5.7f1以降**。 | com.unity.modules.audio / jsonserialize / uielements 1.0.0 |
| [シーン切り替え（SceneFlow）](SceneFlow/) | 完全なSceneパスでSingle・Additive読込、有効Scene切替、Unloadを直列化し、開始前条件と完了後状態を結果で返す。**Unity 6000.5.7f1以降**。 | なし |
| [画面フェード（ScreenTransition）](ScreenTransition/) | UI Toolkitの全画面オーバーレイでCover・Revealを非スケール時間に実行し、色・時間・補間方法・完了結果を明示する。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [画面サイズ・ノッチ対応（AdaptiveLayout）](AdaptiveLayout/) | `Screen.safeArea`をUI ToolkitとRectTransformへ適用し、ノッチ、角丸、画面回転、解像度変更に追従する。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [ゲーム時間制御（TimeControl）](TimeControl/) | Scene所有のControllerが複数leaseの相対倍率を最小値で集約し、pause・slow motion・単独fast-forwardをTime.timeScaleへ安全に反映する。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [不具合レポート保存（DiagnosticsContext）](DiagnosticsContext/) | 明示追加したcontext・breadcrumbと実行中のUnity Warning・Error・Assert・Exceptionを有界に保持し、手動操作時だけJSON reportへ書き出す。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [プロジェクト不備確認・修復（Build Guard）](BuildGuard/) | build対象Sceneと選択PrefabのMissing Script・削除済みObject Referenceを一覧から開き、Missing Scriptだけを確認・Undo付きで除去できる。別windowではenabled build SceneのPrefabへ追加・削除したGameObject／Componentをreviewし、stale再確認後に安全に対象へ移動する。Property Modificationは含めず、review結果はPlayer buildを止めないEditor専用module。**Unity 6000.5.7f1以降**。 | なし |
| [アセット設定チェック（Asset Import Audit）](AssetImportAudit/) | `Assets`配下のTexture2Dを決定論的に検査し、共通設定とStandalone・Android・iOS別OverrideをShared・Platform・両方のscopeでPreview・選択適用・全件適用する。Preview後のstale importerは拒否するEditor専用module。**Unity 6000.5.7f1以降**。 | なし |
| [アセット整理・参照管理（Reference Finder）](ReferenceFinder/) | 選択Assetの直接・間接参照元を検索し、安全に特定できた参照だけをUndo付きで置換する。さらに複数Assetへ文字置換・prefix・suffixをまとめて適用し、GUIDを維持してRenameするEditor専用module。**Unity 6000.5.7f1以降**。 | なし |
| [入力補助（Input Assist）](InputAssist/) | 2D入力へradial dead zone、応答curve、増減速度制限、方向量子化、重み付き合成を適用し、button入力からTap・Hold・Repeat・multi-tapを判定する。Unity向けの`float`+`deltaTime`契約と、確保を伴わない`double`契約を同じpackageが持つ。入力値と経過時間は利用側から渡すため、Input System・AI・Replayのどれでも使える。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [入力デバイス表示（Input Device Display）](InputDeviceDisplay/) | Input Systemのglobalな実入力から最後に操作されたdeviceをKeyboard／Mouse、Xbox、PlayStation、Switch、一般Gamepad、Touchの表示familyへ分類する。厳密layout overrideと明示fallbackを備え、glyph asset、rebind、pairing、player別追跡は扱わない。**Unity 6000.5.7f1以降**。 | com.unity.inputsystem 1.20.0 / com.unity.modules.uielements 1.0.0 |
| [入力の一時停止（InputGate）](InputGate/) | PlayerInputの実行中Action Mapを入れ子leaseで停止し、最後の解放時にActionごとの有効状態を復元する。**Unity 6000.5.7f1 / Input System 1.20.0以降**。 | com.unity.inputsystem 1.20.0 / com.unity.modules.uielements 1.0.0 |
| [音声再生管理（AudioControl）](AudioControl/) | owner付きAudioSource poolで再生、voice上限、priority steal、handle停止、非スケールfadeを管理する。**Unity 6000.5.7f1以降**。 | com.unity.modules.audio 1.0.0 / com.unity.modules.uielements 1.0.0 |
| [振動の統一（Haptics）](Haptics/) | intent指定の振動再生とdriver capability報告を1つのserviceへまとめ、Androidは波形、iOSはシステム振動へ自動劣化させる。ネイティブプラグイン同梱なし。**Unity 6000.5.7f1以降**。 | なし |
| [起動手順管理（StartupFlow）](StartupFlow/) | 明示した非同期stepをOrderとIdで決定論的に直列実行し、進捗・失敗位置・完了件数・協調cancelを結果として返す。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [入力コマンド判定（Input Command）](InputCommand/) | 先行入力buffer、順序判定、同時押し、優先順位選択、対向軸解決、チャタリング除去を独立した決定論的部品としてまとめる。tickを使う判定、sample回数で進む安定化、状態を持たない選択から必要なものを選び、異なる入出力を繋ぐadapterは利用側が持つ。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [ゲーム判定・計算（Gameplay Rules）](GameplayRules/) | リソースとコスト、能力補正、重み付き抽選と整数配分、区間curveとしきい値tier、直近statisticsと傾向推定、時限stackと定期発火、数値条件・行動score・敵対度の評価、ダメージ軽減を用途別namespaceでまとめて提供する決定論的な計算群。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |
| [再現可能シミュレーション（Deterministic Simulation）](DeterministicSimulation/) | 固定刻み時計、再現可能な乱数、canonicalなdata変換、固定小数点、入力記録tape、状態fingerprint、世代付きhandleをまとめる。replayやlockstepは単独moduleでは成立しないため、1つの導入単位にしている。**Unity 6000.5.7f1以降**。 | com.unity.modules.uielements 1.0.0 |

### 統合前モジュールとの関係

44 個の細分化 module は、次の 4 つへ統合した。C# の namespace、型名、member、動作は維持しているためsource / API互換だが、runtime assembly名は変わるためbinary互換ではない。旧packageを削除して統合後packageを追加し、自作asmdefの`references`を差し替える。旧assemblyを参照するprecompiled DLLは再buildする。

| 統合先 | 統合前 |
|---|---|
| [入力補助（Input Assist）](InputAssist/) | Input Radial Dead Zone / Input Vector Response Curve / Input Vector Slew Limiter / Input Vector Exponential Smoother / Input Vector Direction Limiter / Input Vector Weighted Mixer / Input Direction Quantizer / Input Quantizer / Input Threshold Classifier / Input Press Classifier / Input Repeat / Input Multi Tap Classifier |
| [入力コマンド判定（Input Command）](InputCommand/) | Input Command Buffer / Input Sequence Matcher / Input Chord Matcher / Input Command Arbiter / Input Axis Conflict Resolver / Input Stabilizer |
| [ゲーム判定・計算（Gameplay Rules）](GameplayRules/) | Resource Meter / Resource Cost Evaluator / Stat Modifier Stack / Weighted Choice Table / Weighted Integer Allocator / Piecewise Linear Curve / Rolling Sample Window / Sample Statistics / Linear Trend Estimator / Threshold Tier Table / Charge Cooldown / Periodic Tick Planner / Timed Stack Resolver / Stack Transfer Planner / Numeric Requirement Evaluator / Utility Score Evaluator / Stable Score Selector / Damage Mitigation Evaluator / Threat Score Resolver |
| [再現可能シミュレーション（Deterministic Simulation）](DeterministicSimulation/) | Simulation Clock / Deterministic Random / State Fingerprint / Replay Tape / Canonical Payload / Fixed Point / Generational Handle |

今回の統合対象で公開済みだった旧packageの43個のtagは削除していない。`?path=/<旧フォルダー名>#<旧tag>`で固定している既存利用者は旧配布単位を継続利用できる。Threat Score Resolverには単独tagがなく、Gameplay Rulesで初めてtag付き配布になる。旧packageと統合後packageは同じ型を別assemblyに含むため同時導入せず、新規導入と更新では統合後packageを使う。

---

## 使い方

新しいProjectでは、まず [モジュール管理（Module Manager）](ModuleInstaller/) をPackage Managerへ追加し、`Tools > Module Manager > Open`から4つの実用workflowを選ぶ。`Quick guide`で用途・最初の操作・変更範囲を確認し、未導入moduleの追加件数を確認してから実行する。専門向けcollectionと22件の個別一覧は初期状態で折りたたまれ、個別行の`Read guide`はcatalogと同じ公開tagのREADMEを開く。更新は公開tagへ固定され、同じversion・より新しいversion・catalog外versionを上書きしない。統合前の旧packageや`Assets/Modules` copyが残る場合は、重複型を避けるためpackage変更前に停止し、削除対象を表示する。Module Manager自身は旧moduleを自動削除しない。

新規Projectの設定をそろえる場合は、[プロジェクト一括設定（Project Setup）](ProjectSetup/) を追加して `Tools > Project Setup > Open` を開く。`New recommended profile`で安全な推奨profileを作り、必要なら基本フォルダー、Runtime・Editor・test用asmdef、Unity向け`.gitignore`と`.gitattributes`、build target別Application Identifier・Scripting Backend・API Compatibility Level・Managed Stripping Level・IL2CPP Code Generation、Root Namespace、新規scriptの改行方式、複製時の命名規則、Play Mode Start Scene、条件付きコンパイル記号、Tag・Layer・Sorting Layer、Physics／Physics 2D Layer Collision、Build Scenesを追加する。`Preview changes`で差分を確認してから`Apply profile`を実行すると、適用直前の設定とツールが自動backupされる。復元時は、ツールが作成した後に内容が変わっていないfileだけを削除する。

複数Sceneの編集構成を用途ごとに切り替える場合は、[シーン作業セット（Scene Workspace）](SceneWorkspace/) を個別に追加して `Tools > Scene Workspace > Open` を開く。① `Workspace Profile`、② `Scene Setup/Capture`、③ `Preview Changes`、④ `Review and Confirm`、⑤ `Switch Workspace/Result` の順に確認し、Sceneを変更する前に差分と安全条件を確定する。

Play Mode中の調整値を残す場合は、[プレイ中の調整を反映（Play Mode Tuning）](PlayModeTuning/) を個別に追加して `Tools > Play Mode Tuning > Open` を開く。① `Targets`、② `Capture During Play`、③ `Preview After Play`、④ `Review and Confirm`、⑤ `Apply Tuning / Result` の順に確認し、選択項目だけを手動で取り込んでから差分を反映する。

特定モジュールだけを手作業で配置する場合は、そのフォルダーをプロジェクトの `Assets/Modules/` 以下へコピーする。アセンブリ定義が同梱されているので、利用側のasmdefから必要なassemblyを参照する。

```
Assets/
└── Modules/
    ├── ModuleInstaller/
    │   ├── Editor/          ModuleInstaller.Editor
    │   └── Tests/Editor/    ModuleInstaller.Editor.Tests
    ├── ProjectSetup/
    │   ├── Editor/          ProjectSetup.Editor
    │   └── Tests/           ProjectSetup.Tests
    ├── AssemblyDependencyAudit/
    │   ├── Editor/          AssemblyDependencyAudit.Editor
    │   └── Tests/Editor/    AssemblyDependencyAudit.Editor.Tests
    ├── BuildGuard/
    │   ├── Editor/          BuildGuard.Editor
    │   └── Tests/Editor/    BuildGuard.Editor.Tests
    ├── AssetImportAudit/
    │   ├── Editor/          AssetImportAudit.Editor
    │   └── Tests/           AssetImportAudit.Tests
    ├── ReferenceFinder/
    │   ├── Editor/          ReferenceFinder.Editor
    │   ├── Tests/           ReferenceFinder.Tests
    │   └── Samples~/        2 assemblies
    ├── BuildAssistant/
    │   ├── Editor/          BuildAssistant.Editor
    │   └── Tests/           BuildAssistant.Tests
    ├── SceneWorkspace/
    │   ├── Editor/          SceneWorkspace.Editor
    │   ├── Tests/           SceneWorkspace.Tests
    │   └── Documentation~/  操作順と実画面
    ├── PlayModeTuning/
    │   ├── Editor/          PlayModeTuning.Editor
    │   ├── Tests/           PlayModeTuning.Tests
    │   └── Documentation~/  操作順と実画面
    ├── Inspector/
    │   ├── Runtime/         Inspector.Runtime
    │   ├── Editor/          Inspector.Editor
    │   ├── Tests/           Inspector.Tests
    │   └── Samples~/        1 assembly
    ├── Drawing/
    │   ├── Runtime/         Drawing.Runtime
    │   ├── Tests/           Drawing.Tests
    │   └── Samples~/        1 assembly
    ├── PerfMeter/
    │   ├── Runtime/         PerfMeter.Runtime
    │   ├── Tests/Editor/    PerfMeter.Editor.Tests
    │   └── Samples~/        2 assemblies
    ├── Containers/
    │   ├── Runtime/         Containers.Runtime
    │   ├── Editor/          Containers.Editor
    │   └── Tests/           Containers.Tests
    ├── ObjectPool/
    │   ├── Runtime/         ObjectPool.Runtime
    │   ├── Tests/Editor/    ObjectPool.Editor.Tests
    │   └── Samples~/        2 assemblies
    ├── SceneFlow/
    │   ├── Runtime/         SceneFlow.Runtime
    │   ├── Editor/          SceneFlow.Editor
    │   ├── Tests/           SceneFlow.Tests, SceneFlow.Editor.Tests, SceneFlow.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── ScreenTransition/
    │   ├── Runtime/         ScreenTransition.Runtime
    │   ├── Tests/           ScreenTransition.Tests, ScreenTransition.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── AdaptiveLayout/
    │   ├── Runtime/         AdaptiveLayout.Runtime
    │   ├── Tests/           AdaptiveLayout.Tests, AdaptiveLayout.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── TimeControl/
    │   ├── Runtime/         TimeControl.Runtime
    │   ├── Tests/           TimeControl.Tests, TimeControl.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── StartupFlow/
    │   ├── Runtime/         StartupFlow.Runtime
    │   ├── Tests/           StartupFlow.Tests, StartupFlow.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── SaveSystem/
    │   ├── Runtime/         SaveSystem.Runtime
    │   ├── Tests/           SaveSystem.Tests
    │   └── Samples~/        1 assembly
    ├── PlayerOptions/
    │   ├── Runtime/         PlayerOptions.Runtime
    │   ├── Tests/Editor/    PlayerOptions.Editor.Tests
    │   ├── Tests/Runtime/   PlayerOptions.Runtime.Tests
    │   └── Samples~/        2 assemblies
    ├── AudioControl/
    │   ├── Runtime/         AudioControl.Runtime
    │   ├── Tests/           AudioControl.Tests, AudioControl.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── Haptics/
    │   ├── Runtime/         Haptics.Runtime
    │   ├── Tests/Editor/    Haptics.Editor.Tests
    │   └── Samples~/        2 assemblies
    ├── DiagnosticsContext/
    │   ├── Runtime/         DiagnosticsContext.Runtime
    │   ├── Tests/           DiagnosticsContext.Tests, DiagnosticsContext.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── InputAssist/
    │   ├── Runtime/         InputAssist.Runtime
    │   ├── Tests/Editor/    InputAssist.Editor.Tests
    │   └── Samples~/        26 assemblies
    ├── InputCommand/
    │   ├── Runtime/         InputCommand.Runtime
    │   ├── Tests/Editor/    InputCommand.Editor.Tests
    │   └── Samples~/        12 assemblies
    ├── InputGate/
    │   ├── Runtime/         InputGate.Runtime
    │   ├── Tests/           InputGate.Tests, InputGate.PlayMode.Tests
    │   └── Samples~/        2 assemblies
    ├── InputDeviceDisplay/
    │   ├── Runtime/         InputDeviceDisplay.Runtime
    │   ├── Tests/           InputDeviceDisplay.Editor.Tests, InputDeviceDisplay.PlayMode.Tests
    │   └── Samples~/        1 assembly
    ├── GameplayRules/
    │   ├── Runtime/         GameplayRules.Runtime
    │   ├── Tests/Editor/    GameplayRules.Editor.Tests
    │   └── Samples~/        38 assemblies
    └── DeterministicSimulation/
        ├── Runtime/         DeterministicSimulation.Runtime
        ├── Tests/Editor/    DeterministicSimulation.Editor.Tests
        └── Samples~/        14 assemblies
```

UPM パッケージとして扱う場合は、モジュールのフォルダを `Packages/` 以下に置くか、
`manifest.json` からローカルパスで参照する。

---

## 各モジュールの約束

- **依存を明記する** — リポジトリ内モジュールへの依存と導入順を各 README に書く
- **`unsafe` を使わない** — asmdef の設定を変えずに導入できる
- **アセンブリ定義を持つ** — 使わないモジュールはビルドに入らない
- **`.meta` を含む** — GUID が変わらないので、参照が壊れない
