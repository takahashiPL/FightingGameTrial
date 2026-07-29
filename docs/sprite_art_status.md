# スプライト素材の状態

## 結論（現状）

**本番清書スプライトシート（固定セル仕様を満たした最終素材）は未完成である。**
一方、FightDebugScene 向けの**動作確認用シート**は PixelLab 生成素材を統合した正本として運用中である。

- 正本ファイル: `Game/Assets/Art/Characters/Fighter_SpriteSheet.png`（1536×1024）
- 正本 GUID: `dcb7851d129f2305be49fac973bf47b4`
- 旧シート GUID `2bb8ae7896cf21b43bcd9cf17bf228d2` は参照ゼロ確認後に削除済み（2026-07 一本化）
- Characters フォルダは正本 PNG + `.meta` の **1 組のみ**（`_New` 中間名・旧シート・単体 PNG なし）
- sub-sprite **31 枚**（Idle 8 / Walk 8 / Jump 8 / Punch 2 / Kick 5）
- 均等 8×5 固定セルではない。アクションごとに個別 Rect + 共通 Bottom Center Pivot

`art/fighter_motion_reference_sheet.png` および `docs/reference_infographic.png` は、白い道着・赤い鉢巻の格闘家を基準にした**デザイン／モーション方向性の参考画像**である（Unity 取込完成シートではない）。

## FightDebug 用シート（2026-07 正本）

| 項目 | 内容 |
|---|---|
| ファイル | `Fighter_SpriteSheet.png` + `.meta` |
| GUID | `dcb7851d129f2305be49fac973bf47b4` |
| 元素材 | PixelLab Export（116×116 連番 PNG）→ 1536×1024 シートへ配置 |
| Sprite Mode | Multiple |
| PPU / Mesh / Filter / Compression | **39** / Full Rect / Point / None |
| Physics Shape | Off |
| Rect | アクションごとに個別 Rect。体幹中心・足元基準で再スライス済み |
| Pivot | 全 sub-sprite **Bottom Center (0.5, 0)** |
| 共通 Rect サイズ | Idle 102×116 / Walk 96×115 / Jump 120×105 / Punch 110×113 / Kick 108×117 |

### sub-sprite 一覧（31）

| グループ | 枚数 | 名前 |
|---|---|---|
| Idle | 8 | `Fighter_Idle_00` … `_07` |
| Walk | 8 | `Fighter_Walk_00` … `_07` |
| Jump | 8 | `Fighter_Jump_00` … `_07` |
| Punch | 2 | `Fighter_Punch_00` / `_01` |
| Kick | 5 | `Fighter_Kick_00` … `_04` |

### Gameplay 接続（FightDebugScene）

| グループ | Scene Sequence | 状態 |
|---|---|---|
| Idle | 8 コマ | **接続済み・確認 OK** |
| Walk | 8 コマ（WalkF / WalkB 共有） | **接続済み・確認 OK** |
| Jump | Start=00 / Rise=01–02 / Apex=03 / Fall=04–05 / Landing=06–07 | **接続済み・確認 OK** |
| Punch | 2 コマ（Attack） | **接続済み・確認 OK**（Recovery 専用コマなし・暫定） |
| Kick | — | **素材のみ・Gameplay 未接続** |

詳細な実装接続は `docs/unity_implementation_status.md` §1.1。

## 素材ステージの現状

| ステージ | 現状 |
|---|---|
| 骨格 | 未整備（制作工程の正本は `production_spritesheet_spec.md`） |
| シルエット | 未整備 |
| 仮ドット絵 | FightDebug 正本シート（PixelLab 統合済み。本番清書ではない） |
| 動作確認用素材 | **31 sub-sprite 運用中** |
| 本番清書素材 | **未作成**（固定セル仕様適合・清書は未達） |
| 参考PNG | 参考のみ。完成扱いしない |

## 学習メモ（素材トラブルシュート）

- 画像寸法（例: 256×256）だけでは見かけサイズは揃わない。**PPU は旧・新 Alpha bbox から算出**（正本は 39）。Transform Scale の場当たり調整は行わない
- PixelLab Export は 116×116 連番だったが、正本シートは 1536×1024・8 列×5 行相当。想定フレーム数と実物が異なった（Idle/Walk/Jump 各 8、Punch 2、Kick 5）
- 192×192 共通 Rect + 共通 Custom Pivot だけで取り込むと、各フレーム内のキャラ位置ずれにより SpriteRenderer だけが左右・上下へ動いて見える
- **Rect 側で体幹中心（腰帯付近）を水平中央、接地点または最下端を Rect 下端へ揃える**方針を採用。フレームごとの個別 Pivot で帳尻を合わせない
- Alpha 全体重心だけでは手足・帯・髪に引っ張られる。体幹・腰帯を基準にする
- `framesPerSprite` は FPS ではなく、**1 枚を何 CombatFrame 表示するか**
- JumpRise / JumpFall は `loop=false` + `holdLastFrame=true` で空中姿勢の往復を防止
- Punch は素材 2 枚のみ（Recovery 専用コマなし）。Kick は原画上 knee-kick 寄りの見え方を含むが、今回は原画修正ではなく切り出し整理のみ
- 旧・新シート並存のままコミットせず、参照検索後に新 GUID を維持して正本名へ一本化した
- 旧素材は新参照確認前に削除しない

## 制作方針（要約）

専任デザイナー不在のため、ChatGPT / PixelLab 支援の段階制作を正とする。詳細は `docs/production_spritesheet_spec.md`。

仮素材のまま判定実装を進めてよい。見た目完成待ちで止めない。

## 今後の改善候補

- Kick Gameplay 実装（Sequence 接続）
- Punch 3 枚以上（Recovery 含む）への素材改善
- 必要なら攻撃・歩行素材の再制作

## 実装との関係

- ゲーム挙動の正本は `docs/rules.md`
- Unity 到達点は `docs/unity_implementation_status.md`
- 切り出し・工程の長期正本は `docs/production_spritesheet_spec.md`（固定セル方針）。FightDebug の個別 Rect 運用は学習・検証用の現行方式

## 削除した旧ファイル

- v2 簡易図形: `art/fighter_spritesheet_v1.png` 等（実装素材として使用しない）
- デバッグ単体 PNG（2026-07 削除）: `fighter_idle_00_transparent` / `fighter_attack_punch_transparent` / `Fighter_Walk_*` / `Fighter_Jump*` / `Fighter_Landing`（PNG+meta セット。Scene 参照ゼロ確認後）
- 旧 `Fighter_SpriteSheet.png`（GUID `2bb8ae7896cf21b43bcd9cf17bf228d2`、2026-07 一本化時。参照ゼロ確認後）
