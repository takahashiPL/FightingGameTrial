# Unity 実装状況・次工程（段階1〜15到達後）

最終更新: 2026-07-27
対象ブランチ: `unity`
最新コミット済み HEAD: **`e157b7d`**（Add Unity GC learning guide）
段階14全体（14A+14B）・段階15（攻撃データ化）: **完了・push 済み**
GC-1（固定 Help 毎 Frame 再構築停止）: **実装・Editor 実測済み。ドキュメント反映時点ではコード未コミットの場合あり**

このファイルは、Unity 側の**実装済み / 暫定 / 未実装 / 次回候補 / 正式方針**を混同せずに追うための正本です。
ゲーム仕様そのものの正本は引き続き `docs/rules.md` です。
「GC-1」等は正式 Stage 番号ではなく、学習・計測用の補助区分である。

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

## 2. 段階1〜15の到達点

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
| **15** | J Punch 攻撃データ化（固定値の参照元整理） | **実装済み・確認済み** |

既存工程表の段階14（**HP、Damage、KO**）は **14A + 14B で充足・完了**。
既存工程表の段階15（**攻撃データ化**）は **完了**（ScriptableObject 化は見送り。コード内の読み取り専用データ）。
Round 終了・勝敗判定は工程表上の段階14/15には含まれず、後続候補として残す。

段階14Aの暫定「0HPでも戦闘継続」は**終了**。段階14Bから 0HP 到達で KO へ遷移する。

### 2.1 責務分担（要約・段階15後）

| 入れ物 | 担当 |
|---|---|
| **`DebugAttackData` / `DebugAttackData.JPunch`** | **J Punch 攻撃設定値の正本**（進行状態は持たない） |
| **`DebugFighterAttackState`** | 攻撃進行の正本（ActionFrame / HasCurrentJPunchHit 等） |
| **`DebugFighterHitState`** | 被 Hit / HitStun / ノックバック速度（HP・KO・攻撃データは持たない） |
| **`DebugFighterParticipant`** | **HP 正本** + **KO 正本**（`isKnockedOut`）。local→world Hit Box 変換・Facing 反転 |
| **`DebugFighterMotor`** | LogicalX・TryMoveLogicalXBy（攻撃データ非所有） |
| **`DebugFighterPushResolver`** | 等分 Push ＋壁際再配分（攻撃データ非所有） |
| **`DebugPunchHitResolver`** | world Hit×Hurt 重なり判定（攻撃全体の設定正本にはしない） |
| **`SimulationTimeState`** | 共有時間・共有 HitStop |
| **`SimulationSession`** | tick 進行、攻撃開始/終了、Hit 適用、HitStop 開始、KO 接続（数値の正本にはしない） |

### 2.2 段階14A（HP / Damage・維持）

- HP 正本: Participant（maxHitPoints=100、current は実行時）
- J Punch Damage は段階15以降 **攻撃データ**（値は従来どおり 10）。有効 Hit 1回につき 1 Damage（MarkHit ガード）
- 0 未満 Clamp。Reset で全回復

### 2.3 段階14Bで確定した KO（維持）

| 項目 | 内容 |
|---|---|
| **正本** | `DebugFighterParticipant.isKnockedOut`（HitState / Motor / Push 非所有） |
| **API** | `IsKnockedOut` / `TryEnterKnockout` / `ClearKnockoutForReset` / `BuildLifeLabel` |
| **遷移条件** | 有効 Hit → ApplyDamage 後 `CurrentHitPoints <= 0` かつ未 KO |
| **TryEnterKnockout** | 初回のみ true。進行中 Attack は `InterruptByHit` |

**処理順（最後の一撃・段階15でも維持）**:
`ReceiveHit` → `ApplyDamage` → `TryEnterKnockout` → `MarkHit` → HitStop 開始

ReceiveHit 時点で HitCount / HitStun / Knockback は設定済み。
最後の一撃の Damage / HitCount / HitStop / HitStun / Knockback は通常どおり成立。
KO 後もその最後の一撃の Knockback / HitStun は処理される。HitStun 終了後も KO は解除しない（Reset まで維持）。

