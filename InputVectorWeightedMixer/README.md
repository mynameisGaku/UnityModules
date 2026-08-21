# Input Vector Weighted Mixer

複数の有限2D analog sourceを、明示した非負weightの正規化加重平均へ変換するEngine非依存の純粋processorです。

## 導入

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputVectorWeightedMixer#input-vector-weighted-mixer-v1.0.0
```

または`InputVectorWeightedMixer`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using InputMixing;

var result = InputVectorWeightedMixer.Mix(new[]
{
    new InputVectorContribution(1d, 0d, 0.75d),
    new InputVectorContribution(0d, 1d, 0.25d)
});

// result = (0.75, 0.25), total weight = 1.0
```

## 固定契約

- 配列はnull不可、最大`32`件。empty配列は`(0,0)`の成功結果
- 各2D成分は有限の`[-1,1]`、各weightは有限の`[0,1]`
- weightが0のentryも検証し、不正値を黙って隠さない
- 正のweightがある場合、`sum(vector * weight) / sum(weight)`を返す
- 全weightが0ならneutral出力、active count 0
- 結果は総入力数、正weight数、weight合計、失敗index、丸めclamp有無を公開
- 最大weightで内部scaleして、subnormal weightでも相対比率を失わない
- 同じ順序の配列から同じ結果を返し、入力配列を変更しない

内部で時刻、frame、deltaTime、乱数、Unity API、global stateを読みません。

## 境界

利用側がPlayer、AI、camera assist等のsourceを明示順で配列へ格納します。選択式の競合解決にはInput Command Arbiter、1本の入力整形にはInput Radial Dead Zone・Input Vector Response Curve・Input Vector Slew Limiter・Input Vector Exponential Smootherを使えます。hard dependencyはありません。

## 非目標

- Input System・Legacy Input Manager・source自動発見
- priority選択、winner-takes-all、入力buffer
- 時間補間、dead zone、curve、slew、low-pass
- negative weight、加算mix、magnitude正規化
- callback、状態保持、global service、I/O

## Sample

`Input Vector Weighted Mixer Basics`ではequal mix、player-heavy mix、zero weight、empty成功、weight拒否を実Buttonで確認できます。
