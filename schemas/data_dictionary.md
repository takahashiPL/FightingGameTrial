# CSVデータ辞書

## データの完成状態（重要）

| ファイル | 状態 | 説明 |
|---|---|---|
| `sprite_frame_requirements.csv` | 配置計画の正本 | セル配置の唯一の正本。本番素材の有無とは別 |
| `art/spritesheet_layout.csv` | 正本の展開表 | `sprite_frame_requirements.csv` から生成・同期する |
| `frames.csv` | 初期検証用サンプル | **Idle + StandPunch のみ**。他モーションは未収録 |
| `boxes.csv` | 初期検証用サンプル | Idle と StandPunch Active のみ。箱サイズは仮値 |
| `moves.csv` | 設計データ（数値は仮値） | 技定義あり。`value_status=provisional` |
| `hit_resolution.csv` | 解決優先度の設計 | 実装時の参照表 |
| `state_transitions.csv` | 遷移の設計草案 | 初期実装では Idle / StandPunch 周辺のみ使用 |
| `debug_ui_fields.csv` | UI項目一覧 | `implementation_phase` で初期必須と将来を区別 |

**存在しないフレーム・箱・数値を推測して埋めないこと。**  
`implementation_scope=planned` のフレームは配置予約であり、実装済みではない。

## 仮値一覧

次はすべて初期検証用の仮値であり、バランス確定値ではない。

| 項目 | 仮値の扱い | 参照 |
|---|---|---|
| JustGuardWindow | 3 ゲームフレーム | `docs/rules.md` |
| Damage | `moves.csv` の damage | provisional |
| HitStun / BlockStun | `hitstun_frames` / `blockstun_frames` | provisional |
| Pushback / Recoil | `guard_pushback_px` / `attacker_recoil_px` | provisional |
| Jump 移動量 | 将来 `frames.csv` の `move_x` / `move_y` | 未収録（仮値も未設定） |
| StandPunch Active の `move_x` | 1 | `frames.csv` 仮値 |
| 判定箱サイズ | `boxes.csv` の width/height/offset | 仮値 |

## 未確定仕様

- **空中パンチのガード可否**（特にしゃがみガード）は未確定。`moves.csv` の `can_stand_guard` / `can_crouch_guard` は暫定値であり、仕様確定ではない。

---

## frames.csv

初期検証用サンプル。現時点の収録は `data_scope=initial_sample` の Idle / StandPunch のみ。

| 列 | 意味 |
|---|---|
| frame_id | 一意の表示フレームID |
| move_id | 所属する技・アクション |
| frame_index | アクション内番号 |
| sheet_x / sheet_y | スプライトシート左上ピクセル。`sprite_frame_requirements` の row/col×128 と一致させる |
| width / height | 切り出しサイズ |
| pivot_x / pivot_y | セル左上基準の原点 |
| duration_game_frames | この絵を維持するゲームフレーム数（仮値あり） |
| move_x / move_y | 1ゲームフレーム単位の移動量（仮値あり） |
| can_accept_input | 入力受付可否 |
| can_guard | ガード移行可否 |
| invulnerable | 無敵か |
| airborne | 空中か |
| data_scope | `initial_sample` など。完成データではないことを示す |
| notes | 補足 |

## boxes.csv

1ボックスを1行で定義する。初期検証用サンプル。

| 列 | 意味 |
|---|---|
| frame_id | 所属フレーム |
| box_id | ボックス識別子 |
| box_type | push / hurt / hit |
| offset_x / offset_y | キャラクター原点からの相対位置 |
| width / height | ボックスサイズ（仮値あり） |
| enabled | 有効可否 |
| hit_group_id | 多段技用。初期版は1 |
| data_scope | `initial_sample` など |
| notes | 高さや用途 |

## moves.csv

| 列 | 意味 |
|---|---|
| move_id | 技ID |
| display_name | 表示名 |
| total_frames / startup_frames / active_frames / recovery_frames | フレーム構成（仮値あり） |
| attack_category | Punch / Kick |
| damage | ダメージ（仮値） |
| on_hit_result | HitStun / Knockdown / AirHitStun等 |
| can_stand_guard | 立ちガード可能（暫定値の場合あり） |
| can_crouch_guard | しゃがみガード可能（暫定値の場合あり） |
| can_hit_crouch | しゃがみHurtboxに届く |
| can_hit_air | 空中Hurtboxに届く |
| causes_knockdown | ダウンさせるか |
| blockstun_frames / hitstun_frames | 硬直（仮値） |
| guard_pushback_px / attacker_recoil_px | 押し戻し・反動（仮値） |
| clash_enabled | Hitbox同士のClash対象 |
| air_action_once_per_jump | 1ジャンプ1回制限 |
| value_status | `provisional` = 数値・フラグは仮／暫定 |
| notes | 補足。未確定仕様はここに明記 |

## hit_resolution.csv

同一ゲームフレーム内の接触解決の優先度表。

| 列 | 意味 |
|---|---|
| priority | 小さいほど先に評価する |
| contact_type | Hitbox_vs_Hitbox / Hitbox_vs_Hurtbox など |
| condition | 成立条件の論理説明 |
| result | Clash / Miss / JustGuard / Guard / Hit など |
| attacker_state | 攻撃側の遷移先・継続 |
| defender_state | 防御側の遷移先・継続 |
| damage | 与ダメージ。0 または `move.damage` 参照 |
| notes | 補足 |

Clash（Hitbox同士）は Hurtbox ヒットより先に解決する。

## state_transitions.csv

入力と条件による状態遷移の設計草案。

| 列 | 意味 |
|---|---|
| from_state | 遷移元状態 |
| input | トリガー入力。`None` / `Any` も可 |
| condition | 追加条件 |
| to_state | 遷移先状態 |
| priority | 大きいほど優先（同時候補の仲裁用） |
| notes | 補足 |

初期実装では `Neutral` ↔ `StandPunch` 周辺のみを対象とし、他行は planned として残す。  
`GroundCancelable` などの抽象状態の詳細は未確定のまま残してよい。

## sprite_frame_requirements.csv

**スプライト配置計画の正本。** `sheet_x = sheet_col * 128`、`sheet_y = sheet_row * 128`。

| 列 | 意味 |
|---|---|
| frame_id | 必須フレームID |
| action_group | 所属アクション |
| pose_role | Startup / Active / Recovery などの役割 |
| required | 最終的に必要か（1/0） |
| sheet_row / sheet_col | 8×8グリッドの配置先 |
| pivot_x / pivot_y | セル内ピボット |
| implementation_scope | `initial` = 最初の実装対象 / `planned` = 将来 |
| asset_status | `required` = 本番素材未作成 / `ready` / `verified` |
| notes | 補足 |

`implementation_scope=initial` でも `asset_status=required` の間は、プレースホルダ描画で検証する。参考PNGを完成素材としない。

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
| implementation_phase | `initial` = 初期必須 / `future` = 将来候補 |
| notes | 補足 |

### 初期必須（implementation_phase=initial）

- P1/P2 State, MoveId, ActionFrame
- GameFrame, Pause
- Contact Result, AttackInstanceId
- Boxes表示状態

### 将来候補（implementation_phase=future）

- JumpState, AirActionUsed
- Pushback詳細, IsJustGuard, IsClash
- 入力履歴詳細 など
