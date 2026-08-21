# Changelog

## [1.0.0] - 2026-08-21

### Added

- 最大32候補の0〜1 scoreとcurrent IDから維持・切替を決めるstateless selector
- 同点と小さな優位差ではcurrentを維持し、明示minimum advantage以上でだけ切り替える契約
- current消失時のbest候補復帰と、入力順を使う安定tie-break
- current・best・challenger・selected・判断理由と入力順全明細を再構築できるimmutable result
- null・件数・ID・重複・score・current・minimum advantageを部分結果なしで区別するerror契約
- 境界、微差、同点、消失復帰、入力/結果不変、公開型面を検証するEditMode tests
- select・keep・switch・tie・missingとwide/narrow実Panelを確認するBasics sample
