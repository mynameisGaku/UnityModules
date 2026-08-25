# 実行速度計測（Perf Meter）

## 30秒で分かる説明

Perf Meterは、frame time、fps、簡易メモリを有界リングバッファへサンプリングし、平均、最小、最大、中央値、標準偏差、percentileとして数値化するRuntimeモジュールです。「なんだか重い」を実機とEditorの両方で同じ数値で語れるようにします。

計測は`FrameTimeSampler`という純粋classが担い、寿命が明確な1つのownerがnewして毎frame `AddFrame`を呼びます。自動起動するsingletonやEditor menuはありません。Sceneに置くだけで使える表示用の`PerfMeterComponent`も同梱しますが、singletonではなく、sceneに置いた者だけが動きます。

## できること

- 指定容量（1〜65536 frame）の有界リングバッファへframe timeを蓄え、容量超過時は最古のsampleを上書きする。
- 平均、最小、最大、中央値、母標準偏差、平均dt逆数fps、任意percentile（線形補間）を現在windowに対して決定論的に返す。
- spike判定の閾値秒を設定し、前回読出し以降の超過数をラッチ方式で取り出す。
- 全統計を1つの取得タイミングで揃えた`FrameTimeSnapshot`を受け取れる。
- managed heapサイズと、Profiler有効時のみProfiler reported heapサイズを取得する。
- `AddFrame`経路はGC確保なし（constructorでの初回capacity確保を除く）。

## 使わない方がよい場合

GPU時間、draw call、CPUプロファイラmarker別の内訳、電力や温度の推定は扱いません。重い原因の詳細分析はUnity Profilerの役割です。メモリ計測も簡易であり、nativeヒープ全体やallocation追跡は含みません。長期蓄積、file出力、閾値超過の通知も1.0.0には含みません。

## 3分で試す

1. Package Managerの`Add package from git URL...`へ次を指定します。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/PerfMeter#perf-meter-v1.0.0
   ```

2. Package ManagerのSamplesから`Perf Meter Basics`をimportします。
3. 空Sceneで空GameObjectを作り、`PerfMeterBasicsController`を追加してPlayします。同GOへ`PerfMeterComponent`が自動で追加されます。
4. `Heavy Frame (120ms)`buttonを押すとmain threadが約120ms止まり、人工的なスパイクを発生させてHUDで確認できます。直後のframeはoverlayが赤になり、lastとmaxが約120msへ跳ね上がります。
5. `Reset Stats`buttonで統計とspike計数が0へ戻ることを確認します。

利用側にasmdefがある場合は`PerfMeter.Runtime`を参照します。フォルダーを直接管理する場合だけ、`PerfMeter/`を`Assets/Modules/PerfMeter/`へ配置してください。

| package dependency | version | 用途 |
| --- | --- | --- |
| なし | - | runtimeはUnity組込みmodule（Time、Profiler、GUI）のみを使用する |

## 最小コード

```csharp
using PerfMeter;
using UnityEngine;

public sealed class FrameStatsOwner : MonoBehaviour
{
    private FrameTimeSampler _sampler;

    private void Awake()
    {
        _sampler = new FrameTimeSampler(600);
        _sampler.SetSpikeThreshold(0.033d, out _);
    }

    private void Update()
    {
        _sampler.AddFrame(Time.deltaTime, out _);
        var snapshot = _sampler.CreateSnapshot();
        Debug.Log($"fps={snapshot.AverageFps:F1} max={snapshot.Maximum * 1000d:F1}ms spikes={_sampler.TotalSpikes}");
    }

