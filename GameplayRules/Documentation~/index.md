# Gameplay Rules

## Boundaryとデータフロー

- Input: 検証済みの有限値、整数量、明示tick、正規化sample、policy列挙値。
- State: callerが所有する値型と局所object。必要な履歴やentryは明示容量のcollectionにだけ保持し、Unityの時計やglobal stateは読みません。
- Output: 前後値、要求・実適用・未適用delta、per-line／per-stepの内訳、成否と明示error。

Unityのframe、時刻、`deltaTime`、乱数、global state、UIは境界外です。同じ入力列は同じ出力列になります。

## 名前空間の分担

| 名前空間 | 担当する計算 |
|---|---|
| `GameplayResources` | resource量の回復・消費、複数resourceのcost支払い可否と不足量 |
| `GameplayStats` | flat・加算percent・乗算factorの段階的な能力値補正 |
| `GameplaySelection` | 重み付きentryからの1件選択 |
| `GameplayAllocation` | 整数総量の重み按分（最大剰余法） |
| `GameplayMath` | 折れ線curveの補間と端点clamp |
| `GameplayMetrics` | 固定長FIFO windowのsample保持と要約 |
| `GameplayAnalysis` | 要約統計と最小二乗の傾向推定 |
| `GameplayProgression` | 閾値tierの判定と進捗率 |
| `GameplayTiming` | charge recharge、定期発火の計画と追いつき |
| `GameplayEffects` | 時限stackの再付与解決 |
| `GameplayInventory` | source・destination間の整数移送計画 |
| `GameplayRules` | 数値条件のline単位判定 |
| `GameplayDecision` | utility scoringとヒステリシス付き安定選択 |
| `GameplayDamage` | mitigation層の順次適用 |
| `GameplayThreat` | 敵対度scoreの増減とleader決定 |

## 失敗時の不変条件

NaN・Infinity・負値・範囲外・重複ID・件数超過・不正policyは、例外ではなく明示errorとして返ります。検査は適用の前に完了するため、失敗時に部分適用されたstateや無効なinstanceが残りません。

## 検証

EditMode testは`GameplayRules.Editor.Tests`にまとまり、境界値、clamp、不足、順序安定性、不正入力での非変更、結果のequality、再現可能なsequenceを確認します。19個のBasics sampleは実Buttonでの操作結果と、960×600／640×360の実描画geometry、`timeScale=0`での再現性をPlayMode testで確認します。

## 互換性

C#の名前空間、型名、member名、動作は統合前の19 packageと同一で、source / API互換です。runtime assembly名は`GameplayRules.Runtime`へ変わるためbinary互換ではありません。自作asmdefのReferences変更と、旧assemblyを参照するprecompiled DLLの再buildが必要です。Threat Score Resolverを除く18packageの公開済みtagは旧配布単位と旧UPM識別子を継続利用する入口として残ります。単独tagがなかったThreat Score Resolverには旧UPM識別子を指定する旧配布入口がなく、本packageで初めてtag付き配布になります。
