# デバッグ画面仕様

この資料は、`reference_infographic.png` で示した「格闘ゲーム判定を観察するための実行画面」を、実装向けに文章で具体化したものです。

表示項目の正本は `data/debug_ui_fields.csv` とする。  
本資料と CSV で差がある場合は CSV の `implementation_phase` を優先する。

## 1. 目的

デバッグ画面の目的は、次を同時に観察できるようにすることです。

- P1 / P2 の現在状態
- 現在アクションのどのフレームにいるか
- どの Pushbox / Hurtbox / Hitbox が有効か
- Hit / Guard / Just Guard / Clash のどれが発生したか（段階的に）
- 一時停止 / 1フレーム送り / 低速再生の状態

初期実装では、判定ループが動いていることを確認できる最小HUDに限定する。

## 2. 実装フェーズ

### 初期必須（最初の実装対象）

`debug_ui_fields.csv` で `implementation_phase=initial` の項目。

- P1 / P2: State, MoveId, ActionFrame
- Playback: GameFrame, Pause
- Contact: Result, AttackInstanceId
- UI: Boxes表示状態

初期実装対象モーションは **Idle + StandPunch** のみ。  
Pushback詳細、JustGuard/Clash専用フラグ、ジャンプ状態などは初期必須に含めない。

### 将来候補

`implementation_phase=future` の項目。例:

- JumpState / AirActionUsed
- Pushback詳細（Requested / Applied）
- IsJustGuard / IsClash
- 入力履歴詳細
- 座標・Facing・Invulnerable など

将来項目はモックアップにあっても、初期実装で必須ではない。

## 3. 推奨レイアウト

### 上段
- P1 State / Move / ActionFrame
- P2 State / Move / ActionFrame
- Game Frame Counter
- Pause 状態
- （将来）Playback Speed

### 中央
- 対戦キャラクターの表示領域
- 任意で背景を簡素化
- Pushbox / Hurtbox / Hitbox を色分け表示（Boxes ON時）
- （将来）キャラクター原点、向き、移動量ベクトル

### 下段左
- Contact Result パネル
- 初期: `Hit` / `Miss` など実際に実装した結果 + AttackInstanceId
- （将来）`Guard` / `JustGuard` / `Clash`、Damage / HitStop / Knockback / Pushback

### 下段中央
- 操作ボタン群
- Pause
- 1 Frame Step
- （将来）Slow 0.25x
- Play
- Reset

### 下段右
- 表示切替
- Boxes ON/OFF（初期必須）
- （将来）Sprites ON/OFF
- （将来）P2 Dummy ON/OFF
- （将来）Input Display ON/OFF

## 4. 色の推奨

- Pushbox: 青
- Hurtbox: 緑
- Hitbox: 赤
- Character Pivot: 白
- Guard 成立時表示: 黄（将来）
- Just Guard 成立時表示: 水色（将来）
- Clash 成立時表示: 紫（将来）

## 5. 操作の想定

- `Space`: Pause / Resume
- `.` : 1フレーム送り
- `,` : 低速再生切替（将来）
- `R` : Round / State Reset
- `F1`: 判定箱表示切替
- `F2`: スプライト表示切替（将来）
- `F3`: 入力表示切替（将来）

## 6. 実装メモ

- 内部シミュレーションは 60Hz 固定
- Pause 中は時間進行を止めるが、明示的な 1フレーム送りのみ許可
- HitStop 中もログ記録は継続し、キャラクター進行・判定更新は停止（HitStop自体は将来でも可）
- 描画FPSと無関係に、表示項目はゲームフレーム番号を正本とする
- 本番スプライト未作成のため、初期は矩形プレースホルダでよい
- 参考PNGを完成スプライトとして Import しない

## 7. モックアップ画像との関係

- `debug_screen_wireframe.png`: ブロック配置の最小構成
- `debug_screen_mockup.png`: 実行イメージのサンプル（将来項目を含む場合あり）
- 実装時の細かいUIスキンは変更してよい
- **初期実装はモックアップの全項目を再現しなくてよい**。`implementation_phase=initial` を満たせばよい
