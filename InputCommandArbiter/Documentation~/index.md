# Input Command Arbiter 1.0.0

## 分解

- Input: 正のcommand id、整数priority、eligible flagを持つ最大64件のordered list
- State: なし。仲裁結果は毎回の入力だけから再計算可能
- Output: 選択有無、入力index、command id、priority、eligible数、明示error

入力device、Unity frame、時刻、random、global stateは境界外です。

## 将来問題を固定する規則

### 検証を選択より先に行う

全candidateのcommand idが正で一意かを先に検証します。ineligible候補も検証対象なので、現在隠れている構成不備が後のeligible切替で突然現れることを防ぎます。null、65件以上、不正id、重複idは選択を返しません。

### Priority

eligible候補だけを走査し、整数priorityが最大の候補を選びます。`int.MinValue`と`int.MaxValue`も通常値として扱い、加算や減算はしません。

### Tie-break

priority同値ではordered listの小さいindexを選びます。並べ替えやhash列挙を行わないため、同じ入力順から同じ結果を再現できます。

### 未選択

候補0件またはeligible候補0件はerrorではありません。`Succeeded=true`、`HasSelection=false`、`SelectedIndex=-1`を返します。失敗結果と正常な未選択を区別するため、`Succeeded`と`Error`を確認します。

## 状態の再構築

Runtimeはstateを保持しません。入力candidate listが完全なsnapshotであり、同じsnapshotを再度渡せば同じ結果を再構築できます。`EligibleCandidateCount`と選択indexにより、外部debuggerやReplay検証から判断過程を観測できます。

## 非目標

Input System読取、command生成、edge、repeat、tap・hold・multi-tap・chord・sequence、tick・実時間、queue、buffer消費、callback、dynamic priority、global service、file I/O、network transportは含めません。

## 検証

EditModeでnull・件数上限・不正id・重複id、全ineligible、priority全域、最大priority、先頭tie-break、入力順変更、結果equality、golden候補列を検証します。sampleとMono/IL2CPP Playerは960×600の5列・640×360の3+2列を実描画し、timeScale=0で同じ5操作を再現します。
