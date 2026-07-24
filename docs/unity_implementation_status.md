# Unity 実装状況・次工程（段階1〜10B-3到達後）

最終更新: 2026-07-24
対象ブランチ: `unity`
最新全体コミット（HEAD）: **`c22bc2a`**（Keep dynamic font data across builds）
最新コミット済み実装: **`582619b`**（Move fighter attack and hit state to participants）
段階10B-3（Push Box）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜10B-3の到達点

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
| **10B-3** | Participant 共通の横方向 Push Box／すり抜け防止（等分分離・端クランプ転送） | **実装済み・確認済み** |

### 2.1 段階10B-2で確定した責務分担（維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterParticipant`** | 参加枠（P1/P2）。Motor / Visual / Tint / 入力種別 / Opponent 参照 / **PushBoxHalfWidth** |
| **`DebugFighterAttackState`** | **攻撃状態の正本**（ActionFrame、IsActionPlaying、IsJPunchAttack、HasCurrentJPunchHit、LastAttackResult、攻撃入力立ち上がり） |
| **Participant 被弾記録** | HitCount / WasHitThisCombatFrame / LastHitCombatFrame（`ReceiveHit`） |
| **`SimulationTimeState`** | **共有時間状態のみ**（SimulationTick、CombatFrame、Pause、HitStop、Status、CurrentInput） |
| **`SimulationSession`** | tick 進行、移動→Push→Facing、攻撃共通処理、`TryResolveJPunchHit(attacker, defender)` |

削除済み（現役ではない）:

- `DebugDummyTarget`（コード・Scene Component・Session 参照）
- `SimulationTimeState` の旧攻撃フィールド5つ（ActionFrame / IsActionPlaying / IsJPunchAttack / HasCurrentJPunchHit / LastAttackResult）
- 旧 `StartJPunchAttack` / `EndJPunchAttack`

