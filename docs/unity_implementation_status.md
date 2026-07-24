# Unity 実装状況・次工程（段階1〜10B-2到達後）

最終更新: 2026-07-24
対象ブランチ: `unity`
最新実装コミット: **`582619b`**（Move fighter attack and hit state to participants）

このファイルは、Unity 側の**実装済み / 暫定 / 未実装 / 次回候補 / 正式方針**を混同せずに追うための正本です。
ゲーム仕様そのものの正本は引き続き `docs/rules.md` です。

---

## 1. 区分の読み方

| 区分 | 意味 |
|---|---|
| **実装済み・確認済み** | FightDebugScene で動作確認済み |
| **暫定実装** | 動くが、正式仕様へ置き換える前提 |
| **正式方針（未実装）** | 今後そうする、と決めた設計。コード未反映 |
| **未実装** | まだ作っていない |
| **次回候補** | 工程上の次ステップ |

---

## 2. 段階1〜10B-2の到達点

| 段階 | 内容 | 状態 |
|---|---|---|
| 1 | 60Hz SimulationTick、Catch-up上限 | 実装済み |
| 2 | Pause / 1 SimulationTick Step | 実装済み |
| 3 | Debug HUD | 実装済み |
| 4 | HitStop（テスト用 H キー含む） | 実装済み |
| 5 | ActionFrame（テスト用 A / R） | 実装済み |
| 6 | 入力サンプリング（矢印・J Held → CurrentInput） | 実装済み |
| 7 | 左右移動、Sprite 表示 | 実装済み |
| 8 | Jパンチ（Startup/Active/Recovery 見た目、立ち上がり入力） | 実装済み |
| 9 | 距離＋向きの暫定 Hit、1攻撃1Hit、Hit時6F HitStop | 実装済み（判定は暫定のまま） |
| **10A** | Facing と移動入力の分離、相手向き合い Facing、同X付近は直前 Facing 維持 | **実装済み・確認済み** |
| **10B-2** | 2体共通 Participant、AttackState、attacker/defender Hit、被弾記録、旧 DummyTarget / TimeState 攻撃フィールド整理 | **実装済み・確認済み** |

