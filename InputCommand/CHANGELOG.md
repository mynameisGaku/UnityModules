# Changelog

## [1.0.0] - 2026-08-24

### Added

- 6つの独立packageを1つの導入単位`com.studiogaku.input-command`へ統合し、明示tickで進む判定、sample回数で進む安定化、状態を持たない優先順位選択を独立部品として配布
- 統合対象は`com.studiogaku.input-command-buffer`、`com.studiogaku.input-sequence-matcher`、`com.studiogaku.input-chord-matcher`、`com.studiogaku.input-command-arbiter`、`com.studiogaku.input-axis-conflict-resolver`、`com.studiogaku.input-stabilizer`
- 単一assembly`InputCommand.Runtime`と単一EditMode assembly`InputCommand.Editor.Tests`へ統合し、関連機能を1回の導入で選択できる状態を提供
- 6つのSampleを`Command Buffer Basics`、`Sequence Matcher Basics`、`Chord Matcher Basics`、`Command Arbiter Basics`、`Axis Conflict Resolver Basics`、`Stabilizer Basics`として同梱

### Changed

- Sample assemblyの参照先を旧runtime assembly名から`InputCommand.Runtime`へ変更

### Compatibility

- C# namespace(`InputBuffering`、`InputSequencing`、`InputChording`、`InputArbitration`、`InputAxisConflict`、`InputStabilization`)、型名、member、動作は変更なし
- source / API互換は維持するが、runtime assembly名が変わるためbinary互換ではない。自作asmdefのReferences変更と、旧assemblyを参照するprecompiled DLLの再buildが必要
- 旧packageの公開済みtagとUPM識別子は、旧配布単位を継続利用する入口として維持。統合後packageとは同時導入しない
