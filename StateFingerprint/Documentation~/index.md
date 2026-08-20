# State Fingerprint 1.0.0

## 目的

利用側が明示したfield列から、process・culture・CPU endiannessへ依存しない一致確認値を作ること。

## 境界

入力は型、`uint` field id、値、操作順です。stateはbuilder内のversion付きcanonical byte列、出力は32-byte SHA-256 fingerprintです。
Unity API、現在時刻、乱数、scene、global stateを読みません。

```text
Input: ordered typed fields
State: SFP v1 canonical bytes, bounded by caller-selected capacity
Output: StateFingerprintValue or explicit StateFingerprintError
```

## 形式version 1

headerは`53 46 50 01`です。各recordは1-byte type tag、4-byte field id、4-byte payload長、payloadの順です。複数byte数値はlittle-endian、最終digestの文字列表現はSHA-256順の小文字64桁hexです。

型tagを含むため`Int32(42)`と`UInt32(42)`は一致しません。field idと操作順もdigestへ含まれます。

## 文字列と浮動小数

stringは厳格なUTF-8でencodeし、不正surrogateを拒否します。Unicode normalizationは利用側のschema判断なので実施しません。
single/doubleはraw IEEE 754 bitsを使い、数値的に等しい`+0`と`-0`も別stateです。

## 失敗時不変

null string/bytes、不正Unicode、容量超過は、そのrecordを1 byteも追加せずに失敗します。`TryBuild`は位置を消費せず、同じbuilderから繰り返し同じ値を得られます。

## 決定論の条件

同じformat version、field id、型、値、操作順が必要です。順序を持たないcollectionを含める場合、利用側がstable keyでsortしてください。reflection順やDictionary列挙順をschemaにしないでください。

## 検証

- headerだけと全型列のSHA-256 golden vector
- field id・型・順序・null/empty・raw float bitsの差
- UTF-8 non-BMPと不正surrogate
- 上限ちょうど、超過時不変、bytes copy
- Reset、Dispose、Build非消費、hex/byte roundtrip
- import後sampleの実Buttonと960x600 / 640x360 geometry

## 非目標

object serializer、schema migration、file I/O、network protocol、cryptographic signature、collection sort、global serviceはv1へ含めません。

