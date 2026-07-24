# Unity 実装状況・次工程（段階1〜11A到達後）

最終更新: 2026-07-24
対象ブランチ: `unity`
最新コミット済み HEAD: **`40dc1f4`**（Fix push resolver meta whitespace）
段階10B-3まで: **完了・push 済み**
段階11A（Box 可視化基盤）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜11Aの到達点

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
| **11A** | Push / Hurt / Hit Box 可視化基盤（`DebugBox2D`、LineRenderer、論理接地 Y） | **実装済み・確認済み** |

### 2.1 段階10B-2で確定した責務分担（維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterParticipant`** | 参加枠（P1/P2）。Motor / Visual / Tint / 入力種別 / Opponent 参照 / **PushBoxHalfWidth** / Box ローカル定義 |
| **`DebugFighterAttackState`** | **攻撃状態の正本**（ActionFrame、IsActionPlaying、IsJPunchAttack、HasCurrentJPunchHit、LastAttackResult、攻撃入力立ち上がり） |
| **Participant 被弾記録** | HitCount / WasHitThisCombatFrame / LastHitCombatFrame（`ReceiveHit`） |
| **`SimulationTimeState`** | **共有時間状態のみ**（SimulationTick、CombatFrame、Pause、HitStop、Status、CurrentInput） |
| **`SimulationSession`** | tick 進行、移動→Push→Facing、攻撃共通処理、`TryResolveJPunchHit(attacker, defender)` |

削除済み（現役ではない）:

- `DebugDummyTarget`（コード・Scene Component・Session 参照）
- `SimulationTimeState` の旧攻撃フィールド5つ
- 旧 `StartJPunchAttack` / `EndJPunchAttack`

### 2.2 段階10B-3で確定した Push Box 責務（維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterParticipant.pushBoxHalfWidth`** | 横方向 Push 半幅の判定正本（既定 **0.5f**） |
| **`DebugFighterMotor`** | 位置の正本 `LogicalX` と Push 書き戻し `SetLogicalX` |
| **`DebugFighterPushResolver`** | Participant 同士の横方向重なり解消 |
| **`SimulationSession`** | **移動 → Push 補正 → Facing → Action/Hit** の順序管理 |

等分押し分けは現段階の**暫定仕様**。壁際配分はステージ境界実装時に再検討。

### 2.3 段階11Aで確定した Box 可視化責務

| 入れ物 | 担当 |
|---|---|
| **`DebugBox2D`** | CenterX/Y、HalfWidth/Height、IsActive、Min/Max、GetCorners |
| **`DebugFighterParticipant`** | Push / Hurt / J Punch Hit のローカル定義所有、World 座標計算、論理接地 Y |
| **`DebugFighterBoxView`** | LineRenderer による Game ビュー矩形表示（本番 Visual と分離） |

可視化の要点:

- **Push**: 既存 `pushBoxHalfWidth` を横幅の正本として表示。Push Resolver は変更していない
- **Hurt**: Participant ごとに常時有効表示
- **J Punch Hit**: ActionFrame **4〜6（Active）** のみ有効。Startup / Recovery / Idle では無効
- Facing Left 時は Hit Box の **Local Center X だけ**符号反転（`flipX` / Scale 非依存）
- 色: Push=水色、Hurt=緑、Hit=赤。Gizmos 専用ではない
- Participant.Awake から `DebugFighterBoxView` を自動生成（Scene 手動配線不要）
- `GlobalDrawEnabled` と個別 `drawEnabled` で表示切替
- HUD: `Box P/H/Hit`、`HitBox CenterX`

#### 論理接地 Y（Box 共通の Y 原点）

- Scene 上の Participant Transform は **Sprite 中央 Pivot** 位置であり、足元ではない
- デバッグ Sprite: 中央 Pivot、PPU=100、256px → 表示高約 2.56
- Box 定義（LocalCenterY / HalfHeight）は足元原点前提のため、Transform.y をそのまま使うと上へずれる
- Awake で一度だけ導出: `boxOriginLocalY = -(sprite.pivot.y / sprite.pixelsPerUnit)`
- `LogicalGroundY = Transform.position.y + boxOriginLocalY`
- `WorldCenterY = LogicalGroundY + LocalCenterY`
- `SpriteRenderer.bounds` を毎フレームの正本にはしていない
- Push / Hurt / Hit すべて同じ論理接地 Y を使用

