# スプライト素材の状態

## 結論（現状）

**本番スプライトシートは未完成である。**  
完成した本番用スプライトシートはリポジトリに存在しない。

`art/fighter_motion_reference_sheet.png` および `docs/reference_infographic.png` は、白い道着・赤い鉢巻の格闘家を基準にした**デザイン／モーション方向性の参考画像**である。

用途:

- キャラクターデザインの統一
- 必要なポーズの検討
- モーションの見た目比較
- 本番制作時の参照

Unity へそのまま取り込む完成スプライトシートではない。理由の例:

- セル寸法が 128×128 へ厳密に揃っていない
- 透明背景ではない
- 足元原点が完全には統一されていない
- 必要フレームIDとの1対1対応が未確定
- 一部ポーズは参考用で、実装仕様と厳密に一致しない

全フレーム `asset_status=required`（本番素材未作成）。

### デバッグ用透過試験PNG（本番ではない）

FightDebugScene 確認用に、次の**透過試験素材**が `Game/Assets/Art/Characters/` にある（2026-07 時点）。

- `fighter_idle_00_transparent.png`
- `fighter_attack_punch_transparent.png`

これらは段階7〜9の表示・攻撃確認用であり、**本番清書スプライトシートではない**。  
配置正本・全ポーズ網羅・セル規格適合は未達として扱う。

## 素材ステージの現状

| ステージ | 現状 |
|---|---|
| 骨格 | 未整備（制作工程の正本は `production_spritesheet_spec.md`） |
| シルエット | 未整備 |
| 仮ドット絵 | 未整備 |
| 動作確認用素材 | デバッグ用透過PNGあり（Idle/Punch）。本番シートではない |
| 本番清書素材 | **未作成** |
| 参考PNG | 参考のみ。完成扱いしない |

## 制作方針（要約）

専任デザイナー不在のため、ChatGPT 支援の段階制作を正とする。

1. 骨格 → シルエット → 仮ドット絵 → 前後比較 → Unity連続再生 → 修正 → 清書  
2. いきなり完成ドット絵をフレーム単位で一発生成しない  
3. 最初の試験は Idle と StandPunch のみ  
4. 詳細正本は `docs/production_spritesheet_spec.md`

ChatGPT 生成物は無条件に完成素材としない。人間が採用可否・連続再生・判定対応を確認する。

## 配置の正本

- `data/sprite_frame_requirements.csv`（セル配置の正本）
- `art/spritesheet_layout.csv`（展開表）
- `data/frames.csv` の `sheet_x` / `sheet_y`（初期サンプル行のみ）

## 実装との関係

- ゲーム挙動の正本は `docs/rules.md`
- Unity 到達点・次工程は `docs/unity_implementation_status.md`
- **仮素材のまま判定実装を進めてよい**。見た目完成待ちで止めない
- 切り出し仕様・工程は `docs/production_spritesheet_spec.md`

| scope | 意味 |
|---|---|
| `initial` | 最初の対象（Idle / StandPunch） |
| `planned` | 配置予約。実装済み・素材完成ではない |

## 削除した旧ファイル

v2 の簡易図形スプライトは削除済み。実装素材として使用しない。

- `art/fighter_spritesheet_v1.png`
- `art/fighter_spritesheet_v1_boxes_preview.png`
