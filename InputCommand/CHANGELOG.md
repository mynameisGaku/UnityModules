# Changelog

## [1.0.0] - 2026-08-24

### Added

- 6つの独立packageを1つの導入単位`com.studiogaku.input-command`へ統合し、`ulong` tickと`int` command idを共有する1本のpipelineとして配布
- 統合対象は`com.studiogaku.input-command-buffer`、`com.studiogaku.input-sequence-matcher`、`com.studiogaku.input-chord-matcher`、`com.studiogaku.input-command-arbiter`、`com.studiogaku.input-axis-conflict-resolver`、`com.studiogaku.input-stabilizer`
- 単一assembly`InputCommand.Runtime`と単一EditMode assembly`InputCommand.Tests`へ統合し、module間の変換codeなしで直接接続できる状態を提供
- 6つのSampleを`Command Buffer Basics`、`Sequence Matcher Basics`、`Chord Matcher Basics`、`Command Arbiter Basics`、`Axis Conflict Resolver Basics`、`Stabilizer Basics`として同梱

### Changed

- Sample assemblyの参照先を旧runtime assembly名から`InputCommand.Runtime`へ変更

### Compatibility

- C# namespace(`InputBuffering`、`InputSequencing`、`InputChording`、`InputArbitration`、`InputAxisConflict`、`InputStabilization`)、型名、member、動作は変更なし
- 旧packageの公開済みtagとUPM識別子は互換入口として維持
