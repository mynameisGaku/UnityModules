# State Fingerprint

明示した型付きfield操作列をcanonical byte列へ変換し、同じ入力から同じSHA-256 fingerprintを作るEngine非依存moduleです。
reflectionやobject serializerを使わず、何を比較対象へ含めるかを利用側のschemaとして明示します。

対応: **Unity 6000.5.7f1 以降**

## 導入

Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/StateFingerprint#state-fingerprint-v1.0.0
```

Runtime APIに追加package依存はありません。組込UI Toolkit moduleは同梱サンプルの表示だけに使います。

## 解決する問題

再現実行で時刻と乱数位置を戻せても、最終stateが一致したかを`GetHashCode`やculture依存の文字列連結で比較すると、processやplatformを跨いだ証拠になりません。
State Fingerprintは次の変換だけを担当します。

```text
ordered (type + field id + value) operations
                     ↓
          StateFingerprintBuilder
                     ↓
versioned canonical bytes + SHA-256 fingerprint
```

## 最小例

```csharp
using System;
using StateFingerprint;

using var builder = new StateFingerprintBuilder();
Require(builder.WriteUInt64(1, simulationStep));
Require(builder.WriteInt32(2, playerHealth));
Require(builder.WriteDouble(3, playerPositionX));
Require(builder.WriteString(4, playerName));

if (!builder.TryBuild(out var fingerprint, out var error))
{
    throw new InvalidOperationException(error.ToString());
}

var portableHex = fingerprint.ToString();

static void Require(StateFingerprintError error)
{
    if (error != StateFingerprintError.None) throw new InvalidOperationException(error.ToString());
}
```

## canonical形式

先頭4 bytesは`SFP`とformat version 1です。各fieldは操作順に次を追加します。

```text
type tag:1 + field id:uint32 little-endian + payload length:uint32 little-endian + payload
```

- signed/unsigned整数は固定幅little-endian
- `float`/`double`はraw IEEE 754 bit列。`+0`と`-0`、NaN payloadを区別
- stringはBOMなしUTF-8。不正surrogateは`InvalidInput`
- nullは`WriteNull`で明示し、空string・空bytesと区別
- field id、型、値、操作順のどれかが変わると別のfingerprint

## 重要な境界

collectionの順序は自動で変更しません。DictionaryやHashSetを含める場合は、利用側が安定keyで並べ、同じ順序でwriteしてください。
Unicode normalizationも行いません。見た目が同じでもcode point列が違えば別stateとして扱います。

既定上限はcanonical bytes 1 MiB、指定可能な上限は16 MiBです。上限超過や不正入力はbuilderを変更せず結果で返します。

## Simulation Clock / Deterministic Randomとの組合せ

固定step番号、Simulation Clock state、Deterministic Random state、ゲーム固有stateを同じschema順でwriteすると、Replay前後の一致を1つのportable fingerprintで確認できます。これらのmoduleへのhard dependencyはありません。

## 含めないもの

- reflectionによるobject graph走査
- JSON・binary object serialization
- collectionの自動sort
- file保存、network送信、server照合
- 暗号署名、秘密鍵、改ざん防止の保証
- global singleton、Unity lifecycle owner

SHA-256を使いますが、このmoduleは署名や認証の仕組みではありません。

## サンプル

Package Managerから`State Fingerprint Basics`をimportしてください。Build、Damage、Move、Replay Snapshot、Resetの実Buttonと、960x600 / 640x360 responsive表示を確認できます。

## License

配布形態に応じて`LICENSE.md`を確認してください。第三者コードは含みません。

