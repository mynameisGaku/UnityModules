# Gameplay Rules（ゲーム判定・計算）

## 30秒で分かる説明

回復量のclamp、cost不足の判定、buffの重ねがけ、抽選、報酬の按分、tier進捗、cooldown、damage軽減、敵対度の並べ替え。ゲームルールの数値計算は、毎回MonoBehaviourの中に書き足され、`Time.deltaTime`や`Random`と混ざり、後から再現もtestもできなくなります。

このmoduleは、その計算だけをEngine非依存の純粋関数とimmutable structへ切り出して1つの導入単位にまとめたものです。時刻・frame・乱数・global stateを一切読まず、入力に対して常に同じ結果と、その結果を再構築できる明示的な内訳を返します。呼び出し側は「いつ呼ぶか」だけを決めます。

## できること

- resource量とcost支払いを、不足量まで含めた明示結果として受け取れる。
- 能力値補正、抽選、整数按分、curve、閾値tierを、順序が決まった再現可能な計算として扱える。
- 標本統計・傾向推定で、直近の値の要約と次の値の予測を得られる。
- charge回復、定期発火、時限stack、stack移送を、明示tickだけで進められる。
- 数値条件、utility score、敵対度、damage軽減を、途中経過つきで判定できる。
- 失敗は例外ではなく明示errorとして返り、失敗時にstateが壊れない。

### 名前空間の対応表

必要な計算は、名前空間から探せます。

| 名前空間 | できる計算 |
|---|---|
| `GameplayResources` | 有限resourceの回復・部分消費・全量必須消費と、複数resourceのcost支払い可否・残量・不足量 |
| `GameplayStats` | flat・加算percent・乗算factorの段階で能力値補正を合成 |
| `GameplaySelection` | 正の重みを持つentryから、明示した正規化sampleで1件を選ぶ |
| `GameplayAllocation` | 整数総量を重みで按分し、最大剰余法で総量を厳密に保つ |
| `GameplayMath` | 折れ線curveのX→Y補間、segment特定、端点clamp |
| `GameplayMetrics` | 固定長FIFO windowでのsample保持、押し出し、最小・最大・平均 |
| `GameplayAnalysis` | 最小・最大・平均・範囲・分散・標準偏差の要約と、最小二乗による傾き・切片・次値予測 |
| `GameplayProgression` | 値を順序つき閾値tierへ割り当て、現tier・次tier・進捗率を返す |
| `GameplayTiming` | charge消費と順次recharge、明示tickでの定期発火計画と追いつき処理 |
| `GameplayEffects` | 効果の再付与時に、stack数と残りtickをpolicyから決定 |
| `GameplayInventory` | 複数source・複数destination間の整数移送計画 |
| `GameplayRules` | 数値条件の充足・未達をline単位で判定 |
| `GameplayDecision` | 候補のutility scoringと、ヒステリシスつきの安定した選択維持 |
| `GameplayDamage` | flat・比率のmitigation層を入力順に適用したdamage内訳 |
| `GameplayThreat` | 敵対度scoreの増減適用と、安定したleader決定 |

## 使わない方がよい場合

- 毎frame自動で進む処理が欲しい場合。このmoduleは`Update`も`deltaTime`も持たず、tickは呼び出し側が渡します。
- 乱数そのものが欲しい場合。抽選は「正規化済みsampleを渡す」契約で、乱数生成は担当しません。
- state保存、UI、animation、audio、入力、network同期が欲しい場合。いずれも責務外です。
- ability system、AI behaviour tree、inventory UIのような枠組みが欲しい場合。ここにあるのはその内部で使う計算だけです。
- Unityの`AnimationCurve`や`Mathf`で足りる単発計算しか要らない場合。導入する利点がありません。

## 3分で試す

1. Package ManagerでAdd package from git URLを選び、次を貼り付けます。

   ```text
   https://github.com/mynameisGaku/UnityModules.git?path=/GameplayRules#gameplay-rules-v1.0.0
   ```

   またはGitを使わない場合は、`GameplayRules`folderを`Assets/Modules/`へcopyします。

