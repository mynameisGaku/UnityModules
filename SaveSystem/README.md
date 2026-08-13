# Save System

ゲーム固有のデータを `[Serializable]` な型のまま保存し、失敗理由まで結果で受け取れる同期保存モジュールです。
一時ファイルへの耐久書き込み、SHA-256 チェックサム、直前データの 1 世代バックアップを標準構成にまとめています。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

外部パッケージへの依存はありません。`unsafe` も使用していません。

## インストール

Package Manager の **Add package from git URL** に、固定タグを含む次の URL を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/SaveSystem#save-system-v1.0.0
```

利用側に asmdef がある場合は `SaveSystem.Runtime` を参照します。

フォルダーを直接管理する場合だけ、`SaveSystem/` を `Assets/Modules/SaveSystem/` へ配置してください。

## 最小例

```csharp
using System;
using SaveSystem;

[Serializable]
public sealed class PlayerSaveData
{
    public int Level;
    public int Coins;
}

var saves = SaveService.CreateDefault();

var saved = saves.Save("manual_1", new PlayerSaveData { Level = 12, Coins = 340 }, "1");
if (!saved.IsSuccess)
{
    UnityEngine.Debug.LogError($"保存失敗: {saved.Error} / {saved.Message}");
}

var loaded = saves.Load<PlayerSaveData>("manual_1", "1");
if (loaded.IsSuccess)
{
    Apply(loaded.Value);
}
else
{
    UnityEngine.Debug.LogWarning($"読込失敗: {loaded.Error} / {loaded.Message}");
}
```

`CreateDefault()` は `Application.persistentDataPath/Saves` に保存します。
主ファイルは `<slot>.save`、直前の内容は `<slot>.save.bak` です。

## 公開 API

| API | 内容 |
|---|---|
| `SaveService.Save<T>` | 型、データ版、保存時刻、チェックサムと一緒に値を同期保存する |
| `SaveService.Load<T>` | 型とデータ版を確認し、主データが壊れていればバックアップから復旧する |
| `SaveService.Delete` | 主データ、バックアップ、同じスロットの処理残骸を削除する |
| `SaveService.ListSlots` | 主データまたはバックアップがあるスロット名を固定済みの一覧で返す |
| `SaveService.CreateDefault` | `FileSaveStorage` と `UnityJsonSaveSerializer` の標準構成を作る |
| `SaveSlot.IsValid` | 保存先に使えるスロット名かを調べる |

保存、読み込み、削除、一覧取得の想定内の失敗は例外ではなく結果型で返ります。

```csharp
var slots = saves.ListSlots();
if (!slots.IsSuccess)
{
    ShowError(slots.Error, slots.Message);
    return;
}

