# CSVデータ辞書

## frames.csv

| 列 | 意味 |
|---|---|
| frame_id | 一意の表示フレームID |
| move_id | 所属する技・アクション |
| frame_index | アクション内番号 |
| sheet_x / sheet_y | スプライトシート左上ピクセル |
| width / height | 切り出しサイズ |
| pivot_x / pivot_y | セル左上基準の原点 |
| duration_game_frames | この絵を維持するゲームフレーム数 |
| move_x / move_y | 1ゲームフレーム単位の移動量 |
| can_accept_input | 入力受付可否 |
| can_guard | ガード移行可否 |
| invulnerable | 無敵か |
| airborne | 空中か |

## boxes.csv

1ボックスを1行で定義する。

| 列 | 意味 |
|---|---|
| box_type | push / hurt / hit |
| offset_x / offset_y | キャラクター原点からの相対位置 |
| width / height | ボックスサイズ |
| enabled | 有効可否 |
| hit_group_id | 多段技用。初期版は1 |
| notes | 高さや用途 |

## moves.csv

| 列 | 意味 |
|---|---|
| startup_frames | 発生前 |
| active_frames | Hitbox有効 |
| recovery_frames | 攻撃後硬直 |
| attack_category | Punch / Kick |
| on_hit_result | HitStun / Knockdown / AirHitStun等 |
| can_stand_guard | 立ちガード可能 |
| can_crouch_guard | しゃがみガード可能 |
| can_hit_crouch | しゃがみHurtboxに届く |
| can_hit_air | 空中Hurtboxに届く |
| guard_pushback_px | 防御側後退 |
| attacker_recoil_px | 攻撃側反動 |
| clash_enabled | Hitbox同士のClash対象 |
| air_action_once_per_jump | 1ジャンプ1回制限 |

## UV変換

CSVはピクセル矩形を正本とする。

- `u0 = sheet_x / texture_width`
- `v0 = sheet_y / texture_height`
- `u1 = (sheet_x + width) / texture_width`
- `v1 = (sheet_y + height) / texture_height`

使用する描画APIのUV原点が左下の場合はV座標を反転する。

## debug_ui_fields.csv

デバッグ画面に表示する項目一覧。

| 列 | 意味 |
|---|---|
| field_id | 一意なUI項目ID |
| group | P1 / P2 / Contact / Physics / Playback / UI などの論理グループ |
| label | 画面上の表示ラベル |
| source | 実装内の参照元を示す識別子 |
| format | text / integer / decimal / boolean |
| visibility | always / debug |
| notes | 補足 |

## sprite_frame_requirements.csv

本番スプライトとして必要なフレームID、配置セル、ピボット、制作状態を管理する。

| 列 | 意味 |
|---|---|
| frame_id | 必須フレームID |
| action_group | 所属アクション |
| pose_role | Startup / Active / Recoveryなどの役割 |
| required | 初期版に必要か |
| sheet_row / sheet_col | 8×8グリッドの配置先 |
| pivot_x / pivot_y | セル内ピボット |
| asset_status | required / ready / verified など |
| notes | 補足 |

