# Canonical Payload

利用側が明示したschema順のprimitive値を、platformに依存しない有界byte列へ変換し、同じ順序で読み戻すEngine非依存moduleです。

Replay Tapeのcommand payload、保存前の小さなrecord、通信前の明示schemaなどで、little-endian変換・厳格UTF-8・length prefixを毎回実装する重複を減らします。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/CanonicalPayload#canonical-payload-v1.0.0
```

または`CanonicalPayload`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using var writer = new CanonicalPayloadWriter(256);
writer.TryWriteInt32(-10, out _);
writer.TryWriteSingle(1.25f, out _);
writer.TryWriteString("移動🚀", out _);
writer.TryBuild(out var payload, out _);

payload.TryCreateReader(out var reader, out _);
reader.TryReadInt32(out var damage, out _);
reader.TryReadSingle(out var speed, out _);
reader.TryReadString(out var label, out _);
var complete = reader.IsAtEnd;
```

writeとreadの順序が利用側schemaです。型tagやfield名を自動追加しないため、command idまたは利用側versionと一緒にschemaを固定してください。

## canonical表現

| 値 | byte表現 |
|---|---|
| `bool` | `00`または`01` |
| `int32` / `uint32` | 4-byte little-endian |
| `int64` / `uint64` | 8-byte little-endian |
| `single` | IEEE 754 bit表現を4-byte little-endian |
| `double` | IEEE 754 bit表現を8-byte little-endian |
| `string` | UTF-8 byte長`uint32` + 厳格UTF-8 bytes |
| `bytes` | byte長`uint32` + copied bytes |

浮動小数はNaNを含めbit表現をそのまま保持します。stringは不正surrogateを拒否し、Unicode normalizationは行いません。空stringと空bytesは、長さ0の有効fieldです。

## ownershipと失敗

- writerは入力bytesを追加時にcopy
- `TryBuild`はwriterを消費せず、immutable valueを独立copy
- `ToByteArray`はcaller所有copy
- readerごとに独立した位置を保持
- 容量超過、不正UTF-8、truncated値、不正boolean、不正lengthは失敗したfieldの前で位置とwriter byte数を保持
- `Reset`は設定上限を保ち、`Dispose`は複数回安全

既定上限は64 KiB、指定可能な最大上限は16 MiBです。値全体にheaderやversionは付けません。versionはpayloadを使うcommandや保存schema側で明示してください。

## Replay Tapeとの組合せ

```csharp
using var payloadWriter = new CanonicalPayloadWriter(64);
payloadWriter.TryWriteInt32(-10, out _);
payloadWriter.TryBuild(out var payload, out _);

using var tape = new ReplayTapeBuilder();
tape.TryAppend(tick, DamageCommandId, payload.ToByteArray(), out _);
```

Canonical PayloadはReplay Tapeへhard dependencyを持ちません。Replay Tapeもpayload内容を解釈しないため、両方を個別に導入・検証できます。

## 非目標

- object serializer、reflection、field名、型registry
- schema version、migration、自動互換変換
- collection sort、Dictionary列挙順の固定
- file I/O、network transport、compression、encryption、hash
- Unity object、global service、singleton

ゲーム固有schema、fieldの意味、version更新方針は利用側の責務です。

## Sample

Package Managerから`Canonical Payload Basics`をimportすると、設定済みSceneでEncode・Decode・破損拒否・再Buildを確認できます。
