# CSVデータ辞書

## データの完成状態（重要）

| ファイル | 状態 | 説明 |
|---|---|---|
| `sprite_frame_requirements.csv` | 配置計画の正本 | セル配置の唯一の正本 |
| `art/spritesheet_layout.csv` | 正本の展開表 | requirements と同期 |
| `frames.csv` | 初期検証用サンプル | Idle + StandPunch のみ |
| `boxes.csv` | 初期検証用サンプル | Idle と StandPunch Active のみ |
| `moves.csv` | 設計データ（数値は仮値） | `value_status=provisional` |
| `hit_resolution.csv` | 方向ごとの一次分類参照 | **Trade 行は置かない**（複合集約はコード） |
| `state_transitions.csv` | **草案・確認用** | 完全実行用SMではない。StandGuardをCombatStateにしない |
| `debug_ui_fields.csv` | UI項目一覧 | SimulationTick / CombatFrame / ActionFrame を区別 |

ゲーム仕様の正本は `docs/rules.md`。  
学習・可読性方針は `docs/learning_and_readability.md`。

---

## 時間の用語

| 用語 | 意味 |
|---|---|
| **SimulationTick** | 60Hzで常に進む。入力履歴用。HitStop中も進む |
| **CombatFrame** | 戦闘進行フレーム。HitStop中は進まない |
| **ActionFrame** | キャラのポーズ／技コマ番号。HitStop中は進まない。新状態入場時は0 |

旧称「GameFrame」だけに依存しない。HUDでは日本語注釈を付ける。

### ActionFrame と duration（オフバイワン対策）

- 新状態入場の CombatFrame では `action_frame = 0`、そのフレームは frame 0 データを使用
- CombatFrame **末尾**で現在ポーズの残り duration を 1 減らす
- `duration_game_frames = 3` なら、そのポーズは **3 CombatFrame** 使われる
- 入場と同時に1消費して実質 duration-1 にする実装は禁止（`docs/rules.md` §3.3）

---

## CSV とコードの責務

### コード側で固定

- 処理順、接触全件収集、Clash、**Trade集約**、AttackInstance、Push、Facing二種、アンチ多重ヒット
- SimulationTick / CombatFrame / ActionFrame の進行規則
- duration 末尾減算

### CSV

- フレーム数、ダメージ、硬直、Pushback、箱、対象フラグ、技属性

### アダプタ／文書

- エンジン入力バインド、UV、Sprite、デバッグキー

---

## 結果種別

| 用語 | 意味 |
|---|---|
| **NoContact** | 候補なし。毎tickログしない |
| **Miss** / **InvalidTarget** | 幾何重なりあるが属性無効（方向ごとの一次分類） |
| **Whiff** | 技終了まで有効接触なし（デバッグ表現） |
| **Clash** | Hitbox同士（一次分類） |
| **Trade** | **複合結果**。一次分類ではない。双方向がともに Hit のときだけ集約 |
| **JustGuard** | 攻撃弾き型。Back の新規押下エッジ |
| **IgnoredByInvulnerability** | 無敵。Instance 非消費 |

### Trade の集約手順（コード）

1. Clash 除外後、P1→P2 / P2→P1 を個別に Invuln / Miss / JG / Guard / Hit へ分類  
2. 両方 Hit のときのみ Trade と表示・記録  
3. 片方だけ Guard 等なら Trade にしない  

`hit_resolution.csv` に Trade を priority 行として置かない。

---

## 仮値・未確定

暫定: JustGuardWindow=3、Damage、Stun、Pushback、Jump移動量、LandingRecoveryフレーム、ClashRecoil量  
未確定: 空中Pガード、多段、必殺、キャラ差、Clash壁際転送（初期は破棄）

---

## frames.csv

| 列 | 意味 |
|---|---|
| duration_game_frames | そのポーズを使う CombatFrame 数。末尾減算方式（§3.3） |
| can_guard | そのポーズでガード成立可能か |
| data_scope / notes | サンプル範囲と補足 |

## boxes.csv

push / hurt / hit。サイズは仮値あり。

## moves.csv

| 列 | 意味 |
|---|---|
| attack_category | 分類ラベルのみ |
| can_hit_ground / can_hit_crouch / can_hit_air | 対象フラグ（幾何と併用） |
| value_status | provisional |

## hit_resolution.csv

**方向ごとの一次分類**の参照表。priority が小さいほど先に評価する目安。

| 列 | 意味 |
|---|---|
| result | Clash / IgnoredByInvulnerability / Miss / JustGuard / Guard / Hit |
| notes | Clash壁際破棄、JGのBackエッジ条件など |

Trade は表外の集約処理（`docs/rules.md` §6.3）。

## state_transitions.csv

草案。`data_status=draft`。

- `StandGuard` / `CrouchGuard` を `to_state`（CombatState）にしない
- Down+Back → Crouch（その場しゃがみ、水平移動なし）
- BlockStun 終了 → Crouch または Neutral

CombatState / GuardPosture / DisplayAnimation の分離は rules §1.3。

## sprite_frame_requirements.csv

配置正本。`StandGuard` 等の名前は**表示アニメ用スプライトID**であり、CombatState名ではない。

## UV変換

描画アダプタ注意。判定仕様の正本ではない。

## debug_ui_fields.csv

| 重要項目 | 日本語の意味 |
|---|---|
| SimulationTick | 入力記録用。60Hzで常に進む |
| CombatFrame | 戦闘フレーム。HitStop中は止まる |
| ActionFrame | 技・ポーズ番号。入場時0。HitStop中は止まる |
| CombatState | 戦闘状態（BlockStun等） |
| GuardPosture | ガード姿勢 Stand/Crouch |
| DisplayAnimation | 表示名（StandGuard等） |
| FacingForInput / FacingFinal | 入力解釈用 / Push後の最終向き |
| IsTrade | 複合結果フラグ |
