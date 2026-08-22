# Module Installer 1.2.0

## 目的

公開済みUnityModulesを、固定tagのGit URLとして個別または用途別bundleで追加するEditor toolです。利用者がpackage名とURLを暗記する必要をなくし、互換packageを壊さずに新しい推奨入口を提供します。

## 操作

1. `Tools > Module Installer > Open`を開きます。
2. bundle cardに表示されるmodule名を確認します。
3. module名と追加件数を確認し、`Install N`と表示されたbuttonを押します。
4. Package Managerの解決とdomain reloadを待ちます。

個別導入は`Advanced: install one module`から実行します。

Project Maintenanceの「プロジェクト初期設定」はv1.1.0へ固定され、Project SettingsとTag・Layer・Sorting Layerを同じprofileから適用できます。

## 安全条件

- catalogに無いpackage名は受理しません。
- URLはrepository、subfolder、公開tagを固定し、任意入力を受けません。
- 導入済みpackageは追加対象から除外します。
- `Assets/Modules/<Folder>`が存在するmoduleは、assembly重複を避けるため導入を停止します。
- 1回の操作は1つの`Client.AddAndRemove`要求として実行し、package削除は要求しません。
- 失敗時はqueueを終了し、同じ要求を無限に再試行しません。
- bundleと個別packageのbuttonは未導入件数、導入済み、Assets copy競合を表示します。

## 状態復元

導入対象は`SessionState`へ保存します。package追加中のdomain reload後、全対象が登録済みなら完了としてqueueを消去します。未導入対象が残る場合は、同じ固定URL集合でPackage Manager要求を再開できます。

Unity Editor自体を終了すると`SessionState`は保証されません。再起動後はwindowを開き直し、未導入分を再選択してください。

## 非対象

- package更新、downgrade、削除
- 任意Git URLやregistryの入力
- `Assets/Modules` copyの自動移動・削除
- package間のAPI統合や型名変更
- custom bundleの保存

## 検証

- catalogのpackage名、folder名、tag、bundle参照の一意性
- installed packageの除外、選択重複の除外、入力順の保持
- Assets copy競合とunknown packageのmutation前停止
- 複数URLを1要求へまとめること
- 成功・失敗・domain reload相当のqueue復元
- Editor windowの6 bundle cardと40個の個別導入行
