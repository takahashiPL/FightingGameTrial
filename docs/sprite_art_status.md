# スプライト素材の状態

## 結論（現状）

2026-07-31、PixelLab由来の再構築版をFightDebugの正式`Fighter_SpriteSheet.png`として採用した。現行正本は41 Sprite、GUID `42345be00e0994144ba94bc5f1362757`、PPU 39、Point、Compression None、Mip Map Off、実画素Trim Rect＋Center Pivotである。2026-08-05、J Punch用パンチ3コマを正式シートへ反映し、`FighterRebuilt_Punch_00`〜`02`のSprite Rectを新素材に合わせて更新した（`3b92914`・`origin/unity` push済み。GUID・Sprite名・internalID維持）。

192×192固定セル＋Center Pivotの比較版は、透明余白を含むセル中心がTransform基準となり、キャラクターが旧素材より大きく上へずれたため不採用とした。PNG内のセル構成は192×192だが、Unity Sprite Rectは各セル内の実画素範囲へTrimする。

**本番清書スプライトシート（固定セル仕様を満たした最終素材）は未完成である。**
一方、FightDebugScene 向けの**動作確認用シート**は PixelLab 生成素材を統合した正本として運用中である。

- 正本ファイル: `Game/Assets/Art/Characters/Fighter_SpriteSheet.png`（1728×1152）
- 正本 GUID: `42345be00e0994144ba94bc5f1362757`
- 旧シート GUID `2bb8ae7896cf21b43bcd9cf17bf228d2` は参照ゼロ確認後に削除済み（2026-07 一本化）
- Characters フォルダは正本 PNG + `.meta` の **1 組のみ**（`_New` 中間名・旧シート・単体 PNG なし）
- sub-sprite **41 枚**（Idle 8 / Walk 8 / Jump 9 / Punch 3 / Kick 7 / Air Kick 6）。詳細は次節
- 均等 8×5 固定セルではない。アクションごとに個別 Rect + Center Pivot（`0.5, 0.5`。Punch Rect は2026-08-05更新）

`art/fighter_motion_reference_sheet.png` および `docs/reference_infographic.png` は、白い道着・赤い鉢巻の格闘家を基準にした**デザイン／モーション方向性の参考画像**である（Unity 取込完成シートではない）。

## FightDebug 用シート（2026-07-31 正式正本）

- Asset: `Game/Assets/Art/Characters/Fighter_SpriteSheet.png`
- GUID: `42345be00e0994144ba94bc5f1362757`
- 41 Sprite: Idle 8 / Walk 8 / Jump 9 / Punch 3 / Kick 7 / Air Kick 6
- Sub-Asset名は参照安全性のため`FighterRebuilt_...`を意図的に維持
- P1/P2の初期SpriteとIdle／Walk／Jump／Punch／Ground Kick／Air Kickを接続済み
- 旧GUID `dcb7851d129f2305be49fac973bf47b4`は参照0件確認後に削除。旧PNG/metaはAssets外バックアップに保存

| 項目 | 内容 |
|---|---|
| ファイル | `Fighter_SpriteSheet.png` + `.meta` |
| GUID | `42345be00e0994144ba94bc5f1362757` |
| 元素材 | PixelLab Export（116×116 連番 PNG）→ 現行正本シート 1728×1152 へ配置 |
| Sprite Mode | Multiple |
| PPU / Mesh / Filter / Compression | **39** / Full Rect / Point / None |
| Physics Shape | Off |
| Rect | アクションごとに個別 Rect。体幹中心・足元基準で再スライス済み（個別 Pivot で帳尻を合わせない） |
| Pivot | 全 sub-sprite **Center (0.5, 0.5)** |
| 共通 Rect サイズ | Idle 102×116 / Walk 96×115 / Jump 120×105 / Kick 108×117。Punch は個別（`82×120` / `100×120` / `100×120`。2026-08-05更新） |

### sub-sprite 一覧

| グループ | 枚数 | 名前 |
|---|---|---|
| Idle | 8 | `FighterRebuilt_Idle_00` … `_07` |
| Walk | 8 | `FighterRebuilt_Walk_00` … `_07` |
| Jump | 9 | `FighterRebuilt_Jump_00` … `_08` |
| Punch | 3 | `FighterRebuilt_Punch_00` … `_02` |
| Kick | 7 | `FighterRebuilt_Kick_00` … `_06` |
| Air Kick | 6 | `FighterRebuilt_AirKick_00` … `_05` |