    private void OnDestroy()
    {
        _sampler.Dispose();
    }
}
```

ownerが1つのsamplerを持ち、いつ計上し、いつ捨てるかを自分で決めます。`AddFrame`や`SetSpikeThreshold`が失敗したときの`error`は、必要になった利用者が確認します。

## 実行するとどうなるか

- 画面左上に白字のoverlayが出ます。fps(avg)、last/min/max、median/stddev、spikes、memoryを行ごとに表示します。
- 直前に計上したframeがspike閾値を超えている間だけ、overlayが赤色になります。
- `Heavy Frame (120ms)`押下直後の1frameはlastとmaxが約120msになり、spikes合計が増えます。
- Consoleへは何も出力しません。警告、自動修復、設定変更も行いません。
- EditMode testでは、既知dt列に対する統計期待値が許容誤差1e-9以内で一致することを検証します。

## よくある問題

- **overlayが出ない**: `Show Overlay`のcheck、および`PerfMeterComponent`を付けたGameObjectがactiveか確認してください。singletonではないため、置いたsceneでのみ動きます。
- **memory欄にprofiler heapが出ない**: EditorまたはDevelopment BuildでProfilerが有効なときだけ取得できます。無効な環境ではprofiler欄自体を省略します。
- **数値がProfilerの統計と一致しない**: 本moduleは`Time.deltaTime`ベースの応用統計です。Profiler内部の計測定義とは一致しません。
- **Player Optionsとの違い**: Player Optionsはtarget frame rateなどの保存と適用を行います。Perf Meterは実測値の計測のみで、設定の変更は一切行いません。
- **capacityFramesを0にした**: constructorは例外を送出しますが、`PerfMeterComponent`はInspector値を許可範囲1〜65536へ丸めて構築します。

## 詳しい契約

### 公開API

- `FrameTimeSampler` — 有界リングバッファと統計の中核。`AddFrame`、`TryGetPercentile`、`SpikesSinceLastCheck`、`CreateSnapshot`、`Reset`、`Dispose`を提供します。
- `FrameTimeSnapshot` — Last、Average、Minimum、Maximum、Median、StandardDeviation、SampleCount、AverageFpsを持つreadonly structです。全field比較のEquals / GetHashCodeを実装します。
- `MemoryProbe` / `MemorySnapshot` — 簡易メモリ取得のstatic入口と結果です。
- `PerfMeterError` — None、NonFiniteValue、NegativeValue、InvalidCapacity、InvalidPercentile、InvalidThreshold、SamplerDisposedです。
- `PerfMeterComponent` — 毎frame `Time.deltaTime`を計上しoverlayへ出す任意利用のMonoBehaviourです。`ResetStats`と`SetSpikeThreshold`のpublic wrapperを持ちます。

### 失敗条件

| 操作 | 条件 | 結果 |
| --- | --- | --- |
| constructor | capacityFramesが1未満または65536超 | `ArgumentOutOfRangeException` |
| `AddFrame` | NaN / Infinity | false + NonFiniteValue、状態不変 |
| `AddFrame` | 負値 | false + NegativeValue、状態不変 |
| `AddFrame` | 0 | 成功として計上する（同一frame二重計上などの用途） |
| `SetSpikeThreshold` | NaN / Infinity | false + NonFiniteValue |
| `SetSpikeThreshold` | 負値 | false + InvalidThreshold |
| `TryGetPercentile` | pが(0,100]外またはNaN / Infinity | false + InvalidPercentile |
| Dispose後の操作 | AddFrame / SetSpikeThreshold / TryGetPercentile | false + SamplerDisposed |

### 統計の定義

- Average: Σdt ÷ n（oldest→newestの順に加算）。
- StandardDeviation: sqrt(Σ(x−μ)² ÷ n)。母標準偏差です。
- Median: 昇順中央値。偶数件は中央2件の平均。
- Percentile: rank=(n−1)×p÷100の位置を隣接2件で線形補間します。p=100はMaximum、p=50はMedianと一致します。
- AverageFps: n ÷ Σdt。windowが空またはΣdt=0のときは0。
- sampleが0件の場合、上記はすべて正準値0を返します。読む前に`SampleCount`を確認してください。

### spike計数の共有

`SpikesSinceLastCheck()`は読み出しでカウントをリセットします。`PerfMeterComponent`のoverlayも毎frameこのAPIでspike状態を取得するため、利用者が同じsamplerの`SpikesSinceLastCheck()`を直接呼ぶとoverlay表示と計数を取り合います。累積が必要な場合は`TotalSpikes`を読んでください。

### GC契約

`AddFrame`経路はGC確保なしです（constructorでの初回capacity確保を除く）。統計property、`CreateSnapshot`、`TryGetPercentile`もconstructor確保の内部scratch配列のみで動作し、新規確保しません。`PerfMeterComponent.OnGUI`の文字列整形だけは確保を伴います（表示専用経路です）。EditMode testでこの契約を検証しています。

### メモリ計測の限界（Boundaries）

- `ManagedBytes`は`GC.GetTotalMemory(false)`の瞬間値です。強制collectionを行わず、monoとIL2CPPで意味するheapが異なります。native部分は含みません。
- `ProfilerReportedBytes`は`UnityEngine.Profiler.enabled`がtrueの場合のみ取得でき、それ以外は`-1`です。managed値との差は算出方法の違いであり、どちらも総メモリ使用量ではありません。
- 厳密なメモリbudget管理にはUnity Memory Profilerを使用してください。

### 非対象

GPU時間、draw call、Profiler marker別内訳、長期ログ蓄積と出力、alert通知、remote reporting、singleton、static event、自動GameObject、自動起動。

### テスト範囲

EditMode: 既知dt列（60fps相当×10＋spike 0.1s）に対するAverage / Min / Max / Median / StdDev / Percentile95の期待値一致（許容誤差1e-9）、容量超過時の最古上書き、percentile境界、非有限・負値エラー、spikeラッチ、snapshot等価比較、Dispose契約、AddFrame経路のGC確保ゼロ。Samples PlayMode: 人工spikeの計上とreset。シーン(.unity)は同梱しません。

本packageは[MIT License](LICENSE.md)です。外部依存は[Third-Party Notices.txt](Third-Party%20Notices.txt)を参照してください。