### 2.1 段階10B-2で確定した責務分担

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterParticipant`** | 参加枠（P1/P2）。Motor / Visual / Tint / 入力種別 / Opponent 参照 |
| **`DebugFighterAttackState`** | **攻撃状態の正本**（ActionFrame、IsActionPlaying、IsJPunchAttack、HasCurrentJPunchHit、LastAttackResult、攻撃入力立ち上がり） |
| **Participant 被弾記録** | HitCount / WasHitThisCombatFrame / LastHitCombatFrame（`ReceiveHit`） |
| **`SimulationTimeState`** | **共有時間状態のみ**（SimulationTick、CombatFrame、Pause、HitStop、Status、CurrentInput） |
| **`SimulationSession`** | tick 進行、Facing、攻撃共通処理、`TryResolveJPunchHit(attacker, defender)` |

削除済み（現役ではない）:

- `DebugDummyTarget`（コード・Scene Component・Session 参照）
- `SimulationTimeState` の旧攻撃フィールド5つ（ActionFrame / IsActionPlaying / IsJPunchAttack / HasCurrentJPunchHit / LastAttackResult）
- 旧 `StartJPunchAttack` / `EndJPunchAttack`

### 2.2 確認済みの挙動（段階10B-2まで）

- SimulationTick は HitStop 中も進む。CombatFrame / Action / 移動 / Facing 更新は止まる
- Pause 中は自動進行しない。`.` で 1 SimulationTick だけ進む
- Left/Right はワールド X 移動のみ。Facing は入力では変えない
- 移動後・Hit 前に、両者とも相手向き合い Facing を確定（ほぼ同 X なら直前 Facing 維持）
- P1 は Gameplay 入力で移動・攻撃。P2 は **共通攻撃経路を持つが、現在は Neutral 入力のため通常は棒立ち**（攻撃しない）
- Jパンチ: Startup 1〜3 / Active 4〜6 / Recovery 7〜12
- Hit は `TryResolveJPunchHit(attacker, defender)`。同一 CombatFrame で P1→P2 と P2→P1 を同じ関数で判定（**相打ちの土台**）
- 1攻撃1Hit は `AttackState.HasCurrentJPunchHit`。被弾は `defender.ReceiveHit`
- 近距離 Hit / 遠距離 Miss / Facing 反転後の向き込み Hit / HitStop 6F
- J 長押しで自動再攻撃しない（Held 立ち上がりのみ）
- A / R は P1 `AttackState` のテスト開始 / Reset。Pause → A → Step → R でフレーム確認可能
- HUD の Action / AF / AttackResult / PunchHitDone は P1 AttackState。P2 HitCount は Participant 被弾記録
- Console Error 0 / Warning 0

### 2.3 段階9由来の Hit 判定はなお暫定

現在の Hit は **正式な Box 判定ではない**（距離＋向きの暫定）。

暫定条件（正本は各 `AttackState` + Motor 座標）:

- `IsJPunchAttack`
- ActionFrame が Active（4〜6）
- その攻撃で未 Hit（`HasCurrentJPunchHit`）
- Facing 方向側に相手がいる
- 向き込み距離 ≤ `attackRange`（初期 1.35）

この暫定 Hit は**削除対象ではない**。
SimulationTick / ActionFrame / HitStop / 1攻撃1Hit / attacker・defender 共通化の検証実績として残し、のちに Box 重なり判定へ**置換**する。

### 2.4 相打ちについて（未完了の実動作確認）

- **実装済み**: 同一 CombatFrame で両方向 Hit 判定する土台
- **未実施**: P2 入力や AI による実際の相打ち確認（P2 は現状 Neutral）

### 2.5 キャラクター差し替え構造の現状

P1 / P2 は同じ `DebugFighterParticipant` 基盤を使用しており、
入力源を Gameplay / Neutral / 将来の 2P 入力・AI へ差し替えられる土台はある。

一方、キャラクターごとの以下をデータ定義として差し替える仕組みは**未実装**。

- Idle / Attack などの画像・Animation
- 移動速度や体格
- Push Box / Hurt Box / Hit Box
- Startup / Active / Recovery
- HitStop / Damage / HitStun / Knockback

現在は共通のデバッグ用見た目・共通定数・距離ベース Hit を使用している。
別キャラクター対応は、Box 実装（段階11）と攻撃データ化（段階15）の後続工程で扱う。

---

## 3. Facing と移動入力の分離（段階10A・実装済み）

### 3.1 実装済みの正式寄せ

1. **移動方向と Facing を分離**（Left/Right＝ワールド X のみ）
2. 両者とも基本的に相手と向き合う
3. `selfX < opponentX` → 右向き / `selfX > opponentX` → 左向き
4. ほぼ同位置（epsilon）では直前 Facing を維持
5. Facing 確定は移動後・Hit 判定前
6. HitStop / Pause 中は Facing も更新しない

#### 前進・後退の定義（維持）

| 用語 | 意味 |
|---|---|
| **前進** | 相手へ近づく方向への移動 |
| **後退** | 相手から離れる方向への移動 |

「Left＝常に後退」「Right＝常に前進」ではない。相手との位置関係で解釈する。

| 位置関係 | Facing | Right 入力 | Left 入力 |
|---|---|---|---|
| 自分が相手の**左側** | 右向き | **前進** | **後退** |
| 自分が相手の**右側** | 左向き | **後退** | **前進** |

対応仕様: `docs/rules.md` §2.3。

### 3.2 段階10のうち未実装の残り

自動振り向きとセットで設計する **Push Box / 地上すり抜け防止 / Box 可視化** はまだ未実装。

---

## 4. 判定箱方針（正式・ほぼ未実装）

| 箱 | 役割 | 現状 |
|---|---|---|
| **Push Box** | 重なり防止・地上すり抜け防止・向き合いとセット | **未実装** |
| **Hurt Box** | 被弾判定 | **未実装**（CSVサンプルのみ） |
| **Hit Box** | 攻撃判定（Active のみ有効） | **未実装**（CSVサンプルのみ） |

今後の置換方針:

- Facing による左右反転を箱に反映する
- Game ビュー上で Box を可視化する
- 暫定距離判定を **Box 同士の重なり判定**へ置き換える

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10（残り）** | Push Box 定義・可視化、地上すり抜け防止（Facing 分離・2体共通化は完了） | **次回候補** |
| **11** | Hurt Box / Punch Hit Box、Active のみ Hit Box 有効、Facing 反転、Box 可視化、距離判定→Box 重なりへ置換 | 次回以降 |
| **12** | 被 Hit 状態、HitStun、被 Hit 表示 | 未実装 |
| **13** | ノックバック、押し戻し、ステージ端 | 未実装 |
| **14** | HP、Damage、KO | 未実装 |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | 未実装 |

その後の候補（順不同・未着手）:

- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- ガード、コンボ
- Animation 本接続
- 2P 入力 / CPU、ラウンド進行（P2 攻撃経路の Neutral 差し替えを含む）

---

## 6. 関連ドキュメント

| ファイル | 役割 |
|---|---|
| `docs/rules.md` | ゲーム仕様の正本 |
| `docs/learning_and_readability.md` | 実装の可読性方針 |
| `docs/debug_screen_spec.md` | デバッグ画面の項目方針 |
| `docs/sprite_art_status.md` | 素材完成度 |
| `README.md` | 入口・要約 |
| **このファイル** | Unity 実装の到達点・暫定/正式・次工程 |

---

## 7. 一言まとめ

- 段階1〜**10B-2**まで到達。最新コミット `582619b`
- 攻撃状態の正本は各 **`Participant.AttackState`**。`SimulationTimeState` は共有時間・Pause・HitStop
- Facing 分離・相手向き合い・2体共通 Hit 経路は**実装済み**
- Hit はなお**距離＋向きの暫定**。Box 判定ではない
- P2 は共通経路を持つが Neutral のため通常攻撃しない。相打ちの実動作確認は未実施
- 次は段階10の残り（**Push Box・すり抜け防止**）を優先
