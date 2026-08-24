# Deterministic Simulation 1.0.0

## Requirements

同じ設定・同じ初期状態・同じ入力列から、いつ実行しても同じ結果と同じ最終状態が得られること。
その結果を保存し、あとから再生・比較・検証できること。

## Module Boundary

この package はシミュレーションの「再現性を成立させる部品」だけを持ちます。
ゲームロジック、物理、描画、network同期、file I/O は持ちません。

| 区分 | 名前空間 | 役割 |
|---|---|---|
| 時間 | `SimulationClock` | 明示された整数経過時間を連続固定step範囲と補間端数へ変換する。 |
| 乱数 | `DeterministicRandom` | 保存・復元できるxoshiro256\*\*の乱数列を返す。 |
| 数値 | `FixedPoint` | 検査付きQ16.16固定小数点演算を行う。 |
| 符号化 | `CanonicalPayload` | 呼び出し側が決めた順序で、正規化されたバイト列を書き読みする。 |
| 記録 | `ReplayTape` | tick・command ID・payload を有界のテープへ記録し読み戻す。 |
| 検証 | `StateFingerprint` | 明示した順序付きフィールドから版付きSHA-256を作る。 |
| 識別子 | `GenerationalHandles` | 世代番号付きの識別子を配り、解放済みhandleを弾く。 |

## Dependency Flow

```text
明示された整数入力（elapsed ticks / seed / command）
                 ↓
FixedStepClock → DeterministicRandomStream → ゲームロジック（利用側が所有）
                 ↓                                   ↓
          ReplayTapeBuilder              StateFingerprintBuilder
                 ↓                                   ↓
        ReplayTapeValue（bytes）              32byteの照合値
```

Runtime asmdef は `noEngineReferences: true` です。Unity API は同梱サンプルの adapter 層だけが使います。

## Determinism Contract

- 各型は Unity の現在時刻、`Time.timeScale`、`FixedUpdate`、Scene、GameObject を参照しません。
- 浮動小数点の元値ではなく、実際に渡した整数値を記録・再生の入力契約とします。
- `DeterministicRandomState`、`FixedStepClockState`、`ReplayTapeValue`、`StateFingerprintValue` は値として保存・比較・復元できます。
- 失敗した操作は状態を変更しません。エラーは例外ではなく `*Error` 列挙で返します。

## Packaging

旧7package（`com.studiogaku.simulation-clock` / `deterministic-random` / `state-fingerprint` /
`replay-tape` / `canonical-payload` / `fixed-point` / `generational-handle`）を1配布単位へ統合したものです。
名前空間、型、member、動作は統合前と同一でsource / API互換です。runtime assembly名は`DeterministicSimulation.Runtime`へ変わるためbinary互換ではなく、自作asmdefのReferences変更と、旧assemblyを参照するprecompiled DLLの再buildが必要です。公開済みtagは旧配布単位を継続利用する入口として残りますが、統合後packageとは同時導入できません。

## Avoided Overengineering

step実行callback、event bus、DI container、rollback buffer、network transport、save file format は追加していません。
これらは利用側または別 module の責務です。
