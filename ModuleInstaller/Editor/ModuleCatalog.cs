// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace ModuleInstaller.Editor
{
    internal static class ModuleCatalog
    {
        private static readonly string[] InputAssistLegacyPackages =
        {
            "com.studiogaku.input-radial-dead-zone",
            "com.studiogaku.input-vector-response-curve",
            "com.studiogaku.input-vector-slew-limiter",
            "com.studiogaku.input-vector-exponential-smoother",
            "com.studiogaku.input-vector-direction-limiter",
            "com.studiogaku.input-vector-weighted-mixer",
            "com.studiogaku.input-direction-quantizer",
            "com.studiogaku.input-quantizer",
            "com.studiogaku.input-threshold-classifier",
            "com.studiogaku.input-press-classifier",
            "com.studiogaku.input-repeat",
            "com.studiogaku.input-multi-tap-classifier"
        };

        private static readonly string[] InputAssistLegacyFolders =
        {
            "InputRadialDeadZone",
            "InputVectorResponseCurve",
            "InputVectorSlewLimiter",
            "InputVectorExponentialSmoother",
            "InputVectorDirectionLimiter",
            "InputVectorWeightedMixer",
            "InputDirectionQuantizer",
            "InputQuantizer",
            "InputThresholdClassifier",
            "InputPressClassifier",
            "InputRepeat",
            "InputMultiTapClassifier"
        };

        private static readonly string[] InputCommandLegacyPackages =
        {
            "com.studiogaku.input-command-buffer",
            "com.studiogaku.input-sequence-matcher",
            "com.studiogaku.input-chord-matcher",
            "com.studiogaku.input-command-arbiter",
            "com.studiogaku.input-axis-conflict-resolver",
            "com.studiogaku.input-stabilizer"
        };

        private static readonly string[] InputCommandLegacyFolders =
        {
            "InputCommandBuffer",
            "InputSequenceMatcher",
            "InputChordMatcher",
            "InputCommandArbiter",
            "InputAxisConflictResolver",
            "InputStabilizer"
        };

        private static readonly string[] GameplayRulesLegacyPackages =
        {
            "com.studiogaku.resource-meter",
            "com.studiogaku.resource-cost-evaluator",
            "com.studiogaku.stat-modifier-stack",
            "com.studiogaku.weighted-choice-table",
            "com.studiogaku.weighted-integer-allocator",
            "com.studiogaku.piecewise-linear-curve",
            "com.studiogaku.rolling-sample-window",
            "com.studiogaku.sample-statistics",
            "com.studiogaku.linear-trend-estimator",
            "com.studiogaku.threshold-tier-table",
            "com.studiogaku.charge-cooldown",
            "com.studiogaku.periodic-tick-planner",
            "com.studiogaku.timed-stack-resolver",
            "com.studiogaku.stack-transfer-planner",
            "com.studiogaku.numeric-requirement-evaluator",
            "com.studiogaku.utility-score-evaluator",
            "com.studiogaku.stable-score-selector",
            "com.studiogaku.damage-mitigation-evaluator",
            "com.studiogaku.threat-score-resolver"
        };

        private static readonly string[] GameplayRulesLegacyFolders =
        {
            "ResourceMeter",
            "ResourceCostEvaluator",
            "StatModifierStack",
            "WeightedChoiceTable",
            "WeightedIntegerAllocator",
            "PiecewiseLinearCurve",
            "RollingSampleWindow",
            "SampleStatistics",
            "LinearTrendEstimator",
            "ThresholdTierTable",
            "ChargeCooldown",
            "PeriodicTickPlanner",
            "TimedStackResolver",
            "StackTransferPlanner",
            "NumericRequirementEvaluator",
            "UtilityScoreEvaluator",
            "StableScoreSelector",
            "DamageMitigationEvaluator",
            "ThreatScoreResolver"
        };

        private static readonly string[] DeterministicSimulationLegacyPackages =
        {
            "com.studiogaku.simulation-clock",
            "com.studiogaku.deterministic-random",
            "com.studiogaku.state-fingerprint",
            "com.studiogaku.replay-tape",
            "com.studiogaku.canonical-payload",
            "com.studiogaku.fixed-point",
            "com.studiogaku.generational-handle"
        };

        private static readonly string[] DeterministicSimulationLegacyFolders =
        {
            "SimulationClock",
            "DeterministicRandom",
            "StateFingerprint",
            "ReplayTape",
            "CanonicalPayload",
            "FixedPoint",
            "GenerationalHandle"
        };

        private static readonly ModuleCatalogEntry[] CatalogEntries =
        {
            Entry("com.studiogaku.project-setup", "ProjectSetup", "project-setup-v1.15.0", "プロジェクト一括設定", "推奨フォルダー、実行用・エディター用・任意の試験用アセンブリ定義、Unity向け版管理ファイルを作成します。プロジェクト設定、対象機種別のアプリ識別子・実行方式・API互換範囲・管理コード削減強度・IL2CPP生成方針、コード生成規則、複製時の命名規則、条件付きコンパイル記号、タグ、レイヤー、ビルド対象シーン、再生開始シーンは、差分確認、控えの保存、反映、復元を経て変更します。", guideRelativePath: "Documentation~/index.md"),
            Entry("com.studiogaku.inspector", "Inspector", "inspector-v1.0.0", "インスペクター入力補助", "インスペクターの入力を整理し、値を検証します。"),
            Entry("com.studiogaku.drawing", "Drawing", "drawing-v1.0.0", "デバッグ描画", "実行中に線、図形、経路、文字を描画します。"),
            Entry("com.studiogaku.build-guard", "BuildGuard", "build-guard-v1.4.0", "プロジェクト不備確認・修復", "シーンとプレハブの欠落参照を検出し、修復します。"),
            Entry("com.studiogaku.reference-finder", "ReferenceFinder", "reference-finder-v1.3.0", "アセット参照整理", "アセット参照の検索・置換と名前の一括変更を行います。"),
            Entry("com.studiogaku.asset-import-audit", "AssetImportAudit", "asset-import-audit-v1.1.0", "テクスチャー取込設定監査", "テクスチャーの共通設定と、Standalone、Android、iOS向け設定の差分を確認してから一括反映します。"),
            Entry("com.studiogaku.build-assistant", "BuildAssistant", "build-assistant-v1.0.0", "ビルド実行アシスタント", "デスクトップ向けビルド計画を確認して新しい出力フォルダーへ実行し、件数を制限した履歴、容量差、JSON形式の報告を記録します。"),
            Entry("com.studiogaku.scene-workspace", "SceneWorkspace", "scene-workspace-v1.0.0", "シーン作業セット", "複数シーンの順番と読込状態を記録し、差分確認、計画の有効性確認、切り替え後の検証、失敗時の復元報告を伴って安全に切り替えます。"),
            Entry("com.studiogaku.play-mode-tuning", "PlayModeTuning", "play-mode-tuning-v1.0.0", "実行中調整", "再生中に選んで記録したプロパティー変更を、差分確認、計画の有効性確認、失敗時の復元報告を経て保存済みシーンへ反映します。"),
            Entry("com.studiogaku.scene-flow", "SceneFlow", "scene-flow-v1.0.0", "シーン切り替え", "シーンの読込、使用開始、解放を順番に実行します。"),
            Entry("com.studiogaku.screen-transition", "ScreenTransition", "screen-transition-v1.0.1", "画面切り替え演出", "UI Toolkitの覆いを使って画面を隠し、表示します。"),
            Entry("com.studiogaku.adaptive-layout", "AdaptiveLayout", "adaptive-layout-v1.0.0", "安全領域レイアウト", "切り欠きや表示領域の変化に合わせ、画面部品を安全領域内へ収めます。"),
            Entry("com.studiogaku.time-control", "TimeControl", "time-control-v1.0.0", "ゲーム時間制御", "一時停止、低速再生、高速再生をまとめて調整します。"),
            Entry("com.studiogaku.startup-flow", "StartupFlow", "startup-flow-v1.0.0", "起動手順", "起動処理を決められた順番で実行します。"),
            Entry("com.studiogaku.save-system", "SaveSystem", "save-system-v1.0.0", "セーブデータ", "型を指定したJSON保存枠、破損検査、控えからの復旧を提供します。"),
            Entry("com.studiogaku.audio-control", "AudioControl", "audio-control-v1.0.0", "音声再生", "再利用する音声枠、同時数制限、優先度、操作用識別子、音量変化を制御します。"),
            Entry("com.studiogaku.diagnostics-context", "DiagnosticsContext", "diagnostics-context-v1.0.0", "不具合報告記録", "件数を制限した状況情報、操作履歴、Unityの記録をJSON形式で書き出します。"),
            Entry("com.studiogaku.input-assist", "InputAssist", "input-assist-v2.0.0", "入力補助", "スティックとトリガーの無効範囲、反応曲線、変化速度、方向の段階化、加重合成を整え、ボタンの短押し、長押し、連続入力、複数回入力を判定します。",
                InputAssistLegacyPackages,
                InputAssistLegacyFolders),
            Entry("com.studiogaku.input-command", "InputCommand", "input-command-v1.0.0", "入力コマンド", "入力命令の一時保持、刻み単位の並び判定、同時押しと反対軸の判定、状態を持たない優先選択、入力標本を使う安定化を提供します。",
                InputCommandLegacyPackages,
                InputCommandLegacyFolders),
            Entry("com.studiogaku.input-gate", "InputGate", "input-gate-v1.0.0", "入力一時停止", "指定したInput Systemのアクションマップを一時的に無効化します。"),
            Entry("com.studiogaku.gameplay-rules", "GameplayRules", "gameplay-rules-v1.0.0", "ゲーム規則", "資源と消費、能力値補正、重み付き選択と配分、曲線と段階、標本統計と傾向、時間制限付きの積み重ねと定期処理、条件、効用・脅威点、損害軽減を計算します。",
                GameplayRulesLegacyPackages,
                GameplayRulesLegacyFolders),
            Entry("com.studiogaku.deterministic-simulation", "DeterministicSimulation", "deterministic-simulation-v1.0.0", "決定論的シミュレーション", "固定刻み時計、再現できる乱数状態、正規化データ表現、固定小数点計算、再生記録、状態照合値、世代付き識別子を、再現性の基盤としてまとめます。",
                DeterministicSimulationLegacyPackages,
                DeterministicSimulationLegacyFolders)
        };

        private static readonly ModuleBundle[] CatalogBundles =
        {
            Bundle("project-maintenance", "プロジェクト整備", "新しいプロジェクトを初期設定し、アセットとテクスチャー取込設定を整理し、確認済みのデスクトップ向けビルドを実行します。", ModuleBundleTier.Recommended,
                "プロジェクトの開始時、全体設定やアセットの整理時、デスクトップ向け単体ビルドの準備時に使います。",
                "まず Tools > Project Setup > Open で初期設定を行い、Tools > Asset Import Audit > Open でテクスチャーを検査し、最後に Tools > Build Assistant > Open で公開用ビルド計画を確認します。",
                "導入操作が変更するのはパッケージ設定だけです。プロジェクト一括設定とテクスチャー取込設定監査は、明示的な差分確認と反映操作の後にだけ選択項目を変更します。ビルド実行アシスタントは、内容確認と実行確認の後にだけ新しいビルド出力と件数制限付きのLibrary履歴を書き出します。",
                "com.studiogaku.project-setup", "com.studiogaku.asset-import-audit", "com.studiogaku.inspector", "com.studiogaku.drawing", "com.studiogaku.build-guard", "com.studiogaku.reference-finder", "com.studiogaku.build-assistant"),
            Bundle("scene-and-ui", "シーンと画面", "再利用できるエディターのシーン作業構成と、確認済みの再生中調整を用意し、実行時のシーン切り替え、画面演出、一時停止、起動手順を整えます。", ModuleBundleTier.Recommended,
                "複数シーンの編集構成を切り替える場合、再生中の調整を保存済みシーンへ残す場合、シーン切り替え、画面の明暗、安全領域、一時停止、起動順をまとめて実装する場合に使います。",
                "再利用するシーン構成は Tools > Scene Workspace > Open から始めます。調整値を残す場合は Tools > Play Mode Tuning > Open を開き、再生前に対象を選び、再生中に手動記録し、再生終了後の差分確認を経てから反映を確定します。",
                "導入操作が変更するのはパッケージ設定だけです。シーン作業セットは、差分確認と確定の後にだけエディター上のシーン順、読込状態、使用中シーンを変更し、シーンの保存や未保存変更の破棄は行いません。実行中調整は、有効な同一の確認済み計画を一度だけ反映した場合に選択値を変更し、シーンを未保存状態にしますが保存は行いません。反映結果と復元結果は別々に報告します。実行時の動作は、部品やサービスを追加して設定した後にだけ始まります。",
                "com.studiogaku.scene-workspace", "com.studiogaku.play-mode-tuning", "com.studiogaku.scene-flow", "com.studiogaku.screen-transition", "com.studiogaku.adaptive-layout", "com.studiogaku.time-control", "com.studiogaku.startup-flow"),
            Bundle("game-services", "ゲーム共通機能", "セーブデータ、制御可能な音声再生、手動の不具合報告を追加します。", ModuleBundleTier.Recommended,
                "セーブ枠、音声再生枠、利用者が作成する不具合報告を、複数箇所から共用する場合に使います。",
                "最初に必要な機能の見本を取り込み、アプリケーションまたは起動用シーンに、寿命を管理する所有者を一つ作ります。",
                "導入操作が変更するのはパッケージ設定だけです。セーブファイルと報告ファイルは、対応するサービスを作成して呼び出した場合にだけ書き出されます。",
                "com.studiogaku.save-system", "com.studiogaku.audio-control", "com.studiogaku.diagnostics-context"),
            Bundle("input-support", "入力処理", "スティックとボタン入力を整え、一時保持した入力コマンドを認識し、ゲーム操作用マップを一時停止します。", ModuleBundleTier.Recommended,
                "Input Systemから受け取る値へ一貫したスティック補正、ボタン操作の判定、所有者の寿命に連動するゲーム操作停止が必要な場合に使います。",
                "まず入力補助の見本を取り込んで出力値を確認し、入力の一時保持や複数ボタン操作が必要な箇所へ入力コマンドを追加します。ゲーム操作用マップを止める箇所にだけ入力一時停止を追加します。",
                "導入操作は各パッケージと、宣言済みのInput System依存関係を追加します。実行時の入力マップが変わるのは、設定済みの所有者が有効な間だけです。",
                "com.studiogaku.input-assist", "com.studiogaku.input-command", "com.studiogaku.input-gate"),
            Bundle("deterministic-simulation", "決定論的シミュレーション", "固定刻み、再現できる乱数状態、再生記録、安定した識別子を組み合わせます。", ModuleBundleTier.Specialized,
                "再生、同期進行、再現可能な試験、決定論的な状態比較が具体的な要件になった場合だけ使います。",
                "パッケージを導入し、まずシミュレーション時計の見本を取り込みます。状態の約束事を試験で固定してから、名前空間ごとに一つずつ採用します。",
                "導入操作が変更するのはパッケージ設定だけです。これらの計算部品は、全体共有の実行時所有者を作らず、プロジェクト設定も変更しません。",
                "com.studiogaku.deterministic-simulation"),
            Bundle("game-rules", "ゲーム規則と計算", "資源、能力値、選択、時間、損害を決定論的に計算する部品群を追加します。", ModuleBundleTier.Specialized,
                "名前の付いたゲーム規則に、プロジェクト固有のサービスではなく、小さく決定論的な値型が必要な場合に使います。",
                "パッケージを導入し、READMEの名前空間一覧を確認して、実装する規則に合う名前空間だけを使います。",
                "導入操作が変更するのはパッケージ設定だけです。各部品は明示的に呼び出す計算であり、シーンやUnity全体の状態を更新しません。",
                "com.studiogaku.gameplay-rules")
        };

        internal static IReadOnlyList<ModuleCatalogEntry> Entries => CatalogEntries;
        internal static IReadOnlyList<ModuleBundle> Bundles => CatalogBundles;

        internal static bool TryFindEntry(string packageName, out ModuleCatalogEntry entry)
        {
            for (var index = 0; index < CatalogEntries.Length; index++)
            {
                if (string.Equals(CatalogEntries[index].PackageName, packageName, StringComparison.Ordinal))
                {
                    entry = CatalogEntries[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        internal static bool TryFindBundle(string id, out ModuleBundle bundle)
        {
            for (var index = 0; index < CatalogBundles.Length; index++)
            {
                if (string.Equals(CatalogBundles[index].Id, id, StringComparison.Ordinal))
                {
                    bundle = CatalogBundles[index];
                    return true;
                }
            }

            bundle = null;
            return false;
        }

        private static ModuleCatalogEntry Entry(
            string packageName,
            string folderName,
            string tag,
            string displayName,
            string summary,
            string[] legacyPackageNames = null,
            string[] legacyFolderNames = null,
            string guideRelativePath = "README.md")
        {
            return new ModuleCatalogEntry(packageName, folderName, tag, displayName, summary, legacyPackageNames, legacyFolderNames, guideRelativePath);
        }

        private static ModuleBundle Bundle(
            string id,
            string displayName,
            string summary,
            ModuleBundleTier tier,
            string useWhen,
            string firstStep,
            string changeScope,
            params string[] packageNames)
        {
            return new ModuleBundle(id, displayName, summary, tier, useWhen, firstStep, changeScope, packageNames);
        }
    }
}
