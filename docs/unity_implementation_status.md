# Unity 実装状況・次工程（段階1〜14B到達後）

最終更新: 2026-07-27
対象ブランチ: `unity`
最新コミット済み HEAD: **`d3692aa`**（Document participant health and damage completion）
段階14Aまで: **完了・push 済み**
段階14B（Participant 共通 KO 状態・KO 遷移）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜14Bの到達点

| 段階 | 内容 | 状態 |
|---|---|---|
| 1〜8 | SimulationTick / Pause / HUD / HitStop / Action / 入力 / 移動 / Jパンチ見た目 | 実装済み |
| 9 | 距離 Hit（**11Bで Box 重なりへ置換済み**）、1攻撃1Hit、6F HitStop | 判定は11Bで更新 |
| **10A** | Facing と移動入力の分離 | **実装済み・確認済み** |
| **10B-2** | 2体共通 Participant / AttackState / Hit | **実装済み・確認済み** |
| **10B-3** | 横方向 Push Box／すり抜け防止 | **実装済み・確認済み** |
| **11A** | Push / Hurt / Hit Box 可視化 | **実装済み・確認済み** |
| **11B** | Hit×Hurt 重なり判定 | **実装済み・確認済み** |
| **12A** | Participant 共通の被 Hit 状態・HitStun・被 Hit 表示、HUD 分離 | **実装済み・確認済み** |
| **13A** | Participant 共通ノックバック基盤 | **実装済み・確認済み** |
| **13B-1** | ステージ端を考慮した Participant 共通 Push 補正配分 | **実装済み・確認済み** |
| **14A** | Participant 共通 HP・Damage 基盤 | **実装済み・確認済み** |
| **14B** | Participant 共通 KO 状態・KO 遷移 | **実装済み・確認済み** |

既存工程表の段階14（**HP、Damage、KO**）は **14A + 14B で充足・完了**。
Round 終了・勝敗判定は工程表上の段階14には含まれず、後続候補として残す。
次の番号付き工程は既存計画どおり**段階15**。

段階14Aの暫定「0HPでも戦闘継続」は**終了**。段階14Bから 0HP 到達で KO へ遷移する。

### 2.1 責務分担（要約）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterAttackState`** | 攻撃進行の正本 |
| **`DebugFighterHitState`** | 被 Hit / HitStun / ノックバック速度（HP・KO は持たない） |
| **`DebugFighterParticipant`** | **HP 正本** + **KO 正本**（`isKnockedOut`） |
| **`DebugFighterMotor`** | LogicalX・TryMoveLogicalXBy |
| **`DebugFighterPushResolver`** | 等分 Push ＋壁際再配分 |
| **`SimulationTimeState`** | 共有時間・共有 HitStop |
| **`SimulationSession`** | tick 進行、有効 Hit / Damage / KO 遷移 |

### 2.2 段階14A（HP / Damage・維持）

- HP 正本: Participant（maxHitPoints=100、current は実行時）
- J Punch Damage 暫定 10。有効 Hit 1回につき 1 Damage（MarkHit ガード）
- 0 未満 Clamp。Reset で全回復

### 2.3 段階14Bで確定した KO

| 項目 | 内容 |
|---|---|
| **正本** | `DebugFighterParticipant.isKnockedOut`（HitState / Motor / Push 非所有） |
| **API** | `IsKnockedOut` / `TryEnterKnockout` / `ClearKnockoutForReset` / `BuildLifeLabel` |
| **遷移条件** | 有効 Hit → ApplyDamage 後 `CurrentHitPoints <= 0` かつ未 KO |
| **TryEnterKnockout** | 初回のみ true。進行中 Attack は `InterruptByHit` |

**処理順（最後の一撃）**:
`ReceiveHit` → `ApplyDamage` → `TryEnterKnockout` → `MarkHit` → HitStop 開始

ReceiveHit 時点で HitCount / HitStun / Knockback は設定済み。
最後の一撃の Damage / HitCount / HitStop / HitStun / Knockback は通常どおり成立。
KO 後もその最後の一撃の Knockback / HitStun は処理される。HitStun 終了後も KO は解除しない（Reset まで維持）。

**KO 中の制御**:
- 入力移動禁止: `ProcessOneFighterMovement` 先頭（Knockback は別経路で継続）
- 新規攻撃禁止: `TryStartJPunchForParticipant` 先頭
- KO 済み防御者への追加 Hit 拒否: `TryResolveJPunchHit` 冒頭
  → Damage / HitCount / HitStop / HitStun / Knockback 再設定なし
  → 攻撃側 Action は開始・終了し結果は Miss。HUD ラベル `DefenderKO`