合計 8+8+9+3+7+6 = **41**。

### Gameplay 接続（FightDebugScene）

| グループ | Scene Sequence | 状態 |
|---|---|---|
| Idle | 8 コマ | **接続済み・確認 OK** |
| Walk | 8 コマ（WalkF / WalkB 共有） | **接続済み・確認 OK** |
| Jump | Start=00 / Rise=01–02 / Apex=03 / Fall=04–05 / Landing=06–07 | **接続済み・確認 OK** |
| Punch | 3 コマ（`FighterRebuilt_Punch_00`〜`02`） | **接続済み・確認 OK**（2026-08-05 素材／Rect更新済み） |
| Kick | `FighterRebuilt_Kick_00`〜`04`（シート上は `_06` まで7枚） | **Ground Kickへ接続済み** |
| Air Kick | `FighterRebuilt_AirKick_00`〜`05` | **専用Sequence接続済み・表示確認済み** |

詳細な実装接続は `docs/unity_implementation_status.md` §1.1。

## 素材ステージの現状

| ステージ | 現状 |
|---|---|
| 骨格 | 未整備（制作工程の正本は `production_spritesheet_spec.md`） |
| シルエット | 未整備 |
| 仮ドット絵 | FightDebug 正本シート（PixelLab 統合済み。本番清書ではない） |
| FightDebug正式素材 | **41 sub-sprite 運用中** |
| 本番清書素材 | **未作成**（固定セル仕様適合・清書は未達） |
| 参考PNG | 参考のみ。完成扱いしない |

## 学習メモ（素材トラブルシュート）

- 画像寸法（例: 256×256）だけでは見かけサイズは揃わない。**PPU は旧・新 Alpha bbox から算出**（正本は 39）。Transform Scale の場当たり調整は行わない
- PixelLab Export は 116×116 連番だったが、**当時**の統合シートは 1536×1024・8 列×5 行相当。想定フレーム数と実物が異なった（Idle/Walk/Jump 各 8、Punch 2、Kick 5）。**現行正本は 1728×1152・41 Sprite**
- 192×192 共通 Rect + 共通 Custom Pivot だけで取り込むと、各フレーム内のキャラ位置ずれにより SpriteRenderer だけが左右・上下へ動いて見える
- **Rect 側で体幹中心（腰帯付近）を水平中央、接地点または最下端を Rect 下端へ揃える**方針を採用。Pivot は全コマ Center `(0.5, 0.5)` とし、フレームごとの個別 Pivot で帳尻を合わせない
- Alpha 全体重心だけでは手足・帯・髪に引っ張られる。体幹・腰帯を基準にする
- `framesPerSprite` は FPS ではなく、**1 枚を何 CombatFrame 表示するか**
- JumpRise / JumpFall は `loop=false` + `holdLastFrame=true` で空中姿勢の往復を防止
- Punch は正式シートへ3コマ反映済み（`FighterRebuilt_Punch_00`〜`02`。2026-08-05 Rect更新）。ゲームルール／J Punchフレーム値は変更なし
- Kick は原画上knee-kick寄りで、脚の伸び・シルエット不足がGround Kickの見た目改善を制限している可能性がある。コード不具合と断定せず再制作候補とする
- Air Kick専用Sprite 6枚は作成・接続・表示確認済み。Ground Kick流用は終了した
- 旧・新シート並存のままコミットせず、参照検索後に新 GUID を維持して正本名へ一本化した
- 旧素材は新参照確認前に削除しない

## 制作方針（要約）

専任デザイナー不在のため、ChatGPT / PixelLab 支援の段階制作を正とする。詳細は `docs/production_spritesheet_spec.md`。

仮素材のまま判定実装を進めてよい。見た目完成待ちで止めない。

## 今後の改善候補

- Ground Kickをもっと脚が伸びるシルエットへ再制作し、その後にフレーム／Hit Boxを再調整
- 現行Air Kick専用素材に合わせたフレーム値・Hit Box・使用Sprite範囲の再調整
- 必要なら攻撃・歩行素材の再制作

