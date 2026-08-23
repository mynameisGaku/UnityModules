# Changelog

## [1.0.0] - 2026-08-23

- Play Mode 中に選択項目だけを手動で取り込む Editor 専用 workflow を追加。
- 通常の Domain Reload と Disable Domain Reload をまたぐ SessionState 保持を追加。
- GlobalObjectId、Scene、MonoScript、型、property を組み合わせた厳密な identity 確認を追加。
- 文字列を含む最上位の値 allow-list、件数・文字列・payload 上限を追加。
- 単回使用 Preview、反映直前の古さ確認、反映後確認、Scene の明示的な変更済み化を追加。
- exact payload 由来の round-trip 表示と、解決済み target 名による Preview を追加。
- 未選択項目の副作用検出と、選択項目の復元・復元後確認を追加。
- ①から⑤まで上から進める専用 Window と、P0 の確認結果を固定する EditMode tests を追加。