foreach (var slot in slots.Slots)
{
    AddSlotButton(slot);
}
```

`SaveSlotListResult.Slots` は取得時点で複製された読み取り専用の一覧です。失敗時も `null` ではなく空一覧になります。

## 破損検出と復旧

保存時は次の順で処理します。

1. ゲームデータを JSON に変換する。
2. 型、データ版、UTC 保存時刻を含む保存形式を作る。
3. 保存形式の SHA-256 チェックサムを計算する。
4. 一時ファイルへ書き込み、可能な環境では `File.Replace` で主ファイルを置換する。
5. 置換前の主ファイルを 1 世代だけバックアップに残す。

読み込み時に主ファイルが見つからない、または破損している場合は、バックアップを検証して読み込みます。
成功時はバックアップを主ファイルへ戻し、`SaveLoadResult<T>.Metadata.RecoveredFromBackup` が `true` になります。

`TypeMismatch`、`VersionMismatch`、`FormatVersionMismatch` は破損として扱いません。`Load` は主ファイルやバックアップを書き換えずに失敗結果を返すため、利用側で移行や型の選び直しを行えます。

チェックサムは転送不良や途中書き込みなどの偶発的な破損を見つけるためのものです。改ざん防止、認証、暗号化にはなりません。

## エラー

| `SaveError` | 意味 |
|---|---|
| `InvalidSlot` | スロット名に使えない文字または長さがある |
| `InvalidData` | 保存値が `null`、またはデータ版が空 |
| `NotFound` | 主データとバックアップがない |
| `CorruptData` | 保存形式版が欠落または0以下、もしくは必須項目、時刻、チェックサムが不正 |
| `FormatVersionMismatch` | 正の保存形式版がこのモジュールの対応版と異なる |
| `VersionMismatch` | 保存済みのデータ版が要求した版と異なる |
| `TypeMismatch` | 保存時と読み込み時の型が異なる |
| `SerializationFailed` | 値と JSON の相互変換に失敗した |
| `StorageFailed` | 保存先の読み書き、削除、列挙に失敗した |
| `TimeProviderFailed` | 保存時刻を取得できなかった |

`SaveError.None` は成功を表します。成功判定には各結果の `IsSuccess` を使用してください。

## 保存スロット名

文字と数字、`-`、`_` を 64 文字まで使用できます。日本語も使用できます。
空白、スラッシュ、`../`、Windows の予約デバイス名などは、保存先外への書き込みや環境差を防ぐため拒否します。

## 保存先と変換方法を差し替える

`ISaveStorage` と `ISaveSerializer` は利用側で実装できます。

```csharp
var storage = new ProjectFileSaveStorage(saveDirectory);
var serializer = new ProjectSaveSerializer();
var saves = new SaveService(storage, serializer);
```

この差し替え境界は、呼び出し中に完了できるローカル保存や暗号化、独自形式に使用します。
ネットワーク通信を伴うクラウド保存は `ISaveStorage` の中で待機させず、ゲーム側の非同期処理がダウンロード・アップロードとローカル保存の同期を所有してください。

## 制約

- すべての公開操作は同期処理です。ファイル I/O が終わるまで呼び出し元を待たせます。
- 同じスロットへの操作を並行して行うことには対応しません。1 つの所有者から順番に呼び出してください。
- `SaveService.CreateDefault` と `FileSaveStorage` は同期ファイル保存を保証できない WebGL Player と tvOS Player に対応しません。該当 Player では別の `ISaveStorage` を指定してください。
- `UnityJsonSaveSerializer` の保存ルートには、`[Serializable]` を型の宣言に付けた具象クラスまたは構造体が必要です。プリミティブ、列挙型、文字列、配列、`List<T>` は、対応するフィールドを持つ型で包んでください。`Dictionary<TKey, TValue>` と `UnityEngine.Object` は、次の制約に従って保存用の値へ変換します。
- 標準変換が保存するのはフィールドです。public フィールドまたは `[SerializeField]` を付けた private フィールドを使い、プロパティは保存対象にしないでください。入れ子の独自データ型にも型の宣言へ `[Serializable]` を付けます。
- 配列と `List<T>` は対応するフィールドとして使用できますが、`Dictionary<TKey, TValue>` はフィールドでも対応しません。辞書はキーと値の一覧などへ変換してください。
- `UnityEngine.Object` 参照は保存後も同じ対象を指す保証がないため直接保存せず、GUID や利用側で管理する永続 ID へ変換してください。保存ルートの宣言型と実行時型が異なる値も、派生フィールドの欠落を防ぐため標準変換では拒否します。
- 標準変換は入れ子の全フィールドを事前検査しません。`object`、インターフェース、基底型で宣言して派生値を入れたフィールドなど、`JsonUtility` が完全に復元できない内部状態は、具体的な保存用データへ変換してから渡してください。
- データ版の移行はゲーム固有です。本モジュールは版の記録と一致確認だけを行います。
- `File.Replace` がない環境では、旧主データをバックアップへ耐久書き込みしてから主ファイルを切り替えます。この代替経路は切り替え自体の原子性を保証しませんが、失敗時も旧データを主ファイルまたはバックアップに残します。

## サンプル

Package Manager から **Save System Basics** を Import し、`SaveSystemBasics.unity` を開いて Play してください。
Game View のボタンと **Save System Basics** コンポーネントの Context Menu から、保存、読み込み、削除を確認できます。
Play の開始回数は自動的に保存されるため、Play を止めて再開しても状態が続くことを画面で確認できます。

利用条件は [LICENSE.md](LICENSE.md)、同梱物と外部依存は [Third-Party Notices.txt](Third-Party%20Notices.txt) を参照してください。