### 2.2 段階10B-3で確定した Push Box 責務

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterParticipant.pushBoxHalfWidth`** | 横方向 Push Box 半幅の所有（既定 **0.5f**） |
| **`DebugFighterMotor`** | 位置の正本 `LogicalX` と Push 書き戻し `SetLogicalX`（既存 minX/maxX クランプ） |
| **`DebugFighterPushResolver`** | Participant 同士の横方向重なり解消（P1/P2 名では分岐しない） |
| **`SimulationSession`** | **移動 → Push 補正 → Facing → Action/Hit** の順序管理。HUD用距離記録。補正開始時のみログ |

初期仕様（現段階）:

- 対象は横方向のみ。`minDistance = halfA + halfB`
- 距離不足分は左右へ**等分**して分離（接触後に相手を押し、双方が動く）
- Motor の minX/maxX で片側が動けない場合、残り補正をもう一方へ転送
- 縦方向・ノックバック・画面端専用処理・飛び越え反転は**未実装**
- 画面端・壁際での補正配分は、**ステージ境界実装時に再検討**

### 2.3 確認済みの挙動（段階10B-3まで）

- SimulationTick は HitStop 中も進む。CombatFrame / Action / 移動 / Push / Facing 更新は止まる
- Pause 中は自動進行しない。`.` で 1 SimulationTick だけ進む（接触時も Push 補正される）
- Left/Right はワールド X 移動のみ。Facing は入力では変えない
- 移動後・Push 後・Hit 前に、両者とも相手向き合い Facing を確定（ほぼ同 X なら直前 Facing 維持）
- 通常移動で P1/P2 が重ならない。押し続けても通り抜けない
- 接触時 HUD: Push Dist=1.00 / Over=0。接触後は等分押し分けで双方が移動する（暫定仕様）
- 反対方向へ正常に離れられる。接触中も Facing は安定
- P1 は Gameplay 入力で移動・攻撃。P2 は **共通経路を持つが、現在は Neutral 入力のため通常は棒立ち**
- Jパンチ: Startup 1〜3 / Active 4〜6 / Recovery 7〜12
- 接触中の J 攻撃・Hit・6F HitStop が正常。離れた状態では Miss
- Hit は `TryResolveJPunchHit(attacker, defender)`。同一 CombatFrame で両方向判定（**相打ちの土台**）
- 1攻撃1Hit は `AttackState.HasCurrentJPunchHit`。被弾は `defender.ReceiveHit`
- A / R は P1 `AttackState` のテスト開始 / Reset
- HUD に Push Dist / Over を追加。Push 補正**開始時のみ**ログ（押し続け中の毎フレーム出力はしない）
- Console Error 0 / Warning 0

### 2.4 段階9由来の Hit 判定はなお暫定

現在の Hit は **正式な Box 判定ではない**（距離＋向きの暫定）。

暫定条件（正本は各 `AttackState` + Motor 座標）:

- `IsJPunchAttack`
- ActionFrame が Active（4〜6）
- その攻撃で未 Hit（`HasCurrentJPunchHit`）
- Facing 方向側に相手がいる
- 向き込み距離 ≤ `attackRange`（初期 1.35）

この暫定 Hit は**削除対象ではない**。
SimulationTick / ActionFrame / HitStop / 1攻撃1Hit / attacker・defender 共通化の検証実績として残し、のちに Box 重なり判定へ**置換**する。

### 2.5 相打ちについて（未完了の実動作確認）

- **実装済み**: 同一 CombatFrame で両方向 Hit 判定する土台
- **未実施**: P2 入力や AI による実際の相打ち確認（P2 は現状 Neutral）

### 2.6 キャラクター差し替え構造の現状

P1 / P2 は同じ `DebugFighterParticipant` 基盤を使用しており、
入力源を Gameplay / Neutral / 将来の 2P 入力・AI へ差し替えられる土台はある。

一方、キャラクターごとの以下をデータ定義として差し替える仕組みは**未実装**。

- Idle / Attack などの画像・Animation
- 移動速度や体格
- Push Box / Hurt Box / Hit Box のキャラ別データ化
- Startup / Active / Recovery
- HitStop / Damage / HitStun / Knockback

現在は共通のデバッグ用見た目・共通定数・距離ベース Hit・共通 Push 半幅を使用している。
別キャラクター対応は、Box 実装（段階11）と攻撃データ化（段階15）の後続工程で扱う。

---

## 3. Facing と移動入力の分離（段階10A・実装済み）

### 3.1 実装済みの正式寄せ

1. **移動方向と Facing を分離**（Left/Right＝ワールド X のみ）
2. 両者とも基本的に相手と向き合う
3. `selfX < opponentX` → 右向き / `selfX > opponentX` → 左向き
4. ほぼ同位置（epsilon）では直前 Facing を維持
5. Facing 確定は **Push 補正後**・Hit 判定前（段階10B-3）
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

### 3.2 段階10の Push Box（段階10B-3・完了）

Facing 分離とセットで設計していた **横方向 Push Box / 地上すり抜け防止** は**実装済み・確認済み**。
等分押し分けは現段階の**暫定仕様**。壁際配分はステージ境界実装時に再検討する。
（Box **可視化**および Hurt/Hit Box 判定は段階11。）

---

## 4. 判定箱方針（正式）

| 箱 | 役割 | 現状 |
|---|---|---|
| **Push Box** | 重なり防止・地上すり抜け防止・向き合いとセット | **実装済み（横方向・等分分離の暫定）**。可視化は未実装 |
| **Hurt Box** | 被弾判定 | **未実装**（CSVサンプルのみ） |
| **Hit Box** | 攻撃判定（Active のみ有効） | **未実装**（CSVサンプルのみ） |

今後の置換・拡張方針:

- Facing による左右反転を箱に反映する
- Game ビュー上で Box を可視化する
- 暫定距離判定を **Box 同士の重なり判定**へ置き換える
- ステージ端実装時に Push の壁際補正配分を再検討する

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A / 10B-2 / 10B-3** | Facing 分離、2体共通化、AttackState、横方向 Push Box／すり抜け防止 | **完了** |
| **11** | Push/Hurt/Hit Box の可視化、または判定基盤（Active のみ Hit Box、Facing 反転、距離判定→Box 重なりへ置換） | **次回候補** |
| **12** | 被 Hit 状態、HitStun、被 Hit 表示 | 未実装 |
| **13** | ノックバック、押し戻し、ステージ端（壁際 Push 配分の再検討を含む） | 未実装 |
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

- 段階1〜**10B-3**まで到達（Push Box／すり抜け防止完了）
- 最新全体コミット（HEAD）: `c22bc2a`。最新コミット済み実装: `582619b`。10B-3 は検証済み・未コミット
- 攻撃状態の正本は各 **`Participant.AttackState`**。`SimulationTimeState` は共有時間・Pause・HitStop
- 処理順: **移動 → Push 補正 → Facing → Action/Hit**
- Push は Participant 共通・横方向・等分分離（暫定）。壁際配分は後で再検討
- Hit はなお**距離＋向きの暫定**。Hurt/Hit Box 判定ではない
- P2 は共通経路を持つが Neutral のため通常攻撃しない。相打ちの実動作確認は未実施
- 次は段階11（**Push/Hurt/Hit Box の可視化、または判定基盤**）を優先
