# Unity作業を楽にするモジュール集

Unity で繰り返し発生する設定、実装、確認作業を減らすためのモジュール集。
利用者向けの名前は日本語で目的を示し、フォルダー名・名前空間・UPM 識別子には互換性のため英語の技術名を残している。

対応: **Unity 6000.0 以降**

---

## 困りごとから選ぶ

| 困りごと | 推奨モジュール | まずできること |
|---|---|---|
| Scene の読込順、Additive、Unload を安全に扱いたい | [シーン切り替え（SceneFlow）](SceneFlow/) | 4 種類の Scene 操作を直列化し、失敗理由を結果で受け取る。 |
| Scene 切り替え中に画面を隠したい | [画面フェード（ScreenTransition）](ScreenTransition/) | UI Toolkit の全画面 Cover・Reveal を実行する。 |
| ノッチや画面回転でUIが欠けるのを防ぎたい | [画面サイズ・ノッチ対応（AdaptiveLayout）](AdaptiveLayout/) | UI ToolkitとRectTransformを`Screen.safeArea`へ自動追従させる。 |
| Pause、Slow、Fast を複数機能から安全に使いたい | [ゲーム時間制御（TimeControl）](TimeControl/) | lease を重ねて `Time.timeScale` を競合なく制御する。 |
| BGM・SE の同時再生数や fade をまとめたい | [音声再生管理（AudioControl）](AudioControl/) | AudioSource pool、優先度、停止 handle、fade を管理する。 |
| セーブ枠、破損、バックアップを毎回実装したくない | [セーブデータ管理（SaveSystem）](SaveSystem/) | 型付き JSON、複数 slot、破損検出、backup 復旧を使う。 |
| Missing Scriptや削除済みAssetへの参照をbuild後に見つけたくない | [ビルド前の不備確認・修復（Build Guard）](BuildGuard/) | 壊れた参照を一覧化し、修復場所へ移動する。Missing Scriptは確認後にUndo可能な形で除去できる。 |
| Assetの利用箇所を確認・置換し、複数の名前もまとめて整理したい | [アセット整理・参照管理（Reference Finder）](ReferenceFinder/) | 直接・間接参照の検索、安全な参照置換、GUIDを維持する一括RenameをPreview後に実行する。 |
| 不具合調査用の状態とログを手動保存したい | [不具合レポート保存（DiagnosticsContext）](DiagnosticsContext/) | context、breadcrumb、Unity log を有界 JSON に書き出す。 |
| スティック補正とTap・Hold・Repeatをまとめて扱いたい | [入力補助（Input Assist）](InputAssist/) | dead zone、感度curve、滑らかさ、4/8方向、button gestureを1つの導入で処理する。 |
| Gameplay 入力だけ一時的に止めたい | [入力の一時停止（InputGate）](InputGate/) | PlayerInput の Action Map を入れ子で停止・復元する。 |
| Inspector の表示整理や入力検証を減らしたい | [インスペクター入力補助（Inspector）](Inspector/) | 条件表示、group、tab、検証、button 属性を使う。 |
| 実行中の位置・範囲・経路を見たい | [デバッグ描画（Drawing）](Drawing/) | 線、矢印、箱、球、経路、文字をコードから描く。 |

導入前に「何ができるか」と「最短の使い方」を知りたい場合は、各 README の冒頭から読む。命名・統合・README の基準は [モジュール設計・案内ガイド](MODULE_GUIDE.md) にまとめている。

## 整理方針

小さな計算処理は単独テスト可能なまま保ち、配布単位は利用目的でまとめる。入力加工系は「入力補助」、ゲームの数値計算系は「ゲーム判定・計算」、再現性の基盤は「再現可能シミュレーション」へ統合する。公開済みのフォルダー名とタグは既存利用者のために残す。

新規機能は、Unity 固有の設定・Scene・Prefab・Build・端末差の面倒を直接減らすものを優先する。

---

## 詳細モジュール一覧

