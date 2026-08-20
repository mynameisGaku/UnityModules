# Replay Tape

利用側が明示した`tick + command id + opaque payload`を追加順のcanonical byte列へ記録し、保存・転送後も完全検証して同じ順序で読み戻せるEngine非依存moduleです。

対応: **Unity 6000.5.7f1 以降**

## 導入

Package ManagerのGit URLへ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/ReplayTape#replay-tape-v1.0.0
```

Runtime APIに追加package依存はありません。組込UI Toolkit moduleは同梱サンプルの表示だけに使います。

## 解決する問題

時刻と乱数状態を再現できても、どの操作をどの順序で適用したかが暗黙のcallbackやInput System内部へ残ると、失敗時の操作列を別processで再実行できません。Replay Tapeは操作列の記録と検証だけを担当します。

```text
ordered (tick + command id + opaque payload)
                         ↓
                ReplayTapeBuilder
                         ↓
          versioned canonical ReplayTapeValue
                         ↓
                ReplayTapeReader
```

## 最小例

```csharp
using System;
using ReplayTape;

using var builder = new ReplayTapeBuilder();
Span<byte> movePayload = stackalloc byte[] { 1, 0, 0, 0 };

if (!builder.TryAppend(120, 1, movePayload, out var appendError))
{
    throw new InvalidOperationException(appendError.ToString());
}

if (!builder.TryBuild(out var tape, out var buildError))
{
    throw new InvalidOperationException(buildError.ToString());
}

var bytesForCallerOwnedStorage = tape.ToByteArray();
if (!ReplayTapeValue.TryParse(bytesForCallerOwnedStorage, out var parsed, out var parseError))
{
    throw new InvalidOperationException(parseError.ToString());
}

parsed.TryCreateReader(out var reader, out _);
while (reader.TryRead(out var entry, out var readError))
{
    ApplyGameCommand(entry.Tick, entry.CommandId, entry.ToPayloadArray());
}
```

`ApplyGameCommand`とpayload schemaはゲーム側の責務です。Tapeはpayloadを解釈しません。

## canonical形式 version 1

headerは16 bytesです。

```text
magic "RTAP":4
version:uint16 little-endian
reserved:uint16 = 0
entry count:uint32 little-endian
record bytes:uint32 little-endian
```

各recordは次の順です。

```text
tick:uint64 little-endian
command id:uint32 little-endian
payload length:uint32 little-endian
payload:opaque bytes
```

- tickは非減少。同じtickのcommandは`TryAppend`呼出順を保持
- command id 0は予約し、入力時とparse時に拒否
- parseはmagic、version、reserved、総byte数、entry数、payload長、tick順序、trailing byteを完全検証
- builderとvalueはcallerの入力配列をcopyし、後変更の影響を受けない

## 上限と失敗時不変

既定上限は1 MiB / 65,536 entries、指定可能な上限は16 MiB / 1,000,000 entriesです。容量超過、command id 0、tick逆行は、そのrecordを1 byteも追加せず失敗します。`TryBuild`はbuilderを消費せず、同じ状態から繰り返し同じbyte列を得られます。

## 他moduleとの組合せ

- Simulation Clockが出した固定step番号をtickとして記録
- Deterministic Randomの保存stateをReplay開始点として保持
- State FingerprintでReplay後stateの一致を検証
- Save Systemまたは利用側I/Oで`ReplayTapeValue.ToByteArray()`を保存

これらへのhard dependencyはありません。

## 含めないもの

- Input SystemやUI callbackの自動hook
- commandの実行callback、型registry、reflection、object serializer
- file I/O、圧縮、暗号化、network送信
- rollback state、snapshot、乱数、時計、state fingerprint
- tickの自動sort、同tick commandの並べ替え
- global singleton、Unity lifecycle owner

## サンプル

Package Managerから`Replay Tape Basics`をimportしてください。Record Move、Record Damage、Build Tape、Replay Tape、Resetの実Buttonと、960x600 / 640x360 responsive表示を確認できます。

## License

配布形態に応じて`LICENSE.md`を確認してください。第三者コードは含みません。