**KO 中の制御**（戦闘処理は段階15で変更なし）:
- 入力移動禁止: `ProcessOneFighterMovement` 先頭（Knockback は別経路で継続）
- 新規攻撃禁止: `TryStartJPunchForParticipant` 先頭
- KO 済み防御者への追加 Hit 拒否: `TryResolveJPunchHit` 冒頭
  → Damage / HitCount / HitStop / HitStun / Knockback 再設定なし
  → 攻撃側 Action は開始・終了し結果は Miss。HUD ラベル `DefenderKO`

**KO 視覚（履歴と現在仕様）**:
- 段階14B 時点の記録: 優先 **KO > HitStun > 通常 Tint**（最後の一撃の HitStun 中も KO 色）。Scene 変更なし。
- **段階15 検証中の回帰修正後（現在仕様の正本）**: 優先 **HitStun 被 Hit 表示 > KO 暗色 > 通常 Tint**。
  - `ApplyDisplayColor` のみ変更。`hitState.IsInHitStun` を既存どおり使用。
  - KO 状態（`isKnockedOut`）の開始時点・`TryEnterKnockout` 処理順は変更していない。
  - 最後の一撃でも赤表示のあと HitStun 終了で KO 暗色へ移行。KO 後追加 Hit では赤にならない。

**HUD**: `P1 Life` / `P2 Life`（Alive / KO）。HP・HitCount・Stun・KB と併記。

**ログ**: 初回のみ `Fighter KO`。Punch hit に `KO=0/1`。Reset に `/KO`。

**Reset**: HP 最大 + KO 解除 + 既存 Attack/HitCount/HitStun/Knockback/HitStop。位置・Facing 維持。

### 2.4 段階15で確定した攻撃データ化

**目的**: J Punch 固有の固定値を Session 等へ散在させず、1つの読み取り専用攻撃データから参照する。

| 項目 | 内容 |
|---|---|
| **型** | `Game/Assets/Scripts/Combat/DebugAttackData.cs` |
| **性質** | MonoBehaviour ではない / ScriptableObject ではない |
| **保持内容** | 攻撃の設定値のみ（実行中の攻撃進行は持たない） |
| **正本** | `static readonly DebugAttackData.JPunch`（`CreateJPunch()` で1回生成） |
| **Session 参照** | `private static readonly DebugAttackData JPunchData` |
| **生成頻度** | 毎 Frame / 毎 Hit で `new` しない |

**`DebugAttackData.JPunch` の値**:

| 項目 | 値 |
|---|---|
| AttackId | JPunch |
| StartupFrames | 3 |
| ActiveFrames | 3 |
| RecoveryFrames | 6 |
| TotalFrames | 12 |
| Damage | 10 |
| HitStopFrames | 6 |
| HitStunFrames | 12 |
| KnockbackInitialVelocityX | 0.180 |
| KnockbackDecelerationPerCombatFrame | 0.015 |
| Hit Box local CenterX / CenterY | 0.75 / 1.25 |
| Hit Box HalfWidth / HalfHeight | 0.55 / 0.35 |

**Frame 境界（既存実装を移しただけ。境界自体は変更なし）**:

| 区間 | ActionFrame |
|---|---|
| Startup | 1〜3 |
| Active | 4〜6 |
| Recovery | 7〜12 |
| 攻撃終了 | `ActionFrame >= TotalFrames`（12） |

**データ参照へ置換した内容**:
Startup / Active / Recovery 判定、TotalFrame と攻撃終了、Damage、HitStop、HitStun、Knockback 初速・減速、local Hit Box、Attack started ログ、Punch hit の値、HUD の JPunch Data。

**1攻撃1Hit**: 従来どおり `HasCurrentJPunchHit` + `MarkHit`。Active 3Frame でも 1攻撃1Damage。処理変更なし。

**ScriptableObject 化を見送った理由**:
- 今回は固定値の参照元整理が目的
- Inspector 編集や Asset 依存を増やす前段階
- 複数攻撃・Character 別データがまだない
- SO 化は後続で必要性を判断する
- 当面はコード内の不変データとして保持

