# Stable Score Selector 1.0.0

## Purpose

AI、target選択、自動行動、UI推薦等で候補scoreを継続的に再計算した時、わずかな差で選択が往復する問題を、明示したcurrent IDと最小score優位差だけで決定論的に抑えます。score算出とaction実行は呼び出し側に残します。

## Behavior

`TrySelect`は1〜32件の`StableScoreCandidate`を入力順に検証します。候補IDは正の整数かつ配列内で一意、scoreは有限な0〜1です。current IDは0を未選択として扱い、負値を拒否します。minimum advantageは有限な0〜1です。

入力全体の最高score候補と、current以外の最高score challengerをそれぞれ入力順で探索します。bit一致する同scoreでは先の入力indexをbestまたはchallengerにします。

currentが存在する場合、challenger scoreがcurrent scoreより厳密に高く、差がminimum advantage以上の時だけ切り替えます。minimum advantageが0でも同点ではcurrentを維持します。currentが0または入力に無い場合はbest候補を選択します。

## Result and observability

成功時の`StableScoreSelection`はrequested current、currentの存在・index・score、best候補、challenger、score優位差、minimum advantage、selected候補、維持・切替理由を保持します。

`TryGetCandidateLine`は候補入力順にID・index・scoreと、current・best・selectedの各flagを返します。利用側は最終IDだけでなく、なぜ維持または切り替えたかを再構築できます。

null配列、候補数、current ID、minimum advantage、候補ID、候補ID重複、scoreの不正を`StableScoreError`で区別します。失敗時はselectionをnullにし、部分明細を返しません。

## Determinism and ownership

validation、best探索、challenger探索、明細構築は候補index昇順です。同じbit列の入力は同じ比較順とtie-breakを通ります。結果は入力値を独立した明細へ複製し、Selectorは入力、Unity object、global stateを所有または変更しません。

## Limits

score計算、World/Scene探索、utility curve、候補生成、action実行、random、ranking、priority、cooldown、時間lock、履歴、内部state、callback、network同期、永続化はv1対象外です。

## Verification

EditMode testsはnull・件数・ID・重複・score・current・minimum advantage、最高候補、challenger、同点維持、微差維持、境界切替、current消失、最大件数、全明細、入力/結果不変、決定論、公開型面を検証します。sample testsとMono／IL2CPP Player gateは5つの実Button結果とwide/narrow実描画を検証します。
