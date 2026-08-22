# Module Manager 1.3.4

## 目的

公開済みUnityModulesを、固定tagのGit URLとして個別または用途別bundleで追加し、導入済みの古いversionをまとめて更新するEditor toolです。利用者がpackage名、URL、最新公開versionを暗記する必要をなくします。

## 操作

1. `Tools > Module Manager > Open`を開きます。
2. bundle cardに表示されるmodule名を確認します。
3. module名と追加件数を確認し、`Install N`と表示されたbuttonを押します。
4. Package Managerの解決とdomain reloadを待ちます。

個別導入は`Advanced: install one module`から実行します。

導入済みmoduleに更新がある場合は、上部に`Module Name -> target version`が表示されます。対象を確認して`Update N`を押すと、古いversionだけを1回のPackage Manager要求で更新します。

最初に40件の個別一覧を読む必要はありません。新規ProjectのC#生成既定値、条件付きコンパイル記号、Asset整理は`Project Maintenance`、Scene切り替えやUIは`Scene and UI`、save・音声・reportは`Game Services`、入力補助は`Input Support`から確認します。個別一覧は必要なmoduleが明確な場合や既存projectとの互換用です。

Project Maintenanceの「プロジェクト一括設定」はv1.5.0へ固定されています。C# Root Namespace、新規scriptの改行方式、条件付きコンパイル記号、Tag・Layer・Sorting Layerに加え、Player buildへ含めるSceneとEditorでPlayを押したときだけ使う開始Sceneを、独立した項目として同じprofileから適用・復元できます。Root Namespaceと改行方式は今後生成するC# fileへ適用され、既存sourceは変更しません。条件付きコンパイル記号は既存一覧を維持して不足分だけを追加します。

## 安全条件

- catalogに無いpackage名は受理しません。
- URLはrepository、subfolder、公開tagを固定し、任意入力を受けません。
- 導入済みpackageは追加対象から除外します。
- 更新対象は、導入済みversionを数値比較でき、catalogのversionより古いpackageだけです。新しいversionや独自versionは上書きしません。
- `Assets/Modules/<Folder>`が存在するmoduleは、assembly重複を避けるため導入を停止します。
- 1回の操作は1つの`Client.AddAndRemove`要求として実行し、package削除は要求しません。
- 失敗時はqueueを終了し、同じ要求を無限に再試行しません。
- bundleと個別packageのbuttonは未導入件数、導入済み、Assets copy競合を表示します。

## 状態復元

導入・更新対象は`SessionState`へ保存します。domain reload後、全対象が目的versionへ到達していれば完了としてqueueを消去します。対象が残る場合は、同じ固定URL集合でPackage Manager要求を再開できます。

Unity Editor自体を終了すると`SessionState`は保証されません。再起動後はwindowを開き直し、未導入分を再選択してください。

## 非対象

- packageのdowngrade、削除
- 任意Git URLやregistryの入力
- `Assets/Modules` copyの自動移動・削除
- package間のAPI統合や型名変更
- custom bundleの保存

## 検証

- catalogのpackage名、folder名、tag、bundle参照の一意性
- installed packageの除外、選択重複の除外、入力順の保持
- Assets copy競合とunknown packageのmutation前停止
- 複数URLを1要求へまとめること
- 古いversionだけを更新し、新しいversionや独自versionを変更しないこと
- 成功・失敗・domain reload相当のqueue復元
- Editor windowの更新一覧、6 bundle card、40個の個別導入行
