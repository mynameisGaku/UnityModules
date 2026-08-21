# Periodic Tick Planner Basics

定期発火cursorを指定simulation tickまで進める計画を実Buttonで確認する設定済みSceneです。

1. Futureは次回tickより前を評価し、発火0件でcursorを維持します。
2. Exactは次回tickちょうどを評価し、1件を発火します。
3. Catch-upは10・14・18・22の4件をまとめて計画します。
4. Limitedは到来10件を今回上限3件へ分割し、残り7件を次cursorへ保持します。
5. Completeは残り3件をすべて発火してcanonical完了cursorへ遷移します。

UI Toolkitの実Button callbackを使い、960×600の1列と640×360の3+2列をPlayMode testで検証します。
