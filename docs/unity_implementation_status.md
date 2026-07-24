# Unity 実装状況・次工程（段階1〜11B到達後）

最終更新: 2026-07-24
対象ブランチ: `unity`
最新コミット済み HEAD: **`ce64bd4`**（Document fighter box visualization completion）
段階11Aまで: **完了・push 済み**
段階11B（Hit×Hurt 重なり判定）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜11Bの到達点

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
| 9 | 距離＋向きの暫定 Hit（**段階11Bで Box 重なりへ置換済み**）、1攻撃1Hit、Hit時6F HitStop | **判定は11Bで更新** |
| **10A** | Facing と移動入力の分離、相手向き合い Facing、同X付近は直前 Facing 維持 | **実装済み・確認済み** |
| **10B-2** | 2体共通 Participant、AttackState、attacker/defender Hit、被弾記録 | **実装済み・確認済み** |
| **10B-3** | Participant 共通の横方向 Push Box／すり抜け防止 | **実装済み・確認済み** |
| **11A** | Push / Hurt / Hit Box 可視化基盤（`DebugBox2D`、LineRenderer、論理接地 Y） | **実装済み・確認済み** |
| **11B** | Hit Box × Hurt Box 重なり判定、距離判定の削除・置換 | **実装済み・確認済み** |

段階11（単一 Push / Hurt / Hit Box の可視化＋重なり判定基盤）は **11A+11B で完了**。
複数 Hurt/Hit Box、キャラ固有データ化、攻撃データ SO 化は未実装。

### 2.1 段階10B-2で確定した責務分担（維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterParticipant`** | 参加枠（P1/P2）。Motor / Visual / Tint / 入力種別 / Opponent / PushBoxHalfWidth / Box ローカル定義 |
| **`DebugFighterAttackState`** | **攻撃状態の正本**（ActionFrame、IsActionPlaying、IsJPunchAttack、HasCurrentJPunchHit、LastAttackResult、攻撃入力立ち上がり） |
| **Participant 被弾記録** | HitCount / WasHitThisCombatFrame / LastHitCombatFrame（`ReceiveHit`） |
| **`SimulationTimeState`** | **共有時間状態のみ**（SimulationTick、CombatFrame、Pause、HitStop、Status、CurrentInput） |
| **`SimulationSession`** | tick 進行、移動→Push→Facing→Action→Hit、`TryResolveJPunchHit(attacker, defender)` |

### 2.2 段階10B-3で確定した Push Box 責務（維持・11Bで未変更）

| 入れ物 | 担当 |
|---|---|
| **`pushBoxHalfWidth`** | 横方向 Push 半幅の判定正本（既定 0.5f） |
| **`DebugFighterPushResolver`** | Participant 同士の横方向重なり解消 |
| **`SimulationSession`** | **移動 → Push 補正 → Facing → ActionFrame → Hit** |

等分押し分けは暫定。壁際配分はステージ境界実装時に再検討。

### 2.3 段階11Aで確定した Box 可視化責務（維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugBox2D`** | CenterX/Y、HalfWidth/Height、IsActive、Min/Max、GetCorners、**Overlaps（11B）** |
| **`DebugFighterParticipant`** | Push / Hurt / J Punch Hit のローカル定義、World 座標、論理接地 Y |
| **`DebugFighterBoxView`** | LineRenderer 矩形表示 |

論理接地 Y: `boxOriginLocalY = -(sprite.pivot.y / PPU)`（Awake で一度）。
`LogicalGroundY = Transform.y + boxOriginLocalY`。`WorldCenterY = LogicalGroundY + LocalCenterY`。

### 2.4 段階11Bで確定した Hit 判定責務

| 入れ物 | 担当 |
|---|---|
| **`DebugBox2D.Overlaps`** | 軸平行矩形の重なり（**境界接触も Hit**。大きな epsilon なし） |
| **`DebugPunchHitResolver`** | Active・未Hit・重なりの純関数評価（距離判定は削除） |
| **`SimulationSession.TryResolveJPunchHit`** | attacker/defender 共通経路。成立時 MarkHit / ReceiveHit / 6F HitStop |

判定の要点:

- **距離判定は削除済み**。`attackRange` も削除。Facing 方向付き距離の fallback なし
- 可視化と同じ `EvaluateWorldHitBox` / `EvaluateWorldHurtBox` を実判定でも使用
- Active は既存 ActionFrame **4〜6**（Hit Box `IsActive`）。独自フレーム範囲は持たない
- Unity Physics / Collider / Rigidbody は未使用（固定フレーム内の論理判定）
- Push Resolver、Facing、Box サイズ、Local Center は変更していない

Hit 成立条件（すべて満たすとき）:

1. attacker / defender が有効、かつ attacker ≠ defender
2. Jパンチ再生中
3. attacker の Hit Box が Active
4. defender の Hurt Box が Active
5. Box が重なる（境界接触含む）
6. この攻撃で未 Hit（`HasCurrentJPunchHit`）