### 2.4 確認済みの挙動（段階11Aまで）

- P1/P2 の Push / Hurt Box が常時表示。下端≒足元、上端≒頭付近
- 移動・接触・Push 中も枠が Participant に追従
- J Punch Active 中だけ赤い Hit Box が表示。Recovery で消える
- Pause 中は表示状態が固定。Step で Active 入場と Hit Box 表示を確認
- HitBox CenterX が LogicalX + LocalCenterX と一致
- 既存の距離 Hit、Hit/Miss、HitStop、Push Resolver は変わらない
- Console Error 0 / Warning 0

（段階10B-3までの移動・Facing・Push・攻撃経路の確認事項は維持。）

### 2.5 未検証・将来注意（段階11Aは完了扱い）

- Facing Left 時の Hit Box 反転は**コード確認済み**
- 実操作での Facing Left 攻撃表示は**未検証**（P1 のみ攻撃入力可能で、Push により通常操作では位置交換しにくい）
- → 段階11A の未完了にはしない。将来の 2P 入力またはテスト経路追加時の確認項目とする
- 現在の接地 Y はデバッグ Sprite の Pivot から導出。将来 Sprite ごとに Pivot が異なるアニメや本番キャラデータを入れる際は、**明示的なキャラクター接地原点**へ置き換える候補
- Box は今回**可視化専用**。Hit 判定はなお距離判定

### 2.6 段階9由来の Hit 判定はなお暫定

現在の Hit は **正式な Box 重なり判定ではない**（距離＋向きの暫定）。
段階11A の Box は可視化土台であり、判定置換は**段階11B**。

### 2.7 相打ちについて（未完了の実動作確認）

- **実装済み**: 同一 CombatFrame で両方向 Hit 判定する土台
- **未実施**: P2 入力や AI による実際の相打ち確認（P2 は現状 Neutral）

### 2.8 キャラクター差し替え構造の現状

入力源の差し替え土台はある。一方、キャラクターごとの画像・速度・体格・Box データ・攻撃性能をデータ定義として差し替える仕組み、および攻撃データ SO 化は**未実装**。
別キャラクター対応は Box 判定（段階11B）と攻撃データ化（段階15）の後続で扱う。

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

横方向 Push Box / 地上すり抜け防止は**実装済み・確認済み**。
等分押し分けは暫定。壁際配分はステージ境界実装時に再検討。

---

## 4. 判定箱方針

| 箱 | 役割 | 現状 |
|---|---|---|
| **Push Box** | 重なり防止・地上すり抜け防止 | **実装済み（横方向・等分分離の暫定）**。**可視化済み（11A）** |
| **Hurt Box** | 被弾判定 | **可視化済み（11A）**。重なり判定は**未実装（11B）** |
| **Hit Box** | 攻撃判定（Active のみ有効） | **可視化済み（11A・Active のみ）**。重なり判定は**未実装（11B）** |

今後（段階11B）:

- Hit Box と Hurt Box の重なり判定基盤を作る
- 暫定距離判定を **Box 同士の重なり判定**へ置換する
- ステージ端実装時に Push の壁際補正配分を再検討する

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A / 10B-2 / 10B-3** | Facing 分離、2体共通化、AttackState、横方向 Push Box／すり抜け防止 | **完了** |
| **11A** | Push / Hurt / Hit Box 可視化基盤、論理接地 Y、Game ビュー矩形表示 | **完了** |
| **11B** | Hit Box × Hurt Box の重なり判定基盤、距離判定→Box 判定へ置換 | **次回候補** |
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
- Facing Left 攻撃の実操作確認（2P 入力またはテスト経路追加時）
- 明示的なキャラクター接地原点（Pivot 依存の暫定接地 Y からの置き換え）

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

- 段階1〜**11A**まで到達（Box 可視化基盤完了）
- 最新コミット済み HEAD: `40dc1f4`。段階11A は検証済み・未コミット
- Box は可視化専用。Hit はなお**距離＋向きの暫定**。置換は段階11B
- 論理接地 Y = Transform.y + boxOriginLocalY（Sprite pivot から Awake で一度導出）
- 処理順: **移動 → Push 補正 → Facing → Action/Hit**
- Push Resolver / 距離 Hit は11A で変更していない
- 次は段階11B（**Hit×Hurt 重なり判定 → 距離判定からの置換**）を優先