2. Package ManagerでGameplay Rulesを選び、Samplesから試したいものをImportします。19個のBasics sampleがあり、名前は統合前のmodule名と同じです。まずは`Resource Meter Basics`を選びます。

3. Importされた`Samples/Gameplay Rules/1.0.0/Resource Meter Basics/`の中のSceneを開き、Play modeに入ります。

4. asmdefを使っていない通常のScriptでは、必要な計算の名前空間を`using`するだけです。自作asmdef配下のScriptから使う場合は、そのasmdefのReferencesへ`GameplayRules.Runtime`を追加します。`autoReferenced: true`はpredefined assemblyからの自動参照を有効にする設定です。

## 最小コード

```csharp
using GameplayResources;

public static class ResourceExample
{
    public static void Run()
    {
        if (!ResourceMeter.TryCreate(100d, 40d, out var meter, out var error))
        {
            return;
        }

        var restored = meter.Restore(30d);                                  // current 70、applied +30
        var spent = meter.Spend(80d, ResourceSpendPolicy.AllowPartial);     // current 0、applied -70、unapplied -10
    }
}
```

他の計算も同じ形です。`TryCreate`系で入力を検証し、操作は結果structを返し、失敗は戻り値のerrorで表現します。

## 実行するとどうなるか

- sampleのSceneをPlayすると、UI Toolkitで作られたPanelに操作Buttonが並びます。Buttonを押すと、その操作の前後値・要求量・実際に適用された量・未適用量・境界到達flagが画面に表示されます。
- 表示は端末解像度に追従します。960×600では5列、640×360では3+2列で並び、`Time.timeScale = 0`でも同じ結果が再現されます。
- 自分のcodeで使った場合、Consoleへの出力もfile生成もありません。結果は戻り値のstructだけに現れます。
- Project内には`GameplayRules.Runtime`という1つのassemblyだけが増えます。統合前のように19個のassemblyがcompileされることはありません。

## よくある問題

- **型が見つからない** — `using`している名前空間が対応表と合っているか確認します。型名は統合前と同一です。assembly名だけが`GameplayRules.Runtime`に変わりました。
- **自作asmdefから参照できない** — 自作assemblyのReferencesに`GameplayRules.Runtime`を追加します。統合前の`ResourceMeter.Runtime`などの名前は存在しません。
- **統合前のpackageと同時に入れている** — 型が重複してcompile errorになります。Package Managerで統合前のpackageを削除してから、このpackageだけを残します。
- **sampleがcompileされない** — sampleは`com.unity.modules.uielements`に依存します。UI Toolkit moduleが無効な環境ではImportしないでください。runtime本体はUnity Engineに依存しません。
- **対応version** — Unity 6000.5 (7f1)以降で検証しています。
- **時間で自動に進まない** — 仕様です。cooldownや定期発火は、呼び出し側が渡した明示tickの分だけ進みます。frame駆動が必要な場合は、`SimulationClock`など時計を持つmoduleと組み合わせます。

## 詳しい契約

### 共通の不変条件

- すべての計算はEngine非依存です。時刻、frame、`deltaTime`、乱数、Unity API、global state、static可変stateを読みません。
- 同じ入力列は必ず同じ出力列になります。順序はID順または入力順で固定され、浮動小数の加算順序も固定です。
- 入力の妥当性検査は操作の前に行われ、失敗時はstateを変更しません。NaN・Infinity・負値・範囲外・重複IDは明示errorになります。
- 「有効な要求だが全量を適用できない」状態は失敗ではなく正常結果です。成功flagと「全量適用できたか」のflagは別に返ります。
- 結果structには、前後値と各種deltaや途中stepが含まれ、呼び出し後に計算過程を再構築できます。
- collectionは上限が固定です（多くは32件、敵対度の調整は64件）。上限超過は明示errorです。
- callback、event、singleton、service locatorはありません。

### 非対象

自動回復とtimer、乱数生成、status effectの適用先state、UI・animation・audio・入力、Save System連携、file I/O、network transport、Replay再生。

