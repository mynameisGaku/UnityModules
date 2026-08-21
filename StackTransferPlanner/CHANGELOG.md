# Changelog

## [1.0.0] - 2026-08-21

- 最大32 sourceと32 destinationへ整数unit移送を計画するstateless API
- source不足・destination空き不足・要求量から部分移送量を決定
- 入力順のsource減算明細とdestination加算明細
- null・件数・ID・重複・unit・capacity境界の明示error
- Full、Partial、Source limit、Destination limit、Zero requestを確認するresponsive UI Toolkit sample