## 実装との関係

- ゲーム挙動の正本は `docs/rules.md`
- Unity 到達点は `docs/unity_implementation_status.md`
- 切り出し・工程の長期正本は `docs/production_spritesheet_spec.md`（固定セル方針）。FightDebug の個別 Rect 運用は学習・検証用の現行方式

## 削除した旧ファイル

- v2 簡易図形: `art/fighter_spritesheet_v1.png` 等（実装素材として使用しない）
- デバッグ単体 PNG（2026-07 削除）: `fighter_idle_00_transparent` / `fighter_attack_punch_transparent` / `Fighter_Walk_*` / `Fighter_Jump*` / `Fighter_Landing`（PNG+meta セット。Scene 参照ゼロ確認後）
- 旧 `Fighter_SpriteSheet.png`（GUID `2bb8ae7896cf21b43bcd9cf17bf228d2`、2026-07 一本化時。参照ゼロ確認後）

## Ground Kick 接続（2026-07-30）

- P1/P2 `DebugFighterVisual.kickSequence` に5枚を同順で接続
- 3 CombatFrame/枚、Loop OFF、Hold Last Frame ON
- 現行暫定値ではGround Kick ActiveはAF10〜13。AF14〜19は振り切り表示だけを残し、Hit Boxは無効。AF20〜26はIdle表示
- 本番清書ではなく、現素材を使った学習・判定確認用の暫定品質

## J Punch パンチ3コマ素材反映（`3b92914`・push済み）

- 2026-08-05、`unity` / `origin/unity` へ push 済み
- J Punch用パンチ3コマを正式`Fighter_SpriteSheet.png`へ反映
- `FighterRebuilt_Punch_00`〜`02`のSprite Rectを新素材に合わせて更新
- Rect: Punch_00 `x=46, y=422, w=82, h=120`／Punch_01 `x=224, y=422, w=100, h=120`／Punch_02 `x=418, y=422, w=100, h=120`
- 正式GUID `42345be00e0994144ba94bc5f1362757`・Sprite名・internalIDは維持（参照差し替えなし）
- Editor確認: 通常表示、P1/P2、左右反転、画像切れなし
- ゲームルール／J Punchフレーム値／空中パンチ仕様は変更なし（素材とSprite Rectの改善）

## Air Kick専用素材接続（`5bcc3fa`・push済み）

- `FighterRebuilt_AirKick_00`〜`05`の専用6枚を`DebugFighterVisual.airKickSequence`へ接続済み。素材作成・接続は完了
- Ground Kickの`kickSequence`とは分離済み。専用Air Kick表示をユーザー操作で確認済み（P2 AirKick Assist／Air双方未適用検証とは別。詳細は`unity_implementation_status.md`）
- 6枚、1 CombatFrame/枚、Loop OFF、Hold Last Frame ON。Startup＋Active 10Fのうち6F表示後は最終コマを保持し、RecoveryでJumpFallへ戻る（VisualがJumpFallでも内部Air Kick Recoveryは継続）
- 現行は暫定: S/A/R `5/5/10`、Hit Box。`5/5/10`調整後、立ち相手へ以前より当てやすいことをPlay確認
- Air Hit／Air Knockbackは未実装
- 将来候補: 現行専用素材に合わせたフレーム値・Hit Box・使用Sprite範囲の再調整

## Ground Clash表示（2026-07-30）

Ground Clashの同技（JPunch同士／Ground Kick同士）はEditor確認済み。コミット`78c4e94`で通常Hitから専用`ClashRecoil`状態へ分離し、黄色系の専用色、Damage 0、通常HitCount非加算を確認した。専用Sprite Sequenceは未設定でIdleへfallbackする。今後はClashRecoil専用Spriteや短い白／黄フラッシュなど、演出品質の改善が候補。

現行コードでは地上攻撃同士なら技種不問でGround Clash対象。異技 **P1 GroundKick → P2 JPunch** は鏡写しSwap＋Delay5でPlay実測済み（Damage 0）。**P1 JPunch → P2 GroundKick** も単発 Assist（距離1.50）で同一CF ClashをPlay実測済み。
