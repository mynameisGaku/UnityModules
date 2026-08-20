# Input Threshold Classifier

有限scalar sampleを、release・pressの2つのinclusive thresholdで安定したpressed状態とedgeへ分類するEngine非依存moduleです。

## 導入

Package Managerの`Add package from git URL...`へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputThresholdClassifier#input-threshold-classifier-v1.0.0
```

または`InputThresholdClassifier`を`Assets/Modules/`へcopyします。

## 最小例

```csharp
using InputThresholding;

if (!InputThresholdClassifier.TryCreate(0.25d, 0.75d, false, out var classifier, out var setupError)) return;

var pressed = classifier.Sample(0.75d);  // IsPressed true, Event Pressed
var held = classifier.Sample(0.50d);     // IsPressed true, Event None
var released = classifier.Sample(0.25d); // IsPressed false, Event Released
```

## 固定契約

- `ReleaseThreshold`と`PressThreshold`は有限で`0 <= release < press <= 1`
- released中はsampleがpress以上になった時だけ`Pressed`
- pressed中はsampleがrelease以下になった時だけ`Released`
- 2つの境界間では現在状態を保持し、edgeは`None`
- 両thresholdのexact equalityは状態変化側へinclusive
- 有限sampleは`[0,1]`へclampしてから判定
- NaNとInfinityは`NonFiniteInput`として拒否し、pressed状態を変えない
- defaultまたは不正構成は`InvalidConfiguration`

`InputThresholdClassifier`は構成とpressed状態を値として保持するmutable structです。`TryCreate`の`initialIsPressed`または`Reset`で状態を明示的に再構築でき、copy後は独立して進行します。時刻、乱数、Unity API、global stateを読みません。

## 境界

Input System等からtriggerやmagnitudeを読む処理は利用側adapterに置き、double sampleだけを渡します。signed axisの絶対値化、device calibration、sample時刻は呼出側の責務です。edgeをInput Command Buffer、Input Sequence Matcher、Replay Tape等へ渡す場合も利用側でcommandへ変換し、hard dependencyを作りません。

## 非目標

- Input System・Legacy Input Manager・binding
- repeat、tap、long press、multi-tap、chord、sequence
- smoothing、連続sample数debounce、curve、device calibration
- tick・実時間・timeout・自動sample
- command ID割当、effect callback、global service
- file I/O、network transport、Replay再生

## Sample

`Input Threshold Classifier Basics`ではrelease未到達、exact press、hysteresis保持、exact release、NaN拒否を実Buttonで確認できます。
