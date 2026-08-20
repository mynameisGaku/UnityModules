# State Fingerprint Basics

Sceneを開くと、明示した5 fieldから作るSHA-256 fingerprintを確認できます。

- `Build Fingerprint`: 同じstateから同じ値を再構築
- `Damage -10`: HealthとTickを変えて差分を確認
- `Move +0.25`: doubleのraw bit列を含む差分を確認
- `Replay Snapshot`: 一時変更後にfieldを復元し、元のfingerprintと完全一致することを確認
- `Reset State`: 初期field列とbaseline fingerprintへ戻す