**段階15で実装していないこと**:
ScriptableObject 化、Inspector 編集、Character 別攻撃データ、複数攻撃、弱/中/強、技コマンド、コンボ、Guard、Counter Hit、攻撃キャンセル、アニメーションイベント、JSON/CSV、Round/勝敗/Result、KO 専用アニメ、HP バー。

### 2.5 HUD / ログ（段階15）

**HUD（攻撃データから生成。固定値の再記述なし）**:
`JPunch Data : S/A/R 3/3/6 Dmg 10 HStop 6 HStun 12 KB 0.180`

- 初回配置は状態 HUD 末尾付近にあり、`Truncate` と高さ制限で非表示だった
- 修正（HUD のみ・Scene 変更なし）:
  - `JPunch Data` 行を `P2 KB Vx/Act` 直後へ移動
  - `HelpBlockHeightPixels` 120→92（ランタイム `EnsureSplitHudLayout`）
- 操作説明の欠け・重なりなしを確認

**ログ**:
- Attack started: `[FightDebug] Attack started slot=P1 attack=JPunch S/A/R=3/3/6 Damage=10 HitStop=6 HitStun=12 KB=0.180`
- Attack ended: `[FightDebug] Attack ended slot=P1 attack=JPunch`
- Punch hit: 既存項目を維持し、値は `DebugAttackData.JPunch` から参照

### 2.6 確認済みの挙動（段階15）

**A. コンパイル**: Error 0。Warning 1（既存 CS0618: `TMP_Text.enableWordWrapping`。Stage 15 由来の新規 Warning なし）。

**B. 初期**: 両体 HP=100、Life=Alive、HitCount=0、Stun=0、KB=0、HitStop=0。JPunch Data HUD 表示確認。

**C. 遠距離 Miss**: Attack started/ended、`attack=JPunch`、S/A/R=3/3/6。P2 HP/HitCount 不変、HitStop=0、AttackResult=Miss。

**D. 1Hit**: Damage=10、HP=90、HitCount=1、KO=0、KB=0.180。HitStop 6Frame（SimulationTick と CombatFrame の差で確認）。Active 3Frame でも 1Hit のみ。HitStun 終了後 Idle、KB→0。

**E. Frame 境界**: Startup 1〜3 / Active 4〜6 / Recovery 7〜12。Recovery 中 AF7/8/10 ログ確認。HitStop 中 ActionFrame 停止。AF>=12 で終了。既存境界維持。

**F. KO 回帰**: 9Hit で HP=10 Alive HitCount=9。10Hit で HP=0 Life=KO HitCount=10、KO ログ1回。最後の一撃の HitStop/Stun/KB 維持。KO 後暗色維持。

**G. KO 表示回帰修正**: 最終 Hit で赤→その後 KO 暗色を目視確認。通常 Hit の赤維持。KO 後追加 Hit は赤なし。Reset で Alive 色。戦闘処理・Scene/Prefab 変更なし。

**H. Reset**: 両体 100/Alive、HitCount=0、Stun/KB/HitStop=0、AttackResult=None。位置と Facing 維持。

**I. Stage 13・14 回帰**: 中央 Push・右端再配分・Dist=1.00・Clamp・HP Clamp・KO 追加 Hit 拒否・Reset 維持。

### 2.7 未直接検証（実装済み・P2 実操作未確認）

現在 P2 は Dummy（Neutral）のため、次は**コード経路確認済み／P2 実操作では未検証**:
- KO した Participant 自身の左右入力禁止
- KO した Participant 自身の新規攻撃禁止

将来 2P 入力 / AI 時に共通経路を実操作確認する。検証済みとは書かない。

### 2.8 段階14B 時点の確認メモ（履歴・上書きしない）

段階14B 検証時: 10Hit で KO、追加攻撃は Miss、Reset で Alive/100、Stage 13/14A 回帰正常。
当時の Visual 優先は **KO > HitStun**（段階15 で現在仕様へ修正。§2.3 参照）。

### 2.9 未実装（段階15計画外・後続候補）

