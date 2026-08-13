# Save System

`SaveService` は、型付きデータの変換、保存形式の検証、保存先への同期書き込み、1 世代バックアップからの復旧をまとめます。
導入の最短手順と動作例はパッケージ直下の [README](../README.md) を参照してください。

動作確認済み: **Unity 6000.5.7f1** / Windows / .NET Standard 2.1

## 導入

Package Manager の **Add package from git URL** に次を指定します。

```text
https://github.com/mynameisGaku/UnityModules.git?path=/SaveSystem#save-system-v1.0.0
```

利用側の asmdef から `SaveSystem.Runtime` を参照します。外部パッケージへの依存はありません。

## 標準構成

```csharp
var saves = SaveService.CreateDefault();
```

標準構成は `Application.persistentDataPath/Saves` の `FileSaveStorage` と、`JsonUtility` を使う `UnityJsonSaveSerializer` で構成されます。
別フォルダーに分ける場合は `SaveService.CreateDefault("ProfileSaves")` のように指定します。

## `SaveService`

### 保存

```csharp
SaveOperationResult result = saves.Save("auto", data, dataVersion: "2");
```

`dataVersion` はゲーム側が所有する任意の文字列です。値が `null`、データ版が空、変換や書き込みに失敗した場合は `IsSuccess == false` の結果を返します。

### 読み込み

```csharp
SaveLoadResult<PlayerSaveData> result = saves.Load<PlayerSaveData>("auto", expectedDataVersion: "2");
```

主データを検証し、見つからないか破損している場合だけ 1 世代バックアップを試します。
バックアップから復旧したときは `result.Metadata.RecoveredFromBackup` が `true` です。

型、データ版、保存形式版が一致しない場合は `TypeMismatch`、`VersionMismatch`、`FormatVersionMismatch` のいずれかを返します。この場合は保存ファイルを変更しません。

### 削除

```csharp
SaveOperationResult result = saves.Delete("auto");
```

主データ、バックアップ、一時的な処理残骸を削除します。何も存在しない場合も成功です。

### スロット一覧

```csharp
SaveSlotListResult result = saves.ListSlots();
if (result.IsSuccess)
{
    foreach (var slot in result.Slots)
    {
        ShowSlot(slot);
    }
}
```

`Slots` は取得時点で固定した読み取り専用スナップショットです。主データかバックアップが存在する有効な名前を、重複なしの名前順で返します。
列挙に失敗した場合は `StorageFailed` と空一覧を返します。

## 結果型

| 型 | 主な値 |
|---|---|
| `SaveOperationResult` | `IsSuccess`, `Error`, `Message`, `Metadata` |
| `SaveLoadResult<T>` | `IsSuccess`, `Value`, `Error`, `Message`, `Metadata` |
| `SaveSlotListResult` | `IsSuccess`, `Slots`, `Error`, `Message` |
| `SaveMetadata` | `Slot`, `DataVersion`, `SavedAtUtc`, `RecoveredFromBackup` |

通常の失敗は結果として返ります。`FileSaveStorage` のコンストラクターへ不正なパスを渡した場合など、呼び出し契約自体に違反した場合は例外になります。

## `SaveError`

| 値 | 内容 |
|---|---|
| `None` | 成功 |
| `InvalidSlot` | 保存スロット名が不正 |
| `InvalidData` | 保存値またはデータ版が不正 |
| `NotFound` | 主データとバックアップがない |
| `CorruptData` | 保存形式版が欠落または0以下、もしくは必須項目やチェックサムが不正 |
| `FormatVersionMismatch` | 正の保存形式版がモジュールの対応版と一致しない |
| `VersionMismatch` | データ版が一致しない |
| `SerializationFailed` | 値と保存文字列の変換に失敗 |
| `StorageFailed` | 保存先の操作に失敗 |
| `TypeMismatch` | 保存時と読込時の型が一致しない |
| `TimeProviderFailed` | UTC 保存時刻の取得に失敗 |

## 保存形式と復旧

保存形式には形式版、ゲーム側のデータ版、型識別子、UTC 保存時刻、JSON payload、SHA-256 チェックサムが入ります。
チェックサムは偶発的な破損検出用であり、暗号化、認証、改ざん防止ではありません。

`FileSaveStorage` は一時ファイルを耐久書き込みした後、主ファイルを置換します。置換前の主ファイルは `<slot>.save.bak` に 1 世代だけ残ります。
主ファイルが破損または消失し、バックアップが検証を通った場合は、その内容を返した後に主ファイルへ書き戻します。

型不一致、データ版不一致、保存形式版不一致は破損ではないため、自動復旧や書き戻しを行いません。利用側で移行方法を決めてから明示的に保存してください。

## 拡張点

| インターフェース | 責務 |
|---|---|
| `ISaveSerializer` | 値と保存文字列を相互変換する |
| `ISaveStorage` | スロット単位で主データとバックアップを同期保管する |

```csharp
var storage = new ProjectFileSaveStorage(saveDirectory);
var serializer = new ProjectSaveSerializer();
var saves = new SaveService(storage, serializer);
```

独自実装でも、操作完了まで戻らない同期契約と、同じスロットの操作を利用側が直列化する契約を維持してください。
ネットワーク通信を伴うクラウド保存はこの同期境界へ直接入れず、ゲーム側の非同期処理が通信とローカル保存の同期を所有します。

## 制約

- `SaveService.CreateDefault` と `FileSaveStorage` は WebGL Player と tvOS Player に対応しません。該当 Player では別の `ISaveStorage` が必要です。
- 同じスロットへの並行操作には対応しません。
- `UnityJsonSaveSerializer` の保存ルートは、型の宣言に `[Serializable]` を付けた具象クラスまたは構造体です。プリミティブ、列挙型、文字列、配列、`List<T>` は、保存ルートではなく対応するフィールドを持つ型で包みます。`Dictionary<TKey, TValue>` と `UnityEngine.Object` は、次の制約に従って保存用の値へ変換します。
- 標準変換は public フィールドと `[SerializeField]` 付き private フィールドを保存し、プロパティを保存しません。入れ子の独自データ型にも型の宣言へ `[Serializable]` を付けます。
- 配列と `List<T>` はフィールドとして使用できますが、`Dictionary<TKey, TValue>` は対応しません。辞書はキーと値の一覧などへ変換します。
- `UnityEngine.Object` 参照は GUID や利用側で管理する永続 ID へ変換します。保存ルートの宣言型と実行時型が異なる値は、派生フィールドの欠落を防ぐため拒否されます。
- 標準変換は入れ子の全フィールドを事前検査しません。`object`、インターフェース、基底型で宣言して派生値を入れたフィールドなど、`JsonUtility` が完全に復元できない内部状態は、具体的な保存用データへ変換します。
- チェックサムは暗号機能ではありません。
- データ版の移行、保存するゲーム状態の収集、保存タイミングは利用側の責務です。

## サンプル

Package Manager から **Save System Basics** を Import し、`SaveSystemBasics.unity` を開いて Play します。
画面上のボタンまたはコンポーネントの Context Menu で、コイン数の変更、保存、読み込み、削除ができます。
Play 開始回数を自動保存するため、Play を止めて再開しても前回の状態が読み込まれることを確認できます。