**KO 視覚**: 暗いグレー。優先 **KO > HitStun > 通常 Tint**（最後の一撃の HitStun 中も KO 色）。Scene 変更なし。

**HUD**: `P1 Life` / `P2 Life`（Alive / KO）。HP・HitCount・Stun・KB と併記。

**ログ**: 初回のみ `Fighter KO`。Punch hit に `KO=0/1`。Reset に `/KO`。

**Reset**: HP 最大 + KO 解除 + 既存 Attack/HitCount/HitStun/Knockback/HitStop。位置・Facing 維持。

### 2.4 確認済みの挙動（段階14B）

**コンパイル・初期**: Error 0 / Warning 0。両体 HP=100、Life=Alive、HitCount=0。

**9Hit**: HP=10、Life=Alive、HitCount=9、ログ `KO=0`。未 KO。

**10Hit目 KO**（例 CF 1352）: HP=0、Life=KO、HitCount=10。
`Fighter KO` 1回。Punch hit `Damage=10 actual=10 HP=0/100 KO=1 KB=0.180`。
HitStop → HitStun/KB 開始 → 終了後も Life=KO・KO 色維持。

**KO 後の追加攻撃**: Attack started/ended あり、AttackResult=Miss。新規 Punch hit / KO ログなし。HP/HitCount 不変。HitStop/Stun/KB 再設定なし。

**Reset**: 両体 100/Alive、HitCount=0、Stun/KB/HitStop=0。位置例 P1=6.00 / P2=7.00、Facing R/L 維持。

### 2.5 未直接検証（実装済み・P2 実操作未確認）

現在 P2 は Dummy（Neutral）のため、次は**コード経路確認済み／P2 実操作では未検証**:
- KO した Participant 自身の左右入力禁止
- KO した Participant 自身の新規攻撃禁止

将来 2P 入力 / AI 時に共通経路を実操作確認する。検証済みとは書かない。

### 2.6 Stage 13・14A 回帰（段階14B 検証時）

- 1攻撃1Damage、HP Clamp、HitStop / HitStun / Knockback 維持
- 中央 Push・右端 Push 再配分・Push Dist=1.00、Reset 全回復正常

### 2.7 未実装（段階14計画外・後続候補）

- Round 終了、勝敗判定、WIN/LOSE、KO 後時間停止、リザルト、ラウンド再開始
- KO 専用アニメ、Down 物理、複数ラウンド、タイマー、HP バー、Guard
- Character 別 KO、KO 演出制御
- 壁バウンド等（工程番号なし残課題）
- 攻撃データ化（段階15）

### 2.8 相打ち・キャラ差し替え

- 相打ち: 両方向判定の土台のみ。P2 Neutral のため実動作確認は未実施
- キャラ差し替え・複数 Hurt/Hit Box: 未実装

---

## 3. Facing / Push（維持）

Facing 分離・壁際 Push 再配分は実装済み。Push は HP / KO / KB 速度に触れない。

---

## 4. 判定箱方針（維持）

Push / Hurt / Hit 可視化（11A）と Hit×Hurt 重なり判定（11B）は完了。

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A〜13B-1** | Facing〜壁際 Push 再配分 | **完了** |
| **14A** | Participant 共通 HP・Damage 基盤 | **完了** |
| **14B** | Participant 共通 KO 状態・KO 遷移 | **完了** |
| **14**（全体） | HP、Damage、KO（工程表どおり） | **完了**（14A+14Bで充足） |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | **次回候補** |

その後の候補（順不同・未着手）:

- Round 終了、勝敗、Down、HP バー、Guard
- ノックバック壁到達時の速度停止、壁バウンド、壁やられ、Corner
- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- Animation 本接続
- 2P 入力 / CPU（KO 中移動・攻撃禁止の実操作確認、P1被Hit・左方向KB・Facing Left 攻撃を含む）
- 複数 Hurt / Hit Box、キャラ固有データ化

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

- 段階1〜**14B**まで到達。工程表の段階14（HP/Damage/KO）は完了
- 最新コミット済み HEAD: `d3692aa`。段階14B は検証済み・未コミット
- HP 0 で一度だけ KO。最後の一撃の HitStop/Stun/KB は維持。Reset まで KO 解除しない
- 次は既存計画どおり段階**15**（**攻撃データ化**）
