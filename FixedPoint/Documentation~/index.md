# Fixed Point 1.0.0

## 問題

同じ入力列を再生しても、浮動小数演算をsimulation stateへ直接使うと、platform、compiler、演算順の差が小さな不一致として蓄積する場合があります。一方、任意precisionや物理systemまで同時に作ると責務が広がります。

Fixed Pointは小数値をsigned Q16.16のraw整数として保持し、基本演算だけを決定論的に実行します。

```text
Input: Q16.16 raw values or integer ratio
State: none
Output: Fixed32Result(value or explicit error)
```

現在時刻、乱数、Unity API、global stateを読みません。

## Q16.16表現

`Fixed32.RawValue`の上位16bitが符号付き整数部、下位16bitが小数部です。`Scale`は65536で、`One.RawValue`も65536です。表現範囲は`-32768`から`32767.9999847412109375`です。

`FromInt32`と`FromRatio`は範囲を検証します。既に検証済みのwire値や保存値を戻す場合だけ`FromRaw`を使います。

## 演算と丸め

- Add/Subtractは64-bit中間値でoverflowを確認
- Multiplyは`leftRaw * rightRaw / 65536`
- Divideは`leftRaw * 65536 / rightRaw`
- 整数除算の余りは正負とも0方向へ丸める
- overflowとdivision-by-zeroは`Fixed32Error`で返し、失敗値は`Zero`

publicな算術operatorは提供しません。結果の確認を省略してwraparoundする経路を作らず、利用側が`Succeeded`または`Error`を明示確認します。

## 整数化

`TruncateToInt32`、`FloorToInt32`、`CeilingToInt32`は丸め方向をmethod名で明示します。`ToDouble`は表示またはengine adapter向けです。simulationの次状態を決める演算はFixed32側で完結させてください。

## 他moduleとの組合せ

Canonical Payloadへ`RawValue`をint32として書けばportable bytesへ保存できます。Replay Tapeのcommand payloadやState Fingerprintのfieldにも同じraw値を渡せます。Fixed Pointはこれらへhard dependencyを持ちません。

## 非目標

vector、matrix、quaternion、三角関数、平方根、物理、任意precision、float/double入力変換、parser、unit system、file I/O、network同期、Unity serviceは含めません。
