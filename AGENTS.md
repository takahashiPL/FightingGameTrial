# Unity_FightingGameTrial Codex Rules

## Source folders
- Unity_FightingGameTrial: 変更対象
- UNITY_SharedDocs: 参照専用

## 最初に確認する資料
- README.md
- docs/rules.md
- docs/unity_implementation_status.md
- 必要に応じて UNITY_SharedDocs

## 禁止
- UNITY_SharedDocsを変更しない
- 明示指示なしにgit add、commit、pushしない
- 明示指示なしにScene、Prefab、XLSXを変更しない
- Unity_ScrapCleanerTrialやUE資料を混在させない
- 既存仕様を推測だけで上書きしない

## コード方針
- 実行効率や短さより、処理順と責務が追いやすい可読性を優先する
- 学習用として、何をするか・なぜ必要か・処理順が分かるコメントを付ける
- 変更前に関連ファイルと影響範囲を確認する

## XLSX方針
- 進行管理XLSXはリポジトリ外で管理する
- Codexは原則として参照・整合確認のみ行う
- 明示指示なしにXLSXを作成・更新・移動・Git追加しない

## 作業後の報告
- 変更ファイル一覧
- ファイルごとの変更内容
- 設計上の判断
- 残っている懸念
- git diff --check
- git diff --stat
- git status --short
