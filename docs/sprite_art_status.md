# スプライト素材の状態

## 結論

`art/fighter_motion_reference_sheet.png` は、`docs/reference_infographic.png` に登場する白い道着・赤い鉢巻の格闘家を基準にした**モーション方向性の参考画像**です。

これは次の用途に使います。

- キャラクターデザインの統一
- 必要なポーズの検討
- モーションの見た目比較
- 本番スプライト制作時の参照

ただし、現時点では次の理由により、Unityへそのまま取り込む完成スプライトシートではありません。

- セル寸法が128×128へ厳密に揃っていない
- 透明背景ではない
- 足元原点が完全には統一されていない
- 必要フレームIDとの1対1対応が未確定
- 一部ポーズは参考用で、実装仕様と厳密に一致しない

**本番スプライトPNGは未完成**（全フレーム `asset_status=required`）。

## 配置の正本

セル配置の正本は次の1ファイル。

- `data/sprite_frame_requirements.csv`

これに同期する。

- `art/spritesheet_layout.csv`（展開表）
- `data/frames.csv` の `sheet_x` / `sheet_y`（初期サンプル行のみ）

## 実装用として使用するもの

実装時の切り出し仕様は、次を正本とします。

- `data/sprite_frame_requirements.csv`（配置正本）
- `art/spritesheet_grid_template.png`
- `art/spritesheet_layout.csv`（正本の展開）
- `data/frames.csv`（初期検証用サンプル。Idle + StandPunch のみ）
- `docs/production_spritesheet_spec.md`

これらに従い、右向き・透明背景・固定セル・統一ピボットで本番スプライトを作成します。  
初期実装ではプレースホルダ矩形でよい。

## 実装スコープ

| scope | 意味 |
|---|---|
| `initial` | 最初の実装対象（Idle / StandPunch） |
| `planned` | 配置予約・将来実装。実装済みではない |

## 削除した旧ファイル

v2に含まれていた次のファイルは、参考画像と大きく異なる簡易図形だったため削除しました。

- `art/fighter_spritesheet_v1.png`
- `art/fighter_spritesheet_v1_boxes_preview.png`

これらは実装素材として使用しないでください。
