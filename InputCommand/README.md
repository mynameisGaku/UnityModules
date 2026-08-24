# 入力コマンド判定（Input Command）

## 30秒で分かる説明

「地面に着く直前に押したJumpが消える」「↓↘→+パンチが繋がらない」「LB+RBの同時押しが片方しか拾えない」「JumpとDashが同じframeで両方出る」「←と→を同時に押すとキャラが止まる」「トリガーの震えでcommandが連打になる」。Unityでこれらを直すたびに、Update内でframe数や`Time.time`を数える小さな処理を書き直すことになります。

このmoduleは、その6種類の判断を1つのpackageへまとめます。各部品へ tick、command ID、候補値、priority など必要な入力を明示的に渡します。Input SystemもUnity時刻も内部で読まないので、pause中、fixed simulation、Replay、単体テストのどこでも同じ入力列から同じ結果を再現できます。

## できること

- 操作可能になる少し前に押されたcommandを、tick単位の短い有効期間だけ保持して順番に消費する。
- Light→Light→Heavyのような順序入力を、間隔上限と再開規則付きで判定する。
- 複数buttonの同時押しを、成立幅の上限と再武装規則付きで判定する。
- 複数のcommand候補から最大priorityを選び、同値なら入力配列の小さいindexを選ぶ。
- ←と→のような相反入力を、宣言したpolicy（neutral / 片側優先 / 後押し優先）で-1・0・1へ解決する。
- 震える入力を、同じ候補がN回連続した時だけ確定させて落ち着かせる。

6つは同じpackageから選んで使える独立部品です。Buffer、Sequence Matcher、Chord Matcher、Axis Conflict Resolverは明示tickを使い、Stabilizerはsample回数で進み、Arbiterは状態を持ちません。共通facadeや自動pipelineはなく、入出力が異なる部品を繋ぐadapterは利用側が持ちます。

## 使わない方がよい場合

- Input SystemやLegacy Input Managerから値を読みたい。このmoduleはbutton edgeを読みません。adapterは利用側が書きます。
- stickのdead zone、感度curve、平滑化、方向分割がほしい。それは **入力補助（Input Assist）** の担当です。
- 入力そのものを一時的に止めたい。それは **入力の一時停止（Input Gate）** の担当です。
- 秒やUnity frameで期限を数えたい。時間を扱う4部品は、利用側が進めるtickだけを基準にします。
- Player・AIの行動決定、animation遷移、network同期がほしい。いずれも対象外です。

## 3分で試す

### 1. 導入する

Unity 6000.5.7f1以降で、Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputCommand#input-command-v1.0.0
```

または`InputCommand` folderをprojectの`Assets/Modules/`へ配置します。Input System packageへの依存はありません。

### 2. Sampleをimportする

Package Managerの **Samples** から目的に近いものをImportします。

| Sample | 確認できること |
|---|---|
| Command Buffer Basics | tick 100のJump記録、tick 101でのFIFO消費、tick 104での期限切れ |
| Sequence Matcher Basics | Light-Light-Heavy一致、間隔超過での失敗と再開 |
| Chord Matcher Basics | 同時押しの成立、再武装、遅れた押下の拒否 |
| Command Arbiter Basics | 最大priority選択、同値時は入力配列の小さいindex、重複拒否 |
| Axis Conflict Resolver Basics | 後押し優先、片側releaseでの復帰、同一tickの引き分け |
| Stabilizer Basics | 候補の進行、3sample目での確定、ノイズの打ち消し |

### 3. tickを使う部品の進行元を決める

Buffer、Sequence Matcher、Chord Matcher、Axis Conflict Resolverを使う場合は、`FixedUpdate`、Simulation Clockのstep、Replay再生のいずれか1か所だけがtickを進めるようにします。tickは戻せません。StabilizerとArbiterはtickを受け取りません。

## 最小コード

先行入力bufferだけを使う最小構成です。

```csharp
using InputBuffering;
using UnityEngine;

public sealed class JumpBuffer : MonoBehaviour
{
    private const int JumpCommandId = 1;

