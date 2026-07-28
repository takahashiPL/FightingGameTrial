# スプライト素材の状態

## 結論（現状）

**本番清書スプライトシート（固定セル仕様を満たした最終素材）は未完成である。**
一方、FightDebugScene 向けの**動作確認用シート**は導入済みである。

- 正本ファイル: `Game/Assets/Art/Characters/Fighter_SpriteSheet.png`（1024×1536）
- 旧単体 PNG（Idle/Punch、Walk/Jump 中間素材）は削除済み。Characters フォルダはシートのみ
- 均等 3×3 セル分割ではない。Alpha 連結成分から 9 個別 Sprite Rect
- Walk 2 コマ切替は動作する。**歩行の見た目品質は暫定**（自然な歩行素材は未完了）

`art/fighter_motion_reference_sheet.png` および `docs/reference_infographic.png` は、白い道着・赤い鉢巻の格闘家を基準にした**デザイン／モーション方向性の参考画像**である（Unity 取込完成シートではない）。

## FightDebug 用シート（2026-07）

| 項目 | 内容 |
|---|---|
| ファイル | `Fighter_SpriteSheet.png` + `.meta` |
| Sprite Mode | Multiple |
| PPU / Mesh / Filter / Compression | 100 / Full Rect / Point / None |
| Physics Shape | Off |
| Rect | ポーズごとに幅・高さが異なってよい。セル統一より同一キャラ内の基準位置・縮尺安定を優先 |
| Pivot | 地上 Bottom Center。空中は足元基準 Custom |
| sub-sprite | Idle / Punch / Walk_00 / Walk_01 / JumpStart / JumpRise / JumpApex / JumpFall / Landing |

詳細な実装接続は `docs/unity_implementation_status.md` §1.1。

## 素材ステージの現状

| ステージ | 現状 |
|---|---|
| 骨格 | 未整備（制作工程の正本は `production_spritesheet_spec.md`） |
| シルエット | 未整備 |
| 仮ドット絵 | FightDebug シートに相当する動作確認用（本番清書ではない） |
| 動作確認用素材 | **シート運用中**（9 ポーズ）。Walk 見た目は暫定 |
| 本番清書素材 | **未作成**（固定セル仕様適合・清書は未達） |
| 参考PNG | 参考のみ。完成扱いしない |

## 学習メモ（素材トラブルシュート）

- 画像寸法（例: 256×256）だけでは見かけサイズは揃わない
- 単体画像を別々に生成すると、頭身・線・配色・体格・余白が不統一になりやすい
- 1 枚のシートにまとめると比較・統一はしやすいが、**シート化だけで自然なアニメになるわけではない**
- Walk で同じ脚が前に出る失敗が複数回あった。脚の左右入れ替えだけでは、腰・重心・接地脚・腕振り・頭部上下が弱いと「へこへこ」に見える
- 均等 3×3 分割を必須と誤認したが、個別 Rect 運用で問題なく成立した
- 自動スライスが不安定な場合、Alpha 連結成分から Rect を再構築できる
- 旧素材は新参照確認前に削除しない

## 制作方針（要約）

専任デザイナー不在のため、ChatGPT 支援の段階制作を正とする。詳細は `docs/production_spritesheet_spec.md`。

仮素材のまま判定実装を進めてよい。見た目完成待ちで止めない。

## 実装との関係

- ゲーム挙動の正本は `docs/rules.md`
- Unity 到達点は `docs/unity_implementation_status.md`
- 切り出し・工程の長期正本は `docs/production_spritesheet_spec.md`（固定セル方針）。FightDebug の個別 Rect 運用は学習・検証用の現行方式

## 削除した旧ファイル

- v2 簡易図形: `art/fighter_spritesheet_v1.png` 等（実装素材として使用しない）
- デバッグ単体 PNG（2026-07 削除）: `fighter_idle_00_transparent` / `fighter_attack_punch_transparent` / `Fighter_Walk_*` / `Fighter_Jump*` / `Fighter_Landing`（PNG+meta セット。Scene 参照ゼロ確認後）
