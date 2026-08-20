# Canonical Payload 1.0.0

## 問題

Replayや保存のcommand payloadを利用側ごとに手書きすると、endianness、UTF-8、length検証、copy ownershipがばらつきます。一方、object serializerへ任せるとreflection順、型metadata、設定差がschemaへ隠れます。

Canonical Payloadは明示したprimitive write列だけをportable bytesへ変換します。

```text
Input: caller schema order + explicit primitive values
State: bounded writer bytes or reader cursor
Output: immutable payload or decoded values
```

現在時刻、乱数、Unity API、global state、reflectionを読みません。

## wire表現

payload自体にheader、version、field tagはありません。次のprimitive表現だけを固定します。

- boolean: `00` / `01`
- 32-bit整数: 4-byte little-endian
- 64-bit整数: 8-byte little-endian
- single/double: IEEE 754 bit列をlittle-endian
- string: uint32 UTF-8 byte長 + 厳格UTF-8 bytes
- bytes: uint32 byte長 + bytes

`TryWriteSingle`と`TryWriteDouble`は有限値へ丸めず、NaN payloadやsigned zeroを含むbit列を保持します。stringは不正surrogateを拒否し、Unicode normalizationを推測しません。

## schema version

型tagやversionを自動追加しない設計です。同じbytesを得るには、同じwrite method、値、順序が必要です。Replay Tapeで使う場合はcommand idごとにschemaを固定し、互換性を変える場合は新しいcommand idまたは利用側versionを割り当てます。

## transactional failure

writerはfield全体が上限へ収まることを確認してからbyte数を進めます。readerはfield全体、boolean値、UTF-8を検証してから位置を進めます。したがって失敗後に別schemaを試す場合も、失敗したfieldの開始位置を保持します。

## bounds

既定64 KiB、最大16 MiBです。`CanonicalPayloadValue.TryCreate`もcaller指定上限の範囲でcopyします。大量blob、streaming、圧縮をこのmoduleへ持ち込みません。

## ownership

入力bytes、immutable value、`ToByteArray`出力、reader位置は互いに独立します。writerの追加・Reset・Disposeが既存valueへ影響しません。

## 非目標

object serializer、reflection、field registry、schema migration、collection sort、file I/O、network protocol、compression、encryption、hash、Unity serviceは含めません。