    private InputCommandBuffer _buffer;
    private ulong _tick;

    private void Awake()
    {
        // 容量3、記録tickから2tick後まで有効、開始tick 0
        InputCommandBuffer.TryCreate(3, 2, 0, out _buffer, out _);
    }

    private void FixedUpdate()
    {
        _tick++;
        _buffer.TryAdvanceTo(_tick, out _, out _);

        if (JumpButtonWasPressedThisStep())
            _buffer.TryRecord(JumpCommandId, out _, out _);

        if (IsGrounded() && _buffer.TryConsume(JumpCommandId, out var command, out _))
            Debug.Log($"Jump consumed at tick {_tick} (recorded at {command.RecordedTick})");
    }

    private bool JumpButtonWasPressedThisStep() => false; // 利用側のadapterで置き換える
    private bool IsGrounded() => true;                    // 利用側の判定で置き換える
}
```

状態を持つ部品は`TryCreate`で設定を固定し、各部品の契約に従ってtickまたはsampleを渡し、返ってきたstatus structを読みます。状態を持たないArbiterは候補配列を呼び出しごとに選択します。

```csharp
using InputSequencing;

InputSequenceMatcher.TryCreate(new[] { 1, 1, 2 }, 12, 0, out var matcher, out _);
matcher.TryPush(tick, commandId, out var status, out _);
if (status.Matched) { /* Light-Light-Heavyが成立 */ }
```

## 実行するとどうなるか

- Sample sceneを開いてPlayすると、実Buttonで各段の入力を1stepずつ進められます。960×600でも640×360でも収まる版が用意されています。
- 画面上のlabelが、現在tick、保持中entry、進行度、確定値のように段ごとの内部stateを毎step表示します。成功も失敗も同じ場所に出ます。
- Consoleには何も出ません。このmoduleはlogを出さず、結果はすべて戻り値のstatus structとerror enumで返します。
- 生成fileはありません。ScriptableObject、`Resources`、Project Settingsへの書き込みはありません。
- 各部品へ同じ設定と同じ明示入力列を渡せば、実行のたびに同じ結果になります。

## よくある問題

**`InputCommandBuffer`が見つからない / 型が解決できない**
`using`が必要です。namespaceは段ごとに分かれています（`InputBuffering`、`InputSequencing`、`InputChording`、`InputArbitration`、`InputAxisConflict`、`InputStabilization`）。自前のassemblyから使う場合は、そのasmdefの`references`へ`InputCommand.Runtime`を追加してください。

**commandを記録したのに消費できない**
`TryAdvanceTo`でtickを進め過ぎて期限切れになっているか、`commandId`が一致していません。`TryPeek`で保持内容を確認してください。commandIdは正の`int`のみで、0以下は拒否されます。

**`Try...`が`false`を返すが原因が分からない**
`out`のerror enumを見てください。容量超過、tick逆行、不正id、未発見をすべて別の値で区別しています。失敗時に既存stateは変わりません。

**入力が1step飛ぶ / 二重に進む**
tickを進める場所が複数あります。`InputCommandBuffer`、`InputSequenceMatcher`、`InputChordMatcher`、`InputAxisConflictResolver`へ渡すtickは1か所から供給してください。同じtickの再入力は受理されますが、逆行は拒否されます。`InputCommandStabilizer`はtickではなく呼び出したsample回数で進みます。

**Input Assistとの違い**
Input Assistは生の`Vector2`・`bool`を扱いやすい値とgestureへ変換する前段です。このmoduleは、そこから出た離散commandをbuffer、matcher、arbiterなど目的別の部品で扱います。tickを使う部品は利用側のsimulation stepに従い、StabilizerとArbiterはtickを受け取りません。Input Gateは入力の実行可否そのものを止める別moduleです。

**Unity versionが古い**
`unity: 6000.5` / `unityRelease: 7f1` を対象にしています。Sampleは`com.unity.modules.uielements`を使います。

## 詳しい契約

### namespaceと互換性

C# namespace、型名、member、動作は旧packageから変更しておらず、source / API 互換です。runtime assembly名は変わるためbinary互換ではありません。自作asmdefの`references`を`InputCommand.Runtime`へ変更し、旧assemblyを参照するprecompiled DLLは再buildしてください。

| 段 | namespace | 主な型 |
|---|---|---|
| 先行入力 | `InputBuffering` | `InputCommandBuffer`、`BufferedInputCommand`、`InputCommandBufferError` |
| 順序判定 | `InputSequencing` | `InputSequenceMatcher`、`InputSequenceStatus`、`InputSequenceError` |
| 同時押し判定 | `InputChording` | `InputChordMatcher`、`InputChordStatus`、`InputChordError` |
| 優先順位 | `InputArbitration` | `InputCommandArbiter`、`InputCommandCandidate`、`InputCommandArbitrationResult`、`InputCommandArbitrationError` |
| 軸競合解決 | `InputAxisConflict` | `InputAxisConflictResolver`、`InputAxisConflictPolicy`、`InputAxisConflictStatus`、`InputAxisConflictError` |
| 安定化 | `InputStabilization` | `InputCommandStabilizer`、`InputCommandStatus`、`InputStabilizationError` |

assemblyは`InputCommand.Runtime`1つです。旧`InputCommandBuffer.Runtime`等の6assemblyは統合されました。自前asmdefの`references`だけは書き換えが必要です。

### 統合前のpackage

このpackageは次の6つを1つの導入単位へまとめたものです。

- `com.studiogaku.input-command-buffer`
- `com.studiogaku.input-sequence-matcher`
- `com.studiogaku.input-chord-matcher`
- `com.studiogaku.input-command-arbiter`
- `com.studiogaku.input-axis-conflict-resolver`
- `com.studiogaku.input-stabilizer`

公開済みのtagとUPM識別子は削除しません。旧配布単位を継続利用する入口としてそのまま使えます。旧packageと本packageは同じ型を別assemblyに含むため同時導入できません。新規導入では、このpackageを推奨します。

### 固定契約

- command idは利用側が定義する正の`int`。0以下は拒否する。
- tickは利用側が進める`ulong`。同一tickの再入力は受理し、逆行tickは状態を変えず拒否する。
- 期限判定はtickの差分で行うため、`ulong.MaxValue`付近でもoverflowしない。
- `InputCommandBuffer`の容量は`1..1024`の固定長で、期限内entryを黙って上書きしない。有効期間は記録tickから`RetentionTicks`後まで両端を含む。同一idの重複記録は最も古い一致から消費する。
- `InputSequenceMatcher`は間隔上限超過で進行をtimeoutさせ、規則に従って再開する。
- `InputChordMatcher`は必要command集合が成立幅の上限内で揃った時だけtriggerし、成立後はrearmまで再triggerしない。
- `InputCommandArbiter`は状態を持たない静的選択で、候補は最大64件。最大priorityを選び、同値は入力配列の小さいindexで解決する。有効候補が無い場合は選択なしを返す。
- `InputCommandStabilizer`はtickではなくsample回数で動き、同じ候補が`RequiredConsecutiveSamples`回連続した時だけ確定値を更新する。
- すべての失敗は段ごとのerror enumで区別し、失敗時に既存stateを変更しない。例外も暗黙clampも使わない。

内部で現在時刻、Unity frame、`deltaTime`、乱数、Unity API、global stateを読みません。

### 対象外

- Input System・Legacy Input Managerからの読取、button edge検出、rebind
- analog量子化、dead zone、感度curve、平滑化、方向分割
- 秒・Unity frame・Coroutineによる期限管理
- animation遷移、行動決定、network同期、入力の記録・再生
- callback、event通知、global service、singleton、file I/O

### テスト範囲

EditMode assembly`InputCommand.Editor.Tests`が、6段それぞれの境界値、順序、失敗時非変更、reset・clearを検証します。各Sampleは実PanelのButton操作をPlayMode testで検証します。
