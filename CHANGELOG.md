# CHANGELOG

## v3.1（資料・CSV整合）

- スプライト配置の正本を `data/sprite_frame_requirements.csv` に一本化
- `art/spritesheet_layout.csv` と `data/frames.csv` の `sheet_x` / `sheet_y` を正本へ同期
- `frames.csv` / `boxes.csv` を Idle + StandPunch のみの初期検証用サンプルへ整理
- `implementation_scope`（initial / planned）と `data_scope` / `value_status` で完成状態を明示
- `schemas/data_dictionary.md` に `hit_resolution` / `state_transitions` / `sprite_frame_requirements` の列説明を追加
- `debug_ui_fields.csv` と `docs/debug_screen_spec.md` を初期必須 / 将来候補で整合
- JustGuardWindow・Damage・硬直・Pushback・Jump移動量を仮値として明記
- 空中パンチのガード仕様を未確定として明記（moves.csv のフラグは暫定値）
- README を現状に更新（Game/ は Universal 2D 作成済み、unity `1f294cc`、Git管理範囲の訂正）

## v3

- 参考画像と大きく異なる簡易図形スプライト2枚を削除
- 白い道着・赤い鉢巻の格闘家を基準にした `fighter_motion_reference_sheet.png` を追加
- 参考画像と本番実装素材の違いを `sprite_art_status.md` で明記
- 本番スプライトの制作条件を `production_spritesheet_spec.md` に追加
- 必須フレーム一覧 `sprite_frame_requirements.csv` を追加
- READMEを全面改訂し、実装素材の完成状態について誤解が生じないよう修正
- Gameフォルダ内の仮ファイルを削除し、空フォルダとして扱う構成へ変更
  - 注: その後 unity ブランチ `1f294cc` で Universal 2D プロジェクトを追加済み

## v2

- Unity選定理由をREADMEへ追加
- デバッグ画面ワイヤーフレーム、モックアップ、仕様書、UI項目CSVを追加

## v1

- 初回実装テンプレート