1攻撃1Hit: `HasCurrentJPunchHit` + `MarkHit`。攻撃終了まで Hit しなければ `EndJPunch` で Miss。

処理順（Combat・HitStop 外）:

入力 → 攻撃開始 → 移動 → Push 補正 → Facing → ActionFrame 進行 → **Hit/Hurt 重なり判定** → Visual → 攻撃終了

HUD: `BoxOverlap`、`HitCheck`（Inactive / NoOverlap / Hit / AlreadyHit）

### 2.5 確認済みの挙動（段階11Bまで）

遠距離 J:

- Active 中も赤 Hit と緑 Hurt が重ならない → `BoxOverlap=0` / `HitCheck=NoOverlap` → `AttackResult=Miss`
- P2HitCount 増えない、HitStop なし

近距離 J:

- Box 重なりで Hit（例: CombatFrame 985。HitBox/HurtBox の X/Y 両軸で重なり）
- P2HitCount+1、PunchHitDone=1、6F HitStop、同一攻撃で二重 Hit なし
- Recovery で赤 Hit Box 消失、`HitCheck=Inactive` へ戻る

その他:

- Pause / Step で Active・HitStop・Recovery を確認
- Push Dist=1.00 維持、既存 Push 挙動正常
- Console Error 0 / Warning 0

### 2.6 未検証・将来注意（段階11Bは完了扱い）

- Facing Left 攻撃の実操作表示は**引き続き将来確認項目**（P1 のみ攻撃・Push で位置交換しにくい）
- 接地 Y はデバッグ Sprite Pivot から導出。本番キャラでは明示的接地原点へ置き換え候補
- 複数 Hurt Box / 複数 Hit Box、キャラ固有 Box データ、攻撃データ SO 化は**未実装**

### 2.7 相打ちについて（未完了の実動作確認）

- **実装済み**: 同一 CombatFrame で両方向 Hit 判定する土台
- **未実施**: P2 入力や AI による実際の相打ち確認（P2 は現状 Neutral）

### 2.8 キャラクター差し替え構造の現状

入力源差し替え土台はある。キャラごとの画像・速度・体格・Box データ・攻撃性能の差し替え、攻撃データ SO 化は**未実装**（段階15 以降）。

---

## 3. Facing と移動入力の分離（段階10A・実装済み）

### 3.1 実装済みの正式寄せ

1. 移動方向と Facing を分離（Left/Right＝ワールド X のみ）
2. 両者とも基本的に相手と向き合う
3. Facing 確定は Push 補正後・Hit 判定前
4. HitStop / Pause 中は Facing も更新しない

対応仕様: `docs/rules.md` §2.3。

### 3.2 段階10の Push Box（段階10B-3・完了）

横方向 Push / すり抜け防止は実装済み。等分押し分けは暫定。

---

## 4. 判定箱方針

| 箱 | 役割 | 現状 |
|---|---|---|
| **Push Box** | 重なり防止・すり抜け防止 | **実装済み（横方向・等分分離の暫定）**。可視化済み（11A） |
| **Hurt Box** | 被弾判定 | **可視化済み（11A）**。**重なり判定の防御側として使用中（11B）** |
| **Hit Box** | 攻撃判定（Active のみ） | **可視化済み（11A）**。**重なり判定の攻撃側として使用中（11B）** |

距離判定からの置換は**完了**。残課題は複数 Box・データ化・ステージ端の壁際 Push 配分など。

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A / 10B-2 / 10B-3** | Facing 分離、2体共通化、AttackState、横方向 Push Box | **完了** |
| **11A** | Push / Hurt / Hit Box 可視化基盤、論理接地 Y | **完了** |
| **11B** | Hit×Hurt 重なり判定、距離判定削除・置換 | **完了** |
| **12** | 被 Hit 状態、HitStun、被 Hit 表示 | **次回候補**（既存計画の名称・内容） |
| **13** | ノックバック、押し戻し、ステージ端（壁際 Push 配分の再検討を含む） | 未実装 |
| **14** | HP、Damage、KO | 未実装 |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | 未実装 |

その後の候補（順不同・未着手）:

- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- ガード、コンボ
- Animation 本接続
- 2P 入力 / CPU、ラウンド進行
- Facing Left 攻撃の実操作確認
- 明示的なキャラクター接地原点
- 複数 Hurt / Hit Box

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

- 段階1〜**11B**まで到達（単一 Push/Hurt/Hit Box の可視化＋重なり判定基盤が完了）
- 最新コミット済み HEAD: `ce64bd4`。段階11B は検証済み・未コミット
- Hit 正本は **Hit Box × Hurt Box の Overlaps**（境界接触含む）。距離判定は削除済み
- 可視化と実判定は同じ `EvaluateWorldHitBox` / `EvaluateWorldHurtBox`
- 処理順: 移動 → Push → Facing → ActionFrame → **Box Hit** → Visual
- 次は既存計画どおり段階12（**被 Hit 状態、HitStun、被 Hit 表示**）
