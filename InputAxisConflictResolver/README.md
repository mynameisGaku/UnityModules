# Input Axis Conflict Resolver

negativeとpositiveの相反するdigital入力を、利用側の明示simulation tickと選択policyだけから決定論的に解決します。

## 導入

~~~text
https://github.com/mynameisGaku/UnityModules.git?path=/InputAxisConflictResolver#input-axis-conflict-resolver-v1.0.0
~~~

## 基本

~~~csharp
using InputAxisConflict;

InputAxisConflictResolver.TryCreate(InputAxisConflictPolicy.LastPressedWins, 100, out var axis, out var error);
axis.TrySample(100, true, false, out var left, out error);  // -1
axis.TrySample(101, true, true, out var right, out error);  // +1
~~~

## 契約

- single negativeは-1、single positiveは1、両方releasedは0
- 競合policyはNeutral、NegativeWins、PositiveWins、LastPressedWins
- LastPressedWinsは新しい押下edge側を選び、同一tickの両edgeはneutral
- winner解放時は保持中の反対側へ即時fallback
- 同一押下snapshotではedgeとResolutionChangedを再発行しない
- 逆行tickは状態を変えずTickMovedBackwardを返す

## 境界

本moduleはInput System、Unity時刻、analog値、key bindingを読みません。bool生成、axis量、移動効果、network同期は利用側の責務です。

## Sample

LastPressedWinsでnegative→positive競合→positive解放→全解放→同一tick両押下tieを確認できます。

## License

MIT License。詳細はLICENSE.mdを参照してください。
