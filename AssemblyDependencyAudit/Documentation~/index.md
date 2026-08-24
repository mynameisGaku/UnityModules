# Assembly依存チェック 1.0.0

## 目的

`Assets`と導入済み`Packages`のAssembly Definition（`.asmdef`）を読み取り、参照関係と構成上の問題を1つのEditorWindowで確認します。read-onlyの検査に限定し、Project fileは変更しません。

## 操作順

1. `Tools > Assembly Dependency Audit > Open`を開きます。
2. `Refresh`を押します。
3. 中央の`Assemblies`列から対象を選びます。
4. 左の`Referenced By`で直接の参照元を確認します。
5. 右の`Depends On`で直接の参照先を確認します。
6. 検索と問題filterで対象を絞り、asset pathと問題内容を確認します。

## 3列view

| 列 | 表示内容 |
| --- | --- |
| Referenced By | 選択Assemblyを直接参照しているAssembly |
| Assemblies | `Assets`と`Packages`で見つかったAssembly |
| Depends On | 選択Assemblyが直接参照しているAssembly |

名前参照と`GUID:`参照は、Unityが解決できるAssembly Definitionのpathへ対応付けます。解決先がない参照や、同名候補が複数あって決定できない参照はgraphへ推測で追加せず、問題として表示します。

## 報告する問題

- 循環参照
- 自己参照
- Playerで使えるAssemblyからEditor専用Assemblyへの参照
- 不正なJSON
- 空のAssembly名
- 重複したAssembly名
- 重複したGUID
- 解決できない参照
- 同名候補による曖昧な参照
- 名前参照と`GUID:`参照の混在
- `includePlatforms`と`excludePlatforms`の同時指定

循環検出はgraph全体を調べます。3列viewは選択Assemblyの直接参照だけを表示するため、循環の全経路は問題一覧に含まれるAssemblyを順に選んで確認してください。

## 変更されないもの

`.asmdef`、`.asmref`、C# source、Scene、Prefab、Project Settings、Package manifestは変更されません。Assetのimport、script compile、Player buildも自動実行しません。

## 検査の上限

このツールが判断するのは`.asmdef`に明示された参照とplatform設定です。C# sourceが実際に参照先の型を使用しているか、参照を削除してもcompileできるか、compile時間へどの程度影響するかは判断しません。`.asmref`、precompiled plugin、型単位とAsset単位の依存も検査対象外です。

## 公開APIと依存

公開C# APIとRuntime assemblyはありません。外部Packageへの依存もありません。
