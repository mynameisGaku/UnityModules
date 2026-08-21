# Charge Cooldown Basics

1. `Reset`はtick 100で3/3 chargeへ戻します。
2. `Spend`を3回押すと0/3になり、next rechargeは110のままです。
3. 空の状態で`Spend`しても失敗ではなく、`ChargeSpent == false`になります。
4. `Advance +9`では回復せず、`+1`で境界tick 110の1 chargeが回復します。
5. `Advance +25`はtick 135までの残り2 chargeをcatch upし、満量でscheduleを解除します。

sampleはUnity時刻をcooldown入力に使いません。960×600では5 Buttonを1列、640×360では3+2列に配置します。
