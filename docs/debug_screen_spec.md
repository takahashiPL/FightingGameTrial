# デバッグ画面仕様

この資料は、判定観察用の実行画面を実装向けに具体化したものです。

- 表示項目の正本: `data/debug_ui_fields.csv`
- ゲーム仕様の正本: `docs/rules.md`
- 可読性方針: `docs/learning_and_readability.md`

CSV の `implementation_phase` を優先する。

## 1. 目的

- CombatState / Move / ActionFrame
- SimulationTick（入力用）と、必要なら CombatFrame（戦闘用）
- 判定箱の有効状態
- 方向別の接触結果、および Trade 集約の有無
- Pause / 1フレーム送り

学習者が「今どの時計が進んでいるか」を誤解しない表示を優先する。

## 2. 時間表示（重要）

| HUD項目 | 日本語の意味 | HitStop中 |
|---|---|---|
| **SimulationTick** | 入力記録用の60Hzカウンタ | **進む** |
| **CombatFrame** | 戦闘進行カウンタ（将来表示） | 止まる |
| **ActionFrame** | 技・ポーズ番号（入場時0） | 止まる |

旧称「GameFrame」だけでは不十分。初期必須は SimulationTick。ラベル近くに日本語説明を置く。

## 3. 実装フェーズ

### 初期必須

- P1/P2: CombatState, Move, ActionFrame
- Playback: SimulationTick, Pause
- Contact: Result, AttackInstanceId
- UI: Boxes

対象モーションは Idle + StandPunch。

### 将来候補

- CombatFrame、GuardPosture、DisplayAnimation
- FacingForInput / FacingFinal
- IsJustGuard / IsClash / IsTrade
- Pushback、Whiff、入力履歴など

## 4. 接触結果の表示

| 結果 | 注意 |
|---|---|
| NoContact | 毎tick出さない |
| Miss / InvalidTarget | 方向ごとの一次分類 |
| Clash | 一次分類 |
| Trade | **複合結果**。双方向とも Hit のときだけ集約表示 |
| JustGuard | 攻撃弾き型 |
| Whiff | 技終了時のデバッグ表現 |

片方が Guard などで、もう片方が Hit のときは Trade と書かない。

## 5. ガード表示

- CombatState = BlockStun のとき、硬直の所有者は BlockStun
- StandGuard / CrouchGuard は DisplayAnimation 名として出してよいが、CombatState と混同しない
- GuardPosture（Stand/Crouch）を別表示できると学習に良い

## 6. 推奨レイアウト・操作

上段: CombatState / Move / ActionFrame / SimulationTick / Pause  
中央: キャラ＋箱  
下段: Contact Result、操作ボタン、Boxes ON/OFF  

キー割り当てはアダプタ層（Space=Pause、.=1フレーム送り、F1=箱 など）。

## 7. 実装メモ

- Unity Update/FixedUpdate を仕様正本にしない
- HitStop中は SimulationTick と入力のみ進み、戦闘は停止
- 本番スプライト未作成。プレースホルダ可
- 参考PNGを完成スプライトにしない
- モックアップの全項目再現は初期必須ではない
