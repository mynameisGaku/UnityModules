# Damage Mitigation Evaluator 1.0.0

## Purpose

combat計算、preview UI、AI判断、replay検証が同じdamage軽減内訳を得られるよう、元damageと明示した軽減層だけから入力順の評価結果を構築します。HP更新や防御statの合成は呼び出し側に残します。

## Behavior

`TryEvaluate`は0以上の有限damageと0〜32件の`DamageMitigationLayer`を受け取ります。各層は正の一意なID、`FlatReduction`または`RatioReduction`、有限の非負valueを持ちます。率軽減のvalueだけは0〜1です。

固定軽減は現在damageからvalueを引きます。率軽減は現在damageへvalueを掛けた量を引きます。要求軽減量が現在damageを超える場合は、実適用量を現在damageまでに制限し、outputを0にします。後続層も入力順の明細として残ります。

成功時の`DamageMitigationEvaluation`は元damage、最終damage、実軽減合計、全軽減判定、step件数を保持します。`TryGetStep`は入力順にlayer ID、kind、value、input、requested、applied、output、clamp状態を返します。

## Errors and observability

非有限damage、負damage、null配列、件数超過、非正ID、ID重複、未定義kind、非有限value、負value、率範囲超過を`DamageMitigationError`で区別します。失敗時はevaluationをnullにし、部分明細を返しません。

## Determinism and ownership

validationと計算は配列indexの昇順に実行します。同じbinary64入力と同じ順序は同じ演算順を通ります。Evaluatorは入力配列、Unity object、global state、HP、shield、armorを所有または変更しません。

## Limits

damage type、critical、armor式、貫通、吸収、反射、最低保証、優先度sort、random、effect探索、state適用、network同期、永続化はv1対象外です。callerが自身の設計に合う層と順序を組み立ててください。

## Verification

EditMode testsはdamage・件数・ID・kind・有限値・範囲・重複、flat、ratio、入力順差、0下限、zero層、最大32件、最大有限値、入力不変、結果明細を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果と960×600・640×360の実描画を検証します。