| モジュール | 内容 | 依存 |
|---|---|---|
| [汎用データ構造（Containers）](Containers/) | コンテナ / データ構造 66 種。GC フリーのコレクション、Inspector に出せるシリアライズ対応型、空間分割、Unity のライフサイクルに耐えるコンテナ。 | なし |
| [インスペクター入力補助（Inspector）](Inspector/) | Inspector 拡張の属性 43 種。条件による表示・非表示、グループ化とタブ、入力値の検証、メソッドのボタン化。**Unity 6000.5 以降**。 | なし |
| [デバッグ描画（Drawing）](Drawing/) | 実行中の線・矢印・箱・球・経路・文字をコード1行で描くデバッグ可視化。Development Build専用呼び出しと持続時間に対応。**Unity 6000.5 以降**。 | なし |
| [セーブデータ管理（SaveSystem）](SaveSystem/) | 型付きJSON保存、複数スロット、破損検出、可能な環境での原子的置換、1世代バックアップ復旧。依存なし。**Unity 6000.5 以降**。 | なし |
| [シーン切り替え（SceneFlow）](SceneFlow/) | 完全なSceneパスでSingle・Additive読込、有効Scene切替、Unloadを直列化し、開始前条件と完了後状態を結果で返す。**Unity 6000.5 以降**。 | なし |
| [画面フェード（ScreenTransition）](ScreenTransition/) | UI Toolkitの全画面オーバーレイでCover・Revealを非スケール時間に実行し、色・時間・補間方法・完了結果を明示する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [画面サイズ・ノッチ対応（AdaptiveLayout）](AdaptiveLayout/) | `Screen.safeArea`をUI ToolkitとRectTransformへ適用し、ノッチ、角丸、画面回転、解像度変更に追従する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [ゲーム時間制御（TimeControl）](TimeControl/) | Scene所有のControllerが複数leaseの相対倍率を最小値で集約し、pause・slow motion・単独fast-forwardをTime.timeScaleへ安全に反映する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [不具合レポート保存（DiagnosticsContext）](DiagnosticsContext/) | 明示追加したcontext・breadcrumbと実行中のUnity Warning・Error・Assert・Exceptionを有界に保持し、手動操作時だけJSON reportへ書き出す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [ビルド前の不備確認・修復（Build Guard）](BuildGuard/) | build対象SceneのMissing Scriptと削除済みObject Referenceを一覧から開き、Missing Scriptだけを確認・Undo付きで除去できる。Player build開始時にも同じ検査で自動停止するEditor専用module。**Unity 6000.5 以降**。 | なし |
| [アセット整理・参照管理（Reference Finder）](ReferenceFinder/) | 選択Assetの直接・間接参照元を検索し、安全に特定できた参照だけをUndo付きで置換する。さらに複数Assetへ文字置換・prefix・suffixをまとめて適用し、GUIDを維持してRenameするEditor専用module。**Unity 6000.5 以降**。 | なし |
| [入力補助（Input Assist）](InputAssist/) | 2D入力へradial dead zone、感度curve、増減速度制限、4/8方向判定をまとめて適用し、button入力からTap・Hold・Repeat・multi-tapを判定する。入力値と経過時間は利用側から渡すため、Input System・AI・Replayのどれでも使える。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [入力の一時停止（InputGate）](InputGate/) | PlayerInputの実行中Action Mapを入れ子leaseで停止し、最後の解放時にActionごとの有効状態を復元する。**Unity 6000.5 / Input System 1.20.0 以降**。 | com.unity.inputsystem 1.20.0 / com.unity.modules.uielements 1.0.0 |
| [音声再生管理（AudioControl）](AudioControl/) | owner付きAudioSource poolで再生、voice上限、priority steal、handle停止、非スケールfadeを管理する。**Unity 6000.5 以降**。 | com.unity.modules.audio 1.0.0 / com.unity.modules.uielements 1.0.0 |
| [起動手順管理（StartupFlow）](StartupFlow/) | 明示した非同期stepをOrderとIdで決定論的に直列実行し、進捗・失敗位置・完了件数・協調cancelを結果として返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [固定刻みシミュレーション時計（SimulationClock）](SimulationClock/) | 明示した整数経過時間を再現可能な固定step範囲・端数・補間率・drop量へ変換し、状態を保存・復元する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [再現可能な乱数（DeterministicRandom）](DeterministicRandom/) | version付き256-bit状態を保存・復元し、同じseed・状態・操作順から同じ64-bit列・範囲整数・浮動小数を再現する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [状態一致チェック（StateFingerprint）](StateFingerprint/) | 明示した型付きfield列をversion固定canonical bytesへ変換し、Replay前後のstate一致をportable SHA-256 fingerprintで検証する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [入力記録・再生（ReplayTape）](ReplayTape/) | 非減少tick・command id・opaque payloadをversion固定canonical tapeへ記録し、完全検証後に同じ順序で読み戻す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [再現用データ変換（CanonicalPayload）](CanonicalPayload/) | 明示したschema順のprimitive値をlittle-endian・IEEE 754・厳格UTF-8の有界canonical bytesへ変換し、同順序で読み戻す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [固定小数点計算（FixedPoint）](FixedPoint/) | signed Q16.16の小数値を整数raw値で保持し、0方向丸めと明示overflowを持つ四則演算をplatform間で再現する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [解放済み参照の識別（GenerationalHandle）](GenerationalHandle/) | 最小の空きslotを決定論的に割り当て、generationで解放済みの古いhandleを新しいentryから区別する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [ゲージ値管理（ResourceMeter）](ResourceMeter/) | immutable capacity内の有限resourceを回復・部分消費・全量必須消費し、前後値・実適用量・未適用量・境界遷移を再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [能力値補正（StatModifierStack）](StatModifierStack/) | 最大32件の有限modifierをID昇順でFlat・加算percent・乗算factorの3 stageへ合成し、最終値・stage合計・件数を再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [重み付き抽選（WeightedChoiceTable）](WeightedChoiceTable/) | 最大32件の正weightをID昇順の累積区間へ変換し、明示sampleから選択ID・index・区間・totalを再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [区間補間カーブ（PiecewiseLinearCurve）](PiecewiseLinearCurve/) | 最大32個の有限pointをX昇順で保持し、有限queryを隣接2点から線形補間して値・segment・補間率・clamp状態を再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [直近データ保持（RollingSampleWindow）](RollingSampleWindow/) | 最大32件の有限sampleを固定長FIFO窓へ保持し、追加ごとの退避値と前後snapshot、count・min・max・mean・oldest・newestを再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [しきい値ランク判定（ThresholdTierTable）](ThresholdTierTable/) | 最大32件の有限thresholdを昇順に保持し、有限queryから現在tier・次tier・0〜1の段階内進捗を再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [増減傾向の推定（LinearTrendEstimator）](LinearTrendEstimator/) | 2〜32個の等間隔な有限sampleへ最小二乗直線を当て、mean・slope・intercept・next predictionを再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [チャージ回復計算（ChargeCooldown）](ChargeCooldown/) | 最大32 chargeの消費と逐次回復を明示simulation tickから計算し、前後state・回復数・消費成否を再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [少数データ統計（SampleStatistics）](SampleStatistics/) | 1〜32個の有限sampleからminimum・maximum・mean・range・母分散・母標準偏差を再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [コスト消費判定（ResourceCostEvaluator）](ResourceCostEvaluator/) | 最大32件ずつのresource残量とcostから、stateを変更せず全支払可否・支払後残量・不足量をresource別に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [数値条件判定（NumericRequirementEvaluator）](NumericRequirementEvaluator/) | 最大32件の有限な実値・基準値・比較方法・許容差から、stateを変更せず全条件の成立可否と入力順の全明細を返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [行動スコア計算（UtilityScoreEvaluator）](UtilityScoreEvaluator/) | 最大32候補・各16factorの0〜1 utilityと正weightから、stateを変更せず最高score候補・安定tie-break・全寄与明細を返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [候補の安定選択（StableScoreSelector）](StableScoreSelector/) | 最大32候補の0〜1 scoreとcurrent IDから、同点・微差では維持し、明示優位差以上でだけ安定して切り替える。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [整数の重み配分（WeightedIntegerAllocator）](WeightedIntegerAllocator/) | 最大32 entryへ整数総量を非負整数weight比で配分し、largest remainderと入力順tie-breakで合計を失わず返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [スタック移動計画（StackTransferPlanner）](StackTransferPlanner/) | 最大32 sourceと32 destination間の整数unit移送を入力順で計画し、stateを変更せず両側の全明細と未充足量を返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [時間制スタック更新（TimedStackResolver）](TimedStackResolver/) | 時限effectの現在stack数・残りtick数と追加状態を、独立した再適用方針と上限から決定論的に解決する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [定期発火計画（PeriodicTickPlanner）](PeriodicTickPlanner/) | 次回tick・間隔・残り回数から、指定simulation tickまでの定期発火範囲と次cursorを有界かつ決定論的に計画する。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [ダメージ軽減計算（DamageMitigationEvaluator）](DamageMitigationEvaluator/) | 元damageへ固定軽減・率軽減を入力順に適用し、各層の要求量・実適用量・残damageを再構築可能に返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |
| [敵対度計算（ThreatScoreResolver）](ThreatScoreResolver/) | 1〜32対象の非負threat scoreへ最大64件の有限増減を入力順に適用し、0下限・全明細・安定首位を返す。**Unity 6000.5 以降**。 | com.unity.modules.uielements 1.0.0 |

