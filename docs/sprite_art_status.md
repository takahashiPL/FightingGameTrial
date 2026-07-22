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

## 実装用として使用するもの

実装時の切り出し仕様は、次を正本とします。

- `art/spritesheet_grid_template.png`
- `art/spritesheet_layout.csv`
- `data/frames.csv`
- `data/sprite_frame_requirements.csv`
- `docs/production_spritesheet_spec.md`

これらに従い、右向き・透明背景・固定セル・統一ピボットで本番スプライトを作成します。

## 削除した旧ファイル

v2に含まれていた次のファイルは、参考画像と大きく異なる簡易図形だったため削除しました。

- `art/fighter_spritesheet_v1.png`
- `art/fighter_spritesheet_v1_boxes_preview.png`

これらは実装素材として使用しないでください。
