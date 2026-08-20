# Changelog

## [1.0.0] - 2026-08-14

### Added

- `PlayerInput`の実行中Action Map instanceを所有する`InputGateController`。
- Actionごとの停止前状態を保存し、入れ子leaseの最後の解放で復元する契約。
- worker threadから安全に解放できる、世代分離された`InputGateLease`。
- Action Map重複所有、外部有効化、Action Asset交換、通知再入、lifecycle終了の明示的な失敗状態。
- Gameplay map停止中もUI mapが継続する、設定済みInput ActionsとUI ToolkitのBasics sample。
- 純粋なlease世代テストと、実Input System Action Mapを使うPlayMode契約テスト。

### Boundaries

- Input System 1.20.0へ依存します。
- Legacy Input、EventSystem遮断、入力buffer消去、rebind、device pairing、Scene自動連携、global singletonは含みません。
