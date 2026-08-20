# Changelog

## [1.0.0] - 2026-08-20

### Added

- owner付きAudioSource poolと1から32のvoice上限。
- volume、pitch、loop、非スケールfade-in、priority、steal許可を持つ変更不能な再生要求。
- priorityと開始順による決定論的stealと、低priority要求の安全な拒否。
- generation付きhandle、任意スレッドDispose、明示fade-out、lifecycle cleanup。
- EditMode、PlayMode、ready-to-open Basics、実PanelSettingsの960x600 / 640x360 geometry検証。