- Round 終了、勝敗判定、WIN/LOSE、KO 後時間停止、リザルト、ラウンド再開始
- KO 専用アニメ、Down 物理、複数ラウンド、タイマー、HP バー、Guard
- Character 別 KO / 攻撃データ、KO 演出制御
- 壁バウンド等（工程番号なし残課題）
- 攻撃データの ScriptableObject 化 / Inspector 編集 / JSON・CSV
- 複数攻撃、弱/中/強、技コマンド、コンボ、Cancel、Counter Hit

### 2.10 相打ち・キャラ差し替え

- 相打ち: 両方向判定の土台のみ。P2 Neutral のため実動作確認は未実施
- キャラ差し替え・複数 Hurt/Hit Box: 未実装

---

## 3. Facing / Push（維持）

Facing 分離・壁際 Push 再配分は実装済み。Push は HP / KO / KB 速度に触れない。攻撃データも持たない。

---

## 4. 判定箱方針（維持）

Push / Hurt / Hit 可視化（11A）と Hit×Hurt 重なり判定（11B）は完了。
段階15: local Hit Box 定義は攻撃データ、world 変換・Facing 反転は Participant、重なりは PunchHitResolver。

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A〜13B-1** | Facing〜壁際 Push 再配分 | **完了** |
| **14A** | Participant 共通 HP・Damage 基盤 | **完了** |
| **14B** | Participant 共通 KO 状態・KO 遷移 | **完了** |
| **14**（全体） | HP、Damage、KO（工程表どおり） | **完了**（14A+14Bで充足） |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | **完了**（コード内不変データ。SO 化は見送り） |

その後の候補（順不同・未着手・既存計画どおり。新工程番号は作らない）:

- Round 終了、勝敗、Down、HP バー、Guard
- ノックバック壁到達時の速度停止、壁バウンド、壁やられ、Corner
- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- Animation 本接続
- 攻撃データの ScriptableObject 化（必要になったとき）
- 2P 入力 / CPU（KO 中移動・攻撃禁止の実操作確認、P1被Hit・左方向KB・Facing Left 攻撃を含む）
- 複数 Hurt / Hit Box、キャラ固有データ化

---

## 5.1 GC 学習・計測の補助改善（正式 Stage ではない）

Round / Guard / 複数攻撃などの**機能 Stage とは別枠**。番号「GC-1」は便宜名。

### GC-1 Fixed Help Text Allocation Reduction

| 項目 | 内容 |
|---|---|
| **実装** | `DebugHudView`: Update から固定 Help の毎 Frame 再構築・再代入を削除。設定は Awake → EnsureSplitHudLayout で1回 |
| **非対象** | Status HUD（`BuildStatusHudText`）は従来どおり毎 Frame |
| **実測条件** | Unity 6.3 LTS / Editor Play Mode / FightDebugScene / 通常待機 |
| **変更前** | フレーム全体 約18.0 KB・69 alloc。`DebugHudView.Update` 約17.8 KB。GC.Collect 0.000 ms |
| **変更後** | `DebugHudView.Update` 約17.2 KB（約0.6 KB・約3.4%削減）。全体 KB / alloc 回数 / GC.Collect は**未記録** |
| **回帰** | Help 表示・Status HUD 表示は維持。Compile Error なし。既知 CS0618（enableWordWrapping）1件 |
| **未実装・未計測** | Status HUD 本体の最適化（**GC-2**: 便宜名・仕様未確定）、Development Build 測定、連結/`ToString`/TMP 内訳 |

詳細な学習用記録は `docs/component_and_scene_guide.md` §16.13。

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

- 段階1〜**15**まで到達。工程表の段階14（HP/Damage/KO）と段階15（攻撃データ化）は完了
- 最新コミット済み HEAD: `e157b7d`（GC 学習資料追加）。GC-1（Help 毎 Frame 停止）は補助改善・正式 Stage ではない
- J Punch 設定正本は `DebugAttackData.JPunch`。Session は進行と適用のみ
- KO 処理順・1攻撃1Hit・Frame 境界は維持。Visual 優先は HitStun 赤 > KO 暗色 > 通常 Tint
- 次は既存計画の後続候補（Round/勝敗、Guard、複数攻撃、SO 化の要否判断など。順不同）。GC-2 は未確定
