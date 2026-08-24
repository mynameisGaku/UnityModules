# Simulation Clock Basics

`SimulationClockBasics.unity`を開いてPlayしてください。

- `Advance 16 ms`: 20ms stepに満たない端数を保持
- `Advance 33 ms`: 蓄積端数と合わせて連続stepを返す
- `Hitch 500 ms`: 25step相当を4stepへ制限し、21stepを明示drop
- `Replay Pattern`: 2つの時計へ同じ整数入力列を渡し、結果と最終状態を比較
- `Reset`: 時計状態、入力履歴、Replay判定を初期化

このsampleはUnity時間をRuntime時計内部へ隠しません。実ゲームで`Time.unscaledDeltaTime`を使う場合もadapter側で整数tickへ変換し、Replayには変換後の整数列を保存してください。
