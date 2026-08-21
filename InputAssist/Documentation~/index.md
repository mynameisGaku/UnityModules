# Input Assist 1.0.0

## 目的

入力補正の小さな処理を個別packageとして選ぶ手間を減らし、2D入力とボタンgestureの一般的な組合せを2つのInspector設定へまとめます。

## 依存方向

```text
Input System / Legacy Input / AI / Replay
                    ↓
        raw Vector2 / bool / deltaTime
                    ↓
       InputVectorFilter / InputButtonTracker
                    ↓
       filtered value / direction / events
                    ↓
             Gameplay / UI / Character
```

処理器は`Time`、Input System、singleton、Scene状態を読みません。同じ初期状態、入力列、経過時間から同じ結果を作れます。

## InputVectorFilter

1. radial inner・outer dead zone
2. normalized magnitude response curve
3. explicit delta timeによるrise・fall rate limit
4. neutral / 4-way / 8-way direction classification

`RiseSpeed`または`FallSpeed`が0の場合、その方向のrate limitを無効化してtargetへ即時追従します。

## InputButtonTracker

- edge: `Pressed`, `Released`
- duration: `HoldStarted`
- repeat: `Repeated`, `RepeatCount`
- short-press burst: `TapCompleted`, `TapCount`

repeatの大きな時間jumpは1回の更新につき32件へ制限します。残りは次回以降へ持ち越されるため、異常なframe停止で無制限loopしません。

## 状態の再構築

- `InputVectorFilter.Reset()` / `TryReset(Vector2, out error)`
- `InputButtonTracker.Reset()`

Scene切替、Replay seek、testのarrange時に明示的に状態を戻せます。

## 非目標

Input Actionの購読、device pairing、rebind、Action Map停止、入力record、command comboは扱いません。呼び出し側または専用moduleへ残し、入力値の補正とgesture判定へ責務を限定します。
