# Input Chord Matcher

複数のrequired commandが利用側の明示simulation tickで許容span内に揃ったかを、Engine非依存で決定論的に判定します。

Input System、Unity時刻、frame回数、global stateを内部で読まず、callerが現在pressed中のpositive command idを厳密昇順snapshotとして渡します。

## 導入

Unity 6000.5.7f1以降で、Package Managerから次を追加します。

~~~text
https://github.com/mynameisGaku/UnityModules.git?path=/InputChordMatcher#input-chord-matcher-v1.0.0
~~~

## 基本

~~~csharp
using InputChording;

if (!InputChordMatcher.TryCreate(new[] { 1, 2, 3 }, 2, 100, out var chord, out var error)) return;

chord.TrySample(100, new[] { 1 }, out var guard, out error);
chord.TrySample(101, new[] { 1, 2 }, out var light, out error);
chord.TrySample(102, new[] { 1, 2, 3 }, out var match, out error);
// match.Triggered == true / match.PressSpanTicks == 2
~~~

## 契約

- required commandはpositive idの重複なし2〜16件。作成時に複製・昇順化する
- pressed snapshotはpositive idを厳密昇順に並べ、0〜64件を渡す
- 各required commandの非押下→押下edge tickを保持する
- incompleteからcompleteへ入った時だけ、最古と最新の押下edge差を判定する
- PressSpanTicksがMaximumSpanTicks以下ならTriggered、超過ならSpanExceeded
- completeを保持した同一・後続snapshotでは再発火しない
- completeからincompleteへ戻った時にRearmedを1回返す
- 現在tickより前または不正snapshotは状態を変えず明示errorを返す

## 境界

Input Chord Matcherはinput deviceを読みません。押下snapshotの生成、command id対応、効果実行、held中のrepeat、順序combo、入力bufferは利用側または各専用moduleの責務です。sample間に発生して消えたedgeは観測できません。

## Sample

Package ManagerからInput Chord Matcher Basicsをimportすると、Guard 1@100、Light 2@101、Heavy 3@102でspan 2の成立、Guard解放で再arm、Guard 1@106でspan 5超過を設定済みSceneから確認できます。

## License

MIT License。詳細はLICENSE.mdを参照してください。
