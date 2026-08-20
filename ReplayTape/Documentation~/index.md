# Replay Tape 1.0.0

## 目的

利用側が明示した操作列を、process・culture・CPU endiannessに依存しないversion付きbyte列として記録し、完全検証後に同じ順序で読み戻すこと。

## 境界

入力は非減少`ulong` tick、0以外の`uint` command id、opaque payloadです。stateは有界builder内のcanonical byte列、出力はimmutable valueまたはentry readerです。Unity API、現在時刻、乱数、scene、global stateを読みません。

```text
Input: nondecreasing tick + command id + copied payload
State: RTAP v1 canonical bytes with byte/entry limits
Output: ReplayTapeValue -> ReplayTapeReader -> ReplayTapeEntry
```

command実行とpayload解釈は利用側に残します。これによりgame固有schemaとportable transportの責務を分離します。

## 形式version 1

16-byte headerは`RTAP`、uint16 version、0 reserved、uint32 entry count、uint32 record byte countです。recordはuint64 tick、uint32 command id、uint32 payload length、payloadの順で、複数byte整数はすべてlittle-endianです。

tickは非減少だけを要求し、同tickでは追加順を意味のある順序として保持します。sortは行いません。command id 0は将来の形式拡張用に予約します。

## parse検証

`ReplayTapeValue.TryParse`は次をcopy前に検証します。

- magicとversion
- reserved 0
- 全byte数とdeclared record byte数
- entry count上限と実record数
- 各record headerとpayload範囲
- command id 0の不存在
- tickの非減少
- 最終record後のtrailing byte不存在

検証済みvalueだけがreaderを作れます。readerは独立cursorを持ち、payloadをentryへcopyして返します。

## failure時不変

command id 0、tick逆行、byte上限、entry上限はbuilderを変更せずに失敗します。`TryBuild`は非消費で、Build後の追加も既存valueへ影響しません。`Reset`は設定上限を保ったまま空headerへ戻します。

## 決定論の条件

同じformat version、tick、command id、payload bytes、追加順が必要です。float、string、構造体などのpayload canonical化は利用側schemaで固定してください。Tapeはplatform依存object layoutやserializer設定を推測しません。

## 検証

- emptyと複数entryのcanonical golden bytes
- 同tick追加順とtick逆行
- byte/entry上限ちょうどと超過時不変
- caller payload copy、Build非消費、Reset、Dispose
- magic/version/count/length/id/tick/trailing破損
- reader終端、Reset、payload copy
- public Runtime型5つとEngine非依存assembly
- import後sampleの実Buttonと960x600 / 640x360 geometry

## 非目標

Input capture、command dispatch、file I/O、compression、network protocol、rollback buffer、snapshot、state hash、global serviceはv1へ含めません。
