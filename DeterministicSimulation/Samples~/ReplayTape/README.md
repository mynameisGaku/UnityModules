# Replay Tape Basics

Sceneを開くと、tick付きcommandをmodelへ適用しながら記録し、同じtapeからmodelを再現できます。

- `Record Move +1`: 次tickへMove commandを追加し、Xへ即時反映
- `Record Damage -10`: 次tickへDamage commandを追加し、Healthへ即時反映
- `Build Tape`: 現在のbuilderから独立したimmutable tapeを作成
- `Replay Tape`: 初期modelへ戻し、tapeを先頭から適用して記録時との一致を確認
- `Reset`: model、tick、builder、操作数を初期化

960x600では5 Buttonを1列、640x360では3+2列で表示します。
