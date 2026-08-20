# Input Sequence Matcher

正のcommand id列を、利用側が渡す明示simulation tickで決定論的に照合するEngine非依存state machineです。

入力deviceやUnity時刻を内部で読まず、pattern、最大tick間隔、入力commandをcallerが明示するため、pause、fixed simulation、Replayでも同じmatch・timeout・restartを再現できます。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputSequenceMatcher#input-sequence-matcher-v1.0.0
```

または`InputSequenceMatcher`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
if (!InputSequenceMatcher.TryCreate(new[] { 1, 1, 2 }, 2, 100, out var matcher, out var error)) return;

matcher.TryPush(100, 1, out var first, out error);  // Light 1 / 3
matcher.TryPush(101, 1, out var second, out error); // Light 2 / 3
matcher.TryPush(102, 2, out var result, out error); // Heavyでmatch

if (result.Matched)
{
    // Light・Light・Heavyが最大間隔内で一致
}
```

## 固定契約

- patternは`1..64`個の正の`int` command idで構成し、作成時に複製する
- tickは各`TryPush`で明示し、現在tickより前へ戻せない
- 隣接する一致commandのtick差が`MaximumGapTicks`以下なら進捗を維持する
- 間隔超過は現在入力を処理する前に進捗を破棄し、`TimedOut`で報告する
- 不一致commandがpattern先頭なら進捗1からrestartし、それ以外は0へ戻す
- pattern全体の一致を`Matched`で1回報告し、次のpattern照合へ進捗0から戻る
- 不正command idと逆行tickは状態を変えず明示errorを返す
- `Snapshot`は状態を進めず、`Reset`は進捗とtimelineを明示的に初期化する

現在時刻、Unity frame、入力device、乱数、Unity API、global stateを読みません。

## 境界の置き方

Input System等のadapterがbutton edgeを正のcommand idへ変換し、simulation側が同じtimelineのtickとともに`TryPush`します。Input Stabilizerは連続値の前処理、Input Command Bufferは早押しcommandの短期保持を担当し、このmoduleは受け取った離散command列の順序と間隔だけを判定します。

## 非目標

- Input SystemやLegacy Input Managerからの読取
- button edge検出、held/repeat、analog量子化、stabilization
- commandの保持、優先度解決、秒・Unity frameによる期限管理
- 複数pattern同時照合、分岐、wildcard、chord、完全なprefix fallback
- event通知、global service、singleton、file I/O、network transport

## Sample

Package Managerから`Input Sequence Matcher Basics`をimportすると、Light@100、Light@101、Heavy@102のmatchと、Light@103からLate Light@106への間隔超過timeout/restartを設定済みSceneで確認できます。
