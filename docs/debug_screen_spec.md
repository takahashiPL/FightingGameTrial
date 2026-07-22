# デバッグ画面仕様

この資料は、`reference_infographic.png` で示した「格闘ゲーム判定を観察するための実行画面」を、実装向けに文章で具体化したものです。

## 1. 目的

デバッグ画面の目的は、次を同時に観察できるようにすることです。

- P1 / P2 の現在状態
- 現在アクションのどのフレームにいるか
- どの Pushbox / Hurtbox / Hitbox が有効か
- Hit / Guard / Just Guard / Clash のどれが発生したか
- 押し戻し量や AttackInstanceId がどう解決されたか
- 一時停止 / 1フレーム送り / 低速再生の状態

## 2. 推奨レイアウト

### 上段
- P1 State / Move / ActionFrame
- P2 State / Move / ActionFrame
- Game Frame Counter
- Playback Speed

### 中央
- 対戦キャラクターの表示領域
- 任意で背景を簡素化
- Pushbox / Hurtbox / Hitbox を色分け表示
- キャラクター原点、向き、移動量ベクトルを重ねてよい

### 下段左
- Contact Result パネル
- `Hit` / `Guard` / `JustGuard` / `Clash` / `Miss`
- Damage / HitStop / Knockback / Pushback

### 下段中央
- 操作ボタン群
- Pause
- 1 Frame Step
- Slow 0.25x
- Play
- Reset

### 下段右
- 表示切替
- Boxes ON/OFF
- Sprites ON/OFF
- P2 Dummy ON/OFF
- Input Display ON/OFF

## 3. 推奨表示項目

### P1 / P2 共通
- CurrentState
- CurrentMoveId
- CurrentActionFrame
- Facing
- PositionX / PositionY
- GuardInputState
- JumpState
- AirActionUsed
- Invulnerable

### Contact / Resolution
- LastContactResult
- LastAttackInstanceId
- LastHitboxId
- LastDefenderHurtboxId
- RequestedPushbackPx
- AppliedPushbackPx
- IsJustGuard
- IsClash

### Playback / Simulation
- GlobalGameFrame
- IsPaused
- PlaybackScale
- HitStopRemainingFrames

## 4. 色の推奨

- Pushbox: 青
- Hurtbox: 緑
- Hitbox: 赤
- Character Pivot: 白
- Guard 成立時表示: 黄
- Just Guard 成立時表示: 水色
- Clash 成立時表示: 紫

## 5. 操作の想定

- `Space`: Pause / Resume
- `.` : 1フレーム送り
- `,` : 低速再生切替
- `R` : Round / State Reset
- `F1`: 判定箱表示切替
- `F2`: スプライト表示切替
- `F3`: 入力表示切替

## 6. 実装メモ

- 内部シミュレーションは 60Hz 固定
- Pause 中は時間進行を止めるが、明示的な 1フレーム送りのみ許可
- HitStop 中もログ記録は継続し、キャラクター進行・判定更新は停止
- 描画FPSと無関係に、表示項目はゲームフレーム番号を正本とする

## 7. モックアップ画像との関係

- `debug_screen_wireframe.png`: ブロック配置の最小構成
- `debug_screen_mockup.png`: 実行イメージのサンプル
- 実装時の細かいUIスキンは変更してよいが、表示項目の意味は維持すること
