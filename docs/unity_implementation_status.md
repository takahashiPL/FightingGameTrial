# Unity 実装状況・次工程（段階1〜14A到達後）

最終更新: 2026-07-27
対象ブランチ: `unity`
最新コミット済み HEAD: **`0230f2b`**（Document stage edge push redistribution）
段階13B-1まで: **完了・push 済み**
段階14A（Participant 共通 HP・Damage 基盤）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜14Aの到達点

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

段階14全体（HP・Damage・**KO**）は**未完了**。
14A で HP / Damage 基盤は完了。次は既存計画の段階14残作業として **段階14B（KO 状態・KO 遷移）**。

### 2.1 責務分担（要約）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterAttackState`** | 攻撃進行の正本 |
| **`DebugFighterHitState`** | 被 Hit / HitStun / ノックバック速度（**HP は持たない**） |
| **`DebugFighterParticipant`** | **HP 正本**（max / current、ApplyDamage、全回復） |
| **`DebugFighterMotor`** | LogicalX・TryMoveLogicalXBy |
| **`DebugFighterPushResolver`** | 等分 Push ＋壁際再配分 |
| **`SimulationTimeState`** | 共有時間・共有 HitStop |
| **`SimulationSession`** | tick 進行、有効 Hit 時の Damage 適用 |

### 2.2 段階14Aで確定した HP / Damage

| 項目 | 内容 |
|---|---|
| **正本** | `DebugFighterParticipant`（HitState には持たせない） |
| **`maxHitPoints`** | SerializeField 既定 **100**。Scene 変更なしで既存 Participant へ反映 |
| **`currentHitPoints`** | 非 Serialize の実行時状態。Awake / Reset で最大へ |
| **API** | `MaxHitPoints` / `CurrentHitPoints` / `IsHitPointsDepleted` / `ApplyDamage` / `RestoreHitPointsToMaximum` |
| **J Punch Damage** | Session 暫定定数 **10**（攻撃データ化前） |

**ApplyDamage**:
- `damage <= 0` → 変化なし・戻り値 0
- `current = max(0, current - damage)`（0 未満にしない）
- 戻り値 = 実際に減った量（例: HP5 に Damage10 → 残0、戻り値5）

**Damage 接続タイミング**（有効 Hit 成立時のみ）:
`ReceiveHit` → **`ApplyDamage`** → `MarkHit` → HitStop 開始

Miss / AlreadyHit / NoOverlap では Damage なし。
**1攻撃1Damage** の根拠: 既存 `HasCurrentJPunchHit` / `MarkHit`（1攻撃1Hit）と同じガード。HitCount と Damage 回数が一致。

**0HP 暫定仕様（段階14A 限定）**:
- HP は 0 で Clamp。0 未満にならない
- **KO へ遷移しない**
- 0HP でも移動・被 Hit・HitStop・HitStun・Knockback は継続
- KO / Round / 勝敗は後続（段階14B）

**Reset（R）**: 既存 Attack / HitCount / HitStun / Knockback / HitStop に加え **HP を最大へ全回復**。位置・Facing は維持。

**HUD**: 左上状態領域に `P1 HP : current / max`、`P2 HP : current / max`。操作説明分離は維持。

**ログ**: Punch hit に `Damage` / `actual` / `HP=残/最大` / KB。Reset は `(Attack/HitStun/Knockback/HitStop/HP)`。

### 2.3 HitStop / HitStun / ノックバック / Push（維持）

HitStop=6F、HitStun=12CF、KB 初速0.18／減速0.015。壁際 Push 再配分（13B-1）維持。
処理順: 入力移動 → ノックバック → Push → Facing → Action → **Hit（内で Damage）** → Visual → HitStun。

### 2.4 確認済みの挙動（段階14A）

**コンパイル・初期**: Error 0 / Warning 0。P1/P2 HP=100/100。Scene 変更なしで既定100。

**1回目 Hit**（例 CF 342）: Damage=10 actual=10 HP=90/100 KB=0.180。最終 P2 HP=90、HitCount=1、Stun=0/Idle、KB=0。同一攻撃中の追加 Damage なし。

**2回目の別攻撃**（例 CF 960）: HP=80/100、HitCount=2。HitCount と Damage 回数が一致。

**0HP Clamp**: 10Hit目で HP=0/100（actual=10）。11Hit目以降 actual=0・HP=0維持。HitCount は増加し得る。0HP でも HitStop/HitStun/KB 継続（KO 未実装）。

**Reset**: HP 0・HitCount=13 → 両体 100/100、HitCount=0、Stun/KB/HitStop=0。位置例 P1=6.00 / P2=7.00、Facing P1=R / P2=L 維持。

### 2.5 Stage 13 回帰（段階14A 検証時）

- 中央 Push 正常
- HitStop 中停止、HitStop 後 Knockback 開始・減速・終了
- 右端: P1=6.00 / P2=7.00、Push Dist=1.00、`Push wall redistribute` 確認
- 段階13B-1を壊していない

### 2.6 未実装（段階14Aは完了、段階14全体は未完了）

- **KO 状態・KO 遷移・演出**（段階14B 候補）
- Round 終了、勝敗判定、入力停止、Down
- HP バー、Guard、Chip、Counter、Combo 補正
- Character 別 HP、攻撃別 Damage データ、SO 化
- 壁バウンド／壁やられ／KB壁停止（工程番号なし残課題）

### 2.7 相打ち・キャラ差し替え

- 相打ち: 両方向判定の土台のみ。P2 Neutral のため実動作確認は未実施
- キャラ差し替え・複数 Hurt/Hit Box: 未実装

---

## 3. Facing / Push（維持）

Facing 分離・壁際 Push 再配分は実装済み。Push は HP / KB 速度に触れない。

---

## 4. 判定箱方針（維持）

Push / Hurt / Hit 可視化（11A）と Hit×Hurt 重なり判定（11B）は完了。

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A〜13B-1** | Facing〜壁際 Push 再配分 | **完了** |
| **14A** | Participant 共通 HP・Damage 基盤 | **完了** |
| **14B** | KO 状態・KO 遷移（段階14の残作業） | **次回候補** |
| **14**（全体） | HP、Damage、KO | **未完了**（14Aのみ充足） |
| **15** | 攻撃データ化 | 未実装 |

その後の候補（順不同・未着手）:

- Round 終了、勝敗、Down、HP バー、Guard
- ノックバック壁到達時の速度停止、壁バウンド、壁やられ、Corner
- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- Animation 本接続
- 2P 入力 / CPU（P1被Hit・左方向KB・Facing Left 攻撃の実操作確認を含む）
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

- 段階1〜**14A**まで到達（HP/Damage 基盤完了。段階14全体は未完了）
- 最新コミット済み HEAD: `0230f2b`。段階14A は検証済み・未コミット
- HP 正本は Participant。有効 Hit 1回につき Damage 1回。0HP でも 14A では KO せず戦闘継続（暫定）
- 次は既存計画どおり段階**14B**（**KO 状態・KO 遷移**）