### assemblyとtest

- runtime assemblyは`GameplayRules.Runtime`のみ（`noEngineReferences: true`、`autoReferenced: true`、`rootNamespace`なし）。predefined assemblyからは自動参照され、自作asmdefからは明示参照する。
- EditMode testは`GameplayRules.Editor.Tests`にまとまっています。境界値、clamp、不足、順序安定性、不正入力での非変更、結果のequality、再現可能なsequenceを検証します。
- 各sampleは`<統合前module名>.Samples`という独立assemblyのままで、runtimeは`GameplayRules.Runtime`を参照します。PlayMode testも各sample内に残っています。

### 統合前のpackageからの移行

このpackageは、次の19個のpackageを1つにまとめたものです。

| 旧UPM識別子 | 旧displayName | 互換tag |
|---|---|---|
| `com.studiogaku.resource-meter` | Resource Meter | `resource-meter-v1.0.0` |
| `com.studiogaku.resource-cost-evaluator` | Resource Cost Evaluator | `resource-cost-evaluator-v1.0.0` |
| `com.studiogaku.stat-modifier-stack` | Stat Modifier Stack | `stat-modifier-stack-v1.0.0` |
| `com.studiogaku.weighted-choice-table` | Weighted Choice Table | `weighted-choice-table-v1.0.0` |
| `com.studiogaku.weighted-integer-allocator` | Weighted Integer Allocator | `weighted-integer-allocator-v1.0.0` |
| `com.studiogaku.piecewise-linear-curve` | Piecewise Linear Curve | `piecewise-linear-curve-v1.0.0` |
| `com.studiogaku.rolling-sample-window` | Rolling Sample Window | `rolling-sample-window-v1.0.0` |
| `com.studiogaku.sample-statistics` | Sample Statistics | `sample-statistics-v1.0.0` |
| `com.studiogaku.linear-trend-estimator` | Linear Trend Estimator | `linear-trend-estimator-v1.0.0` |
| `com.studiogaku.threshold-tier-table` | Threshold Tier Table | `threshold-tier-table-v1.0.0` |
| `com.studiogaku.charge-cooldown` | Charge Cooldown | `charge-cooldown-v1.0.0` |
| `com.studiogaku.periodic-tick-planner` | Periodic Tick Planner | `periodic-tick-planner-v1.0.0` |
| `com.studiogaku.timed-stack-resolver` | Timed Stack Resolver | `timed-stack-resolver-v1.0.0` |
| `com.studiogaku.stack-transfer-planner` | Stack Transfer Planner | `stack-transfer-planner-v1.0.0` |
| `com.studiogaku.numeric-requirement-evaluator` | Numeric Requirement Evaluator | `numeric-requirement-evaluator-v1.0.0` |
| `com.studiogaku.utility-score-evaluator` | Utility Score Evaluator | `utility-score-evaluator-v1.0.0` |
| `com.studiogaku.stable-score-selector` | Stable Score Selector | `stable-score-selector-v1.0.0` |
| `com.studiogaku.damage-mitigation-evaluator` | Damage Mitigation Evaluator | `damage-mitigation-evaluator-v1.0.0` |
| `com.studiogaku.threat-score-resolver` | Threat Score Resolver | なし（単独tag未公開） |

- Threat Score Resolverを除く18packageは、公開済みtag内に旧UPM識別子を保っています。Threat Score Resolverは単独tagがなく、旧UPM識別子を指定して導入できる旧配布入口もありません。本packageで初めてtag付き配布になります。新規導入では`com.studiogaku.gameplay-rules`を使ってください。
- **C#の名前空間、型名、member名、およびその挙動は統合前と同一で、source / API互換です。** runtime assembly名は変わるためbinary互換ではありません。自作asmdefのReferencesを`GameplayRules.Runtime`へ変更し、旧assemblyを参照するprecompiled DLLは再buildしてください。
- 統合前のpackageと本packageを同時に導入すると型が重複します。どちらか一方だけを導入してください。
