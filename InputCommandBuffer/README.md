# Input Command Buffer

操作可能になる少し前に押された離散commandを、明示simulation tickの短い有効期間だけ保持してFIFOで消費するEngine非依存bufferです。

入力deviceやUnity時刻を内部で読まず、callerが現在tickを進めるため、pause、fixed simulation、Replayでも同じ記録・期限切れ・消費順を再現できます。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputCommandBuffer#input-command-buffer-v1.0.0
```

または`InputCommandBuffer`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
if (!InputCommandBuffer.TryCreate(3, 2, 100, out var buffer, out var error)) return;

buffer.TryRecord(1, out var jump, out error);       // tick 100でJumpを記録
buffer.TryAdvanceTo(101, out var expired, out error); // retention内なので保持

if (buffer.TryConsume(1, out var consumed, out error))
{
    // tick 100で押されたJumpをtick 101で消費
}
```

## 固定契約

- 容量は`1..1024`の固定長で、期限内commandを黙って上書きしない
- command idは正の`int`を利用側が定義する
- tickは`TryAdvanceTo`で明示し、現在tickより前へ戻せない
- commandは記録tickから`RetentionTicks`後まで両端を含めて有効
- 同じcommand idを複数回記録でき、最も古い一致から消費する
- 別commandの消費は他のentryの順序を変えない
- `Clear`はtickを維持し、`Reset`は新しいtickと順序番号へ初期化する
- 容量不足、逆行tick、不正id、未発見をerrorで区別する

現在時刻、Unity frame、乱数、Unity API、global stateを読みません。

## 境界の置き方

Input System等のadapterがbutton edgeを正のcommand idへ変換し、simulation側が明示tickで記録・前進・消費します。Input QuantizerやInput Stabilizerは連続値を離散commandへ変える前段であり、このmoduleは押下eventの短期保持だけを担当します。

## 非目標

- Input SystemやLegacy Input Managerからの読取
- button edge検出、held/repeat、analog量子化、stabilization
- 秒・Unity frame・Coroutineによる期限管理
- combo、sequence matcher、priority arbitration
- 自動上書き、可変容量、永続化、Replay記録
- global service、singleton、event通知

## Sample

Package Managerから`Input Command Buffer Basics`をimportすると、tick 100のJump記録、tick 101でのFIFO消費、tick 101のDashがtick 104で期限切れになる流れを設定済みSceneで確認できます。
