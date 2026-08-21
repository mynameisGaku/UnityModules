# 入力補助（Input Assist）

## 30秒で分かる

スティック入力とボタン入力で毎回作っていた小さな処理を、2つの設定へまとめるmoduleです。

- `InputVectorFilter`: dead zone、感度curve、急変抑制、4方向・8方向判定
- `InputButtonTracker`: press、release、hold、repeat、single・multi-tap

Input Systemを直接読みません。`Vector2`、`bool`、経過時間を渡すだけなので、旧Input Manager、Input System、AI、Replay、単体テストのどこからでも同じ処理を使えます。

## こんな面倒を減らす

- controllerごとにdead zone、感度curve、平滑化の順番を組み直す。
- tap、長押し、連続入力、double tapを別々のclassで管理する。
- Update内で`Time.deltaTime`を直接読み、同じ入力をテストで再現できない。
- 入力補正の小packageを何個も選び、導入順と使い分けを調べる。

## 3分で使う

### 1. 導入する

Unity 6000.5.7f1以降で、Package Managerの **Add package from git URL...** へ次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/InputAssist#input-assist-v1.0.0
```

または`InputAssist` folderをprojectの`Assets/Modules/`へ配置します。Input System packageへの依存はありません。

### 2. componentへ設定を埋め込む

```csharp
using InputAssist;
using UnityEngine;

public sealed class PlayerInputAdapter : MonoBehaviour
{
    [SerializeField] private InputVectorFilter _move = new InputVectorFilter();
    [SerializeField] private InputButtonTracker _action = new InputButtonTracker();

    public void UpdateInput(Vector2 rawMove, bool actionPressed, float deltaTime)
    {
        var move = _move.Process(rawMove, deltaTime);
        var action = _action.Process(actionPressed, deltaTime);

        if (!move.Succeeded || !action.Succeeded) return;
        transform.position += new Vector3(move.Value.x, 0f, move.Value.y) * deltaTime;

        if (action.Events.HasFlag(InputButtonEvent.TapCompleted))
            Debug.Log($"Tap count: {action.TapCount}");
    }
}
```

`InputVectorFilter`と`InputButtonTracker`は`[Serializable]`なので、そのままInspectorへ設定が表示されます。

### 3. 時間を呼び出し側から渡す

通常操作では`Time.deltaTime`、pause中も動かすUIでは`Time.unscaledDeltaTime`、固定tickではtick間隔を渡します。処理器自身はUnityの現在時刻を読みません。

## 何がまとまっているか

| やりたいこと | 設定・結果 |
|---|---|
| stickの中央ぶれを消す | `InnerDeadZone` / `OuterDeadZone` |
| 小さい入力を弱くする | `ResponseMode` |
| 入力の急変を抑える | `RiseSpeed` / `FallSpeed` |
| menuやgrid用に方向を得る | `DirectionMode` / `Direction` |
| 押した瞬間・離した瞬間 | `Pressed` / `Released` |
| 長押し開始 | `HoldStarted` |
| 押しっぱなしの連続入力 | `Repeated` / `RepeatCount` |
| single・double・triple tap | `TapCompleted` / `TapCount` |

失敗時は例外や暗黙clampではなく`InputAssistError`を返し、前回の成功状態を維持します。

## 既存の細分化moduleとの関係

新しいprojectでは、dead zone、response curve、smoothing、direction quantization、press、repeat、multi-tapを個別packageとして組み合わせる代わりにInput Assistを推奨します。

公開済みの旧packageとtagは既存利用者の互換性のため残します。旧APIを利用中のprojectを自動移行したり、同名型を上書きしたりはしません。

## 対象外

- Input Actionやdeviceの読取・rebind
- Action Mapの一時停止（**入力の一時停止（Input Gate）** が担当）
- command sequence、chord、buffer、priority arbitration
- network同期、入力record、Player/AIの判断

このmoduleは「生の入力値を、ゲーム側が扱いやすい値とgestureへ変換する」範囲に絞ります。

## サンプル

Package ManagerのSamplesから **Input Assist Basics** をImportし、`InputAssistBasics.unity`を開いてください。実Buttonでstick補正、tap、hold、repeatを切り替え、960×600と640×360の両方で収まる画面から結果を確認できます。
