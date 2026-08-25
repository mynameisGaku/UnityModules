# Perf Meter Basics

空Sceneで空GameObjectに`PerfMeterBasicsController`を追加しPlayしてください。

1. 起動すると画面左上にoverlayが表示されます。fps(avg)、last/min/max、median/stddev、spikes、memoryを行ごとに確認できます。
2. `Heavy Frame (120ms)`buttonでmain threadを約120ms停止させ、人工的なスパイクを発生させてHUDで確認します。直後のframeはoverlayが赤になり、last/maxが約120msへ跳ね上がり、spikes合計が増えます。
3. `Reset Stats`buttonで統計とspike計数が0へ戻ります。
4. `PerfMeterBasicsController`は同GOの`PerfMeterComponent`を要求するだけです。component単体でも計測とoverlay表示は動作します。

シーン(.unity)は同梱しません。PlayMode test（`PerfMeterBasicsControllerTests`）が人工spikeの計上とresetを検証します。