### 旧入力モジュールとの関係

通常のスティック補正とbutton gestureは、新規導入では [入力補助（Input Assist）](InputAssist/) を使う。公開済みの細分化moduleは既存利用者との互換性のため残している。sequence・chord・buffer・priority選択など高度なcommand処理が必要な場合だけ、対応する旧moduleを個別に選ぶ。

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
    ├── SaveSystem/
    │   ├── Runtime/     SaveSystem.Runtime
    │   ├── Tests/       SaveSystem.Tests
    │   └── Samples~/    SaveSystem.Samples
    ├── SceneFlow/
    │   ├── Runtime/     SceneFlow.Runtime
    │   ├── Editor/      SceneFlow.Editor
    │   ├── Tests/       SceneFlow.Tests / SceneFlow.Editor.Tests / SceneFlow.PlayMode.Tests
    │   └── Samples~/    SceneFlow.Samples
    ├── ScreenTransition/
    │   ├── Runtime/     ScreenTransition.Runtime
    │   ├── Tests/       ScreenTransition.Tests / ScreenTransition.PlayMode.Tests
    │   └── Samples~/    ScreenTransition.Samples / ScreenTransition.Samples.PlayMode.Tests
    ├── AdaptiveLayout/
    │   ├── Runtime/     AdaptiveLayout.Runtime
    │   ├── Tests/       AdaptiveLayout.Tests / AdaptiveLayout.PlayMode.Tests
    │   └── Samples~/    AdaptiveLayout.Samples / AdaptiveLayout.Samples.PlayMode.Tests
    ├── TimeControl/
    │   ├── Runtime/     TimeControl.Runtime
    │   ├── Tests/       TimeControl.Tests / TimeControl.PlayMode.Tests
    │   └── Samples~/    TimeControl.Samples / TimeControl.Samples.PlayMode.Tests
    ├── DiagnosticsContext/
    │   ├── Runtime/     DiagnosticsContext.Runtime
    │   ├── Tests/       DiagnosticsContext.Tests / DiagnosticsContext.PlayMode.Tests
    │   └── Samples~/    DiagnosticsContext.Samples / DiagnosticsContext.Samples.PlayMode.Tests
    ├── BuildGuard/
    │   ├── Editor/      BuildGuard.Editor
    │   ├── Tests/       BuildGuard.Tests
    │   └── Samples~/    Build Guard Basics
    ├── ReferenceFinder/
    │   ├── Editor/      ReferenceFinder.Editor
    │   ├── Tests/       ReferenceFinder.Tests
    │   └── Samples~/    Reference Finder Basics
    ├── InputAssist/
    │   ├── Runtime/     InputAssist.Runtime
    │   ├── Tests/       InputAssist.Tests
    │   └── Samples~/    InputAssist.Samples / InputAssist.Samples.PlayMode.Tests
    ├── InputGate/
        ├── Runtime/     InputGate.Runtime
        ├── Tests/       InputGate.Tests / InputGate.PlayMode.Tests
        └── Samples~/    InputGate.Samples / InputGate.Samples.PlayMode.Tests
    ├── AudioControl/
        ├── Runtime/     AudioControl.Runtime
        ├── Tests/       AudioControl.Tests / AudioControl.PlayMode.Tests
        └── Samples~/    AudioControl.Samples / AudioControl.Samples.PlayMode.Tests
    ├── StartupFlow/
        ├── Runtime/     StartupFlow.Runtime
        ├── Tests/       StartupFlow.Tests / StartupFlow.PlayMode.Tests
        └── Samples~/    StartupFlow.Samples / StartupFlow.Samples.PlayMode.Tests
    ├── SimulationClock/
        ├── Runtime/     SimulationClock.Runtime
        ├── Tests/       SimulationClock.Tests
        └── Samples~/    SimulationClock.Samples / SimulationClock.Samples.PlayMode.Tests
    ├── DeterministicRandom/
        ├── Runtime/     DeterministicRandom.Runtime
        ├── Tests/       DeterministicRandom.Tests
        └── Samples~/    DeterministicRandom.Samples / DeterministicRandom.Samples.PlayMode.Tests
    ├── StateFingerprint/
        ├── Runtime/     StateFingerprint.Runtime
        ├── Tests/       StateFingerprint.Tests
        └── Samples~/    StateFingerprint.Samples / StateFingerprint.Samples.PlayMode.Tests
    ├── ReplayTape/
        ├── Runtime/     ReplayTape.Runtime
        ├── Tests/       ReplayTape.Tests
        └── Samples~/    ReplayTape.Samples / ReplayTape.Samples.PlayMode.Tests
    ├── CanonicalPayload/
        ├── Runtime/     CanonicalPayload.Runtime
        ├── Tests/       CanonicalPayload.Tests
        └── Samples~/    CanonicalPayload.Samples / CanonicalPayload.Samples.PlayMode.Tests
    ├── FixedPoint/
        ├── Runtime/     FixedPoint.Runtime
        ├── Tests/       FixedPoint.Tests
        └── Samples~/    FixedPoint.Samples / FixedPoint.Samples.PlayMode.Tests
    ├── GenerationalHandle/
        ├── Runtime/     GenerationalHandle.Runtime
        ├── Tests/       GenerationalHandle.Tests
        └── Samples~/    GenerationalHandle.Samples / GenerationalHandle.Samples.PlayMode.Tests
    ├── ResourceMeter/
        ├── Runtime/     ResourceMeter.Runtime
        ├── Tests/       ResourceMeter.Tests
        └── Samples~/    ResourceMeter.Samples / ResourceMeter.Samples.PlayMode.Tests
    ├── StatModifierStack/
        ├── Runtime/     StatModifierStack.Runtime
        ├── Tests/       StatModifierStack.Tests
        └── Samples~/    StatModifierStack.Samples / StatModifierStack.Samples.PlayMode.Tests
    ├── WeightedChoiceTable/
        ├── Runtime/     WeightedChoiceTable.Runtime
        ├── Tests/       WeightedChoiceTable.Tests
        └── Samples~/    WeightedChoiceTable.Samples / WeightedChoiceTable.Samples.PlayMode.Tests
    ├── PiecewiseLinearCurve/
        ├── Runtime/     PiecewiseLinearCurve.Runtime
        ├── Tests/       PiecewiseLinearCurve.Tests
        └── Samples~/    PiecewiseLinearCurve.Samples / PiecewiseLinearCurve.Samples.PlayMode.Tests
    ├── RollingSampleWindow/
        ├── Runtime/     RollingSampleWindow.Runtime
        ├── Tests/       RollingSampleWindow.Tests
        └── Samples~/    RollingSampleWindow.Samples / RollingSampleWindow.Samples.PlayMode.Tests
    ├── ThresholdTierTable/
        ├── Runtime/     ThresholdTierTable.Runtime
        ├── Tests/       ThresholdTierTable.Tests
        └── Samples~/    ThresholdTierTable.Samples / ThresholdTierTable.Samples.PlayMode.Tests
    ├── LinearTrendEstimator/
        ├── Runtime/     LinearTrendEstimator.Runtime
        ├── Tests/       LinearTrendEstimator.Tests
        └── Samples~/    LinearTrendEstimator.Samples / LinearTrendEstimator.Samples.PlayMode.Tests
    ├── ChargeCooldown/
        ├── Runtime/     ChargeCooldown.Runtime
        ├── Tests/       ChargeCooldown.Tests
        └── Samples~/    ChargeCooldown.Samples / ChargeCooldown.Samples.PlayMode.Tests
    ├── SampleStatistics/
        ├── Runtime/     SampleStatistics.Runtime
        ├── Tests/       SampleStatistics.Tests
        └── Samples~/    SampleStatistics.Samples / SampleStatistics.Samples.PlayMode.Tests
    ├── ResourceCostEvaluator/
        ├── Runtime/     ResourceCostEvaluator.Runtime
        ├── Tests/       ResourceCostEvaluator.Tests
        └── Samples~/    ResourceCostEvaluator.Samples / ResourceCostEvaluator.Samples.PlayMode.Tests
    ├── NumericRequirementEvaluator/
        ├── Runtime/     NumericRequirementEvaluator.Runtime
        ├── Tests/       NumericRequirementEvaluator.Tests
        └── Samples~/    NumericRequirementEvaluator.Samples / NumericRequirementEvaluator.Samples.PlayMode.Tests
    ├── UtilityScoreEvaluator/
        ├── Runtime/     UtilityScoreEvaluator.Runtime
        ├── Tests/       UtilityScoreEvaluator.Tests
        └── Samples~/    UtilityScoreEvaluator.Samples / UtilityScoreEvaluator.Samples.PlayMode.Tests
    ├── StableScoreSelector/
        ├── Runtime/     StableScoreSelector.Runtime
        ├── Tests/       StableScoreSelector.Tests
        └── Samples~/    StableScoreSelector.Samples / StableScoreSelector.Samples.PlayMode.Tests
    ├── WeightedIntegerAllocator/
        ├── Runtime/     WeightedIntegerAllocator.Runtime
        ├── Tests/       WeightedIntegerAllocator.Tests
        └── Samples~/    WeightedIntegerAllocator.Samples / WeightedIntegerAllocator.Samples.PlayMode.Tests
    ├── StackTransferPlanner/
        ├── Runtime/     StackTransferPlanner.Runtime
        ├── Tests/       StackTransferPlanner.Tests
        └── Samples~/    StackTransferPlanner.Samples / StackTransferPlanner.Samples.PlayMode.Tests
    ├── TimedStackResolver/
        ├── Runtime/     TimedStackResolver.Runtime
        ├── Tests/       TimedStackResolver.Tests
        └── Samples~/    TimedStackResolver.Samples / TimedStackResolver.Samples.PlayMode.Tests
    ├── PeriodicTickPlanner/
        ├── Runtime/     PeriodicTickPlanner.Runtime
        ├── Tests/       PeriodicTickPlanner.Tests
        └── Samples~/    PeriodicTickPlanner.Samples / PeriodicTickPlanner.Samples.PlayMode.Tests
    ├── DamageMitigationEvaluator/
        ├── Runtime/     DamageMitigationEvaluator.Runtime
        ├── Tests/       DamageMitigationEvaluator.Tests
        └── Samples~/    DamageMitigationEvaluator.Samples / DamageMitigationEvaluator.Samples.PlayMode.Tests
    └── ThreatScoreResolver/
        ├── Runtime/     ThreatScoreResolver.Runtime
        ├── Tests/       ThreatScoreResolver.Tests
        └── Samples~/    ThreatScoreResolver.Samples / ThreatScoreResolver.Samples.PlayMode.Tests
```

UPM パッケージとして扱う場合は、モジュールのフォルダを `Packages/` 以下に置くか、
`manifest.json` からローカルパスで参照する。

---

## 各モジュールの約束

- **依存を明記する** — リポジトリ内モジュールへの依存と導入順を各 README に書く
- **`unsafe` を使わない** — asmdef の設定を変えずに導入できる
- **アセンブリ定義を持つ** — 使わないモジュールはビルドに入らない
- **`.meta` を含む** — GUID が変わらないので、参照が壊れない
