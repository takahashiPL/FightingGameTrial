# Unity 実装状況・次工程（段階1〜15到達後）

最終更新: 2026-07-28
対象ブランチ: `unity`
最新コミット済み HEAD: **`5bd43f7`**（Reset training positions and facing）
段階14全体（14A+14B）・段階15（攻撃データ化）: **完了・push 済み**
GC-1 / GC-2（補助改善・正式 Stage ではない）: **完了・push 済み**
Training Reset 位置・向き復帰: **実装・Editor 確認済み**（正式 Stage 番号なし）
最小 Visual State（Idle / WalkF / WalkB / Attack）: **実装・Editor 一部確認済み**（正式 Stage 番号なし。Docs 反映時点ではコード未コミットの場合あり）
正式な次工程番号: **未定義**（新 Stage 番号は作らない）

このファイルは、Unity 側の**実装済み / 暫定 / 未実装 / 次回候補 / 正式方針**を混同せずに追うための正本です。
ゲーム仕様そのものの正本は引き続き `docs/rules.md` です。
「GC-1」「GC-2」等は正式 Stage 番号ではなく、学習・計測用の補助区分である。

---

## 1. 区分の読み方

| 区分 | 意味 |
|---|---|
| **実装済み・確認済み** | FightDebugScene で動作確認済み |
| **暫定実装** | 動くが、正式仕様へ置き換える前提 |
| **正式方針（未実装）** | 今後そうする、と決めた設計。コード未反映 |
| **未実装** | まだ作っていない |
| **次回候補** | 工程上の次ステップ（番号未割当含む） |

---

## 1.1 モード位置づけ（方針確定・実装は段階15到達時点）

`FightDebugScene` は**対戦モードではない**。**正式な練習・検証モード**として扱う。

| 項目 | 状態 |
|---|---|
| 詳細 HUD・戦闘ログ・判定／KO 確認・R Reset | **実装済み・維持** |
| KO 後の WIN/LOSE・ラウンド終了への進行 | **しない**（方針。現コードも対戦進行なし） |
| KO 状態の観察 | **できる**（確認済み） |
| 対戦モード（Round / 勝敗 / タイマー等） | **未実装**。FightDebugScene へ混在させない |

### 責務表（方針）

| 層 | 担当 | 備考 |
|---|---|---|
| **共通戦闘コア** | 入力サンプリング、左右移動、向き、Push/Hurt/Hit Box、S/A/R、Damage、HitStop、HitStun、Knockback、HP、KO 判定、KO 後の追加被弾拒否、攻撃データ、基本戦闘状態、戦闘状態→見た目同期 | **「KO が成立した」まで**。練習／対戦で共通化する方針 |
| **練習モード（現 FightDebugScene）** | 勝敗なし、ラウンドなし、時間制限なし、WIN/LOSE なし、Training Reset（R）、詳細 Debug HUD、判定・座標・攻撃・HitStop 等の観察 | KO 後は観察継続。将来候補: ダミー回復、自動回復、ガード設定、行動記録、判定表示、フレーム表示など |
| **対戦モード（将来・未実装）** | ラウンド開始・終了、勝敗、WIN/LOSE、ラウンド数、タイマー、READY/FIGHT、次ラウンド、Match 終了、リザルト、対戦用 HUD、開始前・終了後の入力制限 | 別 Scene / Controller / HUD を想定。現 Scene に混在させない |

共通側は KO 成立という**戦闘結果**まで。KO 後に何をするかは**モード側**が決める。

### Training Reset（実装済み・Editor 確認済み）

練習モードの正式な **Training Reset**（R）。従来は戦闘状態のみ戻し、**位置・Facing は維持**していた。今回、論理位置と Facing の初期復帰を追加した（正式 Stage 番号は付けない）。

**R で初期化する項目**

- P1 / P2 論理位置 X、向き（位置関係から再計算）
- HP、KO、HitCount、HitStun、Knockback
- 攻撃状態、HitStop、AttackResult
- 入力の押下残り（既存経路）

**位置の戻し方**: Transform を Session から直接書き換えない。Motor が保持する **論理座標**を Scene 開始時の初期値へ戻し、`SetLogicalX` 経由でクランプと Transform 同期する。

| 項目 | 状態 |
|---|---|
| HP / KO / HitCount / HitStun / Knockback / 攻撃 / HitStop / AttackResult | **実装済み・Editor 確認済み** |
| 論理位置 X・Facing の初期復帰 | **実装済み・Editor 確認済み**（壁際・KO 後） |
| Pause 中の R、Reset 直後の再移動／再攻撃、Development Build | **未確認** |
| Y/Z 復帰、複数初期配置プリセット、P2 が左側の別 Scene | **未確認／対象外** |

**実装構造（要約）**

1. `DebugFighterMotor`: `Awake` で Scene の Transform X → `logicalX`、同値を `initialLogicalX` に保存。`ResetLogicalXToInitial()` → `SetLogicalX(initialLogicalX)`
2. `SimulationSession.ResetTestActionForP1()`: 戦闘状態 Reset → P1/P2 論理 X 復帰 → `ApplyInitialFacingTowardOpponents()` → HitStop 0 → Visual
3. `Participant.ResetCombatDebugState()`: 戦闘状態のみ（位置・Facing は触らない）

**Editor Play Mode 実測例（FightDebugScene）**

| 時点 | P1X | P2X | P1FacingRight | P2FacingRight | 備考 |
|---|---:|---:|---|---|---|
| 初期 | 0.00 | 3.00 | true | false | |
| 壁際 | 6.00 | 7.00 | （移動後） | （移動後） | Push wall redistribute 確認 |
| KO 後 | — | — | — | — | P2 HP=0、KO=1、HitCount=10、AttackResult=Hit |
| R 後 | 0.00 | 3.00 | true | false | HP=100、Alive、HitCount=0、AttackResult=None、Idle、HitStop=0 |

詳細は §2.3、教材 §17.7、`docs/rules.md` §15.4。

### 最小 Visual State（実装済み・Editor 一部確認済み）

正式 Stage 番号は付けない。`FighterVisualState` enum で見た目意図を区別する（Animator / Walk 専用 Sprite は未実装）。

| 値 | 意味 | Sprite（現状） |
|---|---|---|
| Idle | 停止 | idleSprite |
| WalkForward | 前進（Facing と同方向入力） | idleSprite 流用 |
| WalkBackward | 後退（Facing と逆方向入力） | idleSprite 流用 |
| Attack | J Punch 攻撃ポーズ中 | attackSprite |

**責務**

| 担当 | 内容 |
|---|---|
| `SimulationSession` | `ResolveFighterVisualState` / `ResolveLocomotionVisualState`。CurrentInput × `Motor.FacingRight` で前進／後退を決定 |
| `DebugFighterVisual` | `Apply(FighterVisualState)` で Sprite 差し替えのみ。`CurrentVisualState` / `CurrentVisualLabel` / `IsAttackPoseActive` |
| `DebugFighterMotor` | 位置・Facing（歩行判定はしない） |
| `DebugFighterParticipant` | 戦闘状態（HP/KO/HitStun 等）。Visual 判定はしない |

**優先順位（Sprite）**: HitStun または KO → Idle 系 → Attack → WalkForward / WalkBackward → Idle

**判定**: 入力意図基準。Left+Right 同時・無入力は Idle。壁際で論理 X が変わらなくても方向入力中は Walk。Push / Knockback の受動移動だけでは Walk にしない。P2 Neutral は通常 Idle。HitStop 中は Combat スキップのため直前 Visual を保持。

**Editor 確認済み（主に P1・右向き）**

| 条件 | FighterVisual |
|---|---|
| 無入力 | Idle |
| 右向き + 左入力 | WalkBackward |
| 右向き + 右入力 | WalkForward |
| Left+Right 同時 | Idle |
| 壁際（P1X≈6 / P2X≈7）右入力 | WalkForward（実移動なしでも維持） |
| 移動入力中の J Punch | Attack（Walk より優先） |
| Training Reset 後 | Idle（位置 0.00 / 3.00、Facing 初期どおり） |

Push / Damage / HitStop / HitStun / Knockback / KO の継続動作も確認。Compile Error なし。既知 CS0618 以外の新規警告なし。

**未確認 / 現仕様上確認不可**

| 項目 | 区分 | 理由 |
|---|---|---|
| 左向き時の WalkForward / WalkBackward | **現仕様上確認不可（実測未実施）** | Push ですり抜け不可。ジャンプ・飛び越し・位置交換がなく、通常操作で P1 が P2 右側へ回れない。コードは `FacingRight` 参照で対応済みだが**実測済みとはしない** |
| P1 自身が HitStun / KO 中に Walk にならないこと | **未確認** | 今回 KO したのは P2。HUD/Console の FighterVisual は主に P1。優先順位コード上は Walk より上 |
| Development Build | **未確認** | |

詳細は教材 §17.8、`docs/rules.md` §15.5。

キャラクターの前進・後退・Idle・歩行表現・ジャンプ・キック・Punch・HitStun・Knockback・KO・アニメーション状態は、**練習専用ではなく練習／対戦共通のキャラクター機能**とする方針。見た目は戦闘処理がコマを直接決めず、戦闘状態→ Visual State → Animator または Sprite 差し替え、とする（詳細は `docs/rules.md` / 教材 §17）。

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
Round 終了・勝敗判定は工程表上の段階14/15には含まれず、**対戦モード側の後続候補**として残す（練習モードである FightDebugScene には混在させない。§1.1）。
正式な次工程番号は未定義。新 Stage 番号は作らない。

段階14Aの暫定「0HPでも戦闘継続」は**終了**。段階14Bから 0HP 到達で KO へ遷移する。

### 2.1 責務分担（要約・段階15後）

| 入れ物 | 担当 |
|---|---|
| **`DebugAttackData` / `DebugAttackData.JPunch`** | **J Punch 攻撃設定値の正本**（進行状態は持たない） |
| **`DebugFighterAttackState`** | 攻撃進行の正本（ActionFrame / HasCurrentJPunchHit 等） |
| **`DebugFighterHitState`** | 被 Hit / HitStun / ノックバック速度（HP・KO・攻撃データは持たない） |
| **`DebugFighterParticipant`** | **HP 正本** + **KO 正本**（`isKnockedOut`）。local→world Hit Box 変換・Facing 反転 |
| **`DebugFighterMotor`** | LogicalX・`initialLogicalX`・TryMoveLogicalXBy・Training Reset 時の論理 X 復帰（攻撃データ非所有。歩行 Visual 判定はしない） |
| **`DebugFighterVisual`** | `FighterVisualState` → Sprite。`Apply` / `CurrentVisualState` / `IsAttackPoseActive`（入力判定はしない） |
| **`DebugFighterPushResolver`** | 等分 Push ＋壁際再配分（攻撃データ非所有） |
| **`DebugPunchHitResolver`** | world Hit×Hurt 重なり判定（攻撃全体の設定正本にはしない） |
| **`SimulationTimeState`** | 共有時間・共有 HitStop |
| **`SimulationSession`** | tick 進行、攻撃開始/終了、Hit 適用、HitStop 開始、KO 接続、**Visual State 決定**（数値の正本にはしない） |

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

**Reset（Training Reset）**:

- **従来（段階12A〜位置復帰追加前）**: HP 最大 + KO 解除 + Attack/HitCount/HitStun/Knockback/HitStop 等。**位置・Facing は維持**していた。
- **現在**: 上記に加え、P1/P2 の論理 X を Scene 開始時へ戻し、両者復帰後に位置関係から Facing を再計算する（**実装済み**。正式 Stage 番号なし）。
- **Editor 確認済み**: 壁際（P1X=6.00 / P2X=7.00）→ R → 0.00 / 3.00、Facing 初期どおり。KO 後も R で Alive/100・HitCount=0。Push / Hit / HitStop / KO 回帰維持。Compile Error なし（既知 CS0618 以外の新規警告なし）。
- **未確認**: Pause 中 R、Reset 直後の再移動／再 J Punch、Development Build、Y/Z 復帰、P2 左側配置の別 Scene。詳細は §1.1。

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

**H. Reset（段階15時点の記録）**: 当時は両体 100/Alive、HitCount=0、Stun/KB/HitStop=0、AttackResult=None。**位置と Facing は維持**（当時仕様）。位置復帰は後続の Training Reset 拡張で追加（§1.1・§2.3）。

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

正式な優先順位・工程番号は未割当（順不同）。

**対戦モード固有（未実装・FightDebugScene に混在させない）**

- Round 終了、勝敗判定、WIN/LOSE、KO 後時間停止、リザルト、ラウンド再開始
- 複数ラウンド、タイマー、READY/FIGHT、Match 終了、対戦用 HUD、開始前・終了後の入力制限

**練習モード／共通まわりの候補**

- KO 専用アニメ、Down 物理、HP バー、Guard
- Character 別 KO / 攻撃データ、KO 演出制御
- 壁バウンド等（工程番号なし残課題）
- 攻撃データの ScriptableObject 化 / Inspector 編集 / JSON・CSV
- 複数攻撃、弱/中/強、技コマンド、コンボ、Cancel、Counter Hit
- Walk 専用 Sprite、Animator + Animation Clip（最小 Visual State enum は実装済み。§1.1）
- 左向き Walk 実測のための位置入れ替え／ジャンプ等（現仕様ではすり抜け不可）
- K Kick、ジャンプ／空中（いずれも未実装）
- 練習用将来候補: ダミー回復、自動回復、ガード設定、行動記録、判定表示、フレーム表示など

（Training Reset 位置復帰・最小 Visual State は **§1.1 で実装済み**。未実装候補からは外す。）

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

その後の候補（順不同・未着手。**新工程番号は作らない**。正式な次 Stage も未定義）:

- Walk 専用 Sprite、Animator + Animation Clip（Visual State 基盤は完了）
- 左向き実測のための位置入れ替え／ジャンプ等
- K Kick、ジャンプ／空中状態（未実装）
- 対戦モード用 Scene / Controller / HUD（Round / 勝敗 / タイマー / リザルト等。FightDebugScene とは分離）
- Down、HP バー、Guard
- ノックバック壁到達時の速度停止、壁バウンド、壁やられ、Corner
- しゃがみ、複数攻撃、入力バッファ、キャンセル
- 攻撃データの ScriptableObject 化（必要になったとき）
- 2P 入力 / CPU（KO 中移動・攻撃禁止の実操作確認、P1被Hit・左方向KB・Facing Left 攻撃を含む）
- 複数 Hurt / Hit Box、キャラ固有データ化
- 練習モード将来候補: ダミー回復、自動回復、ガード設定、行動記録、判定／フレーム表示など

Training Reset 位置復帰・最小 Visual State は完了（§1.1）。モード責務の方針も同節。Round/勝敗は**対戦モード固有**であり、練習 Scene の次必須工程としては未確定。

---

## 5.1 GC 学習・計測の補助改善（正式 Stage ではない）

Round / Guard / 複数攻撃などの**機能 Stage とは別枠**。番号「GC-1」「GC-2」は便宜名。

### GC-1 Fixed Help Text Allocation Reduction

| 項目 | 内容 |
|---|---|
| **実装** | `DebugHudView`: Update から固定 Help の毎 Frame 再構築・再代入を削除。設定は Awake → EnsureSplitHudLayout で1回 |
| **非対象** | Status HUD（`BuildStatusHudText`）は GC-1 では未変更（毎 Frame） |
| **実測条件** | Unity 6.3 LTS / Editor Play Mode / FightDebugScene / 通常待機 |
| **変更前** | フレーム全体 約18.0 KB・69 alloc。`DebugHudView.Update` 約17.8 KB。GC.Collect 0.000 ms |
| **変更後** | `DebugHudView.Update` 約17.2 KB（約0.6 KB・約3.4%削減）。全体 KB / alloc 回数 / GC.Collect は**未記録** |
| **回帰** | Help 表示・Status HUD 表示は維持。Compile Error なし。既知 CS0618（enableWordWrapping）1件 |
| **未計測** | Development Build 測定、変更後のフレーム全体値 |

詳細: `docs/component_and_scene_guide.md` §16.13。

### GC-2 Status HUD StringBuilder Reuse

| 項目 | 内容 |
|---|---|
| **実装** | 再利用 `StringBuilder(2048)` + `Clear` + `Append`。`hudText.SetText(statusTextBuilder)`。毎 Frame 更新は維持 |
| **非対象** | Help（GC-1 のまま）、戦闘処理、更新頻度の低下、小数 `ToString` の完全除去、`DebugBox2D` 型変更 |
| **実測条件** | Unity 6.3 LTS / Editor Play Mode / FightDebugScene / 通常待機 / `DebugHudView.Update` |
| **変更前（GC-1後）** | 約17.2 KB / frame |
| **変更後** | 約3.2 KB / frame（別通常フレームでも約3.2 KB を確認） |
| **削減** | GC-2 単体 約14.0 KB・約81.4%。初期状態（約17.8 KB）比 約14.6 KB・約82.0% |
| **回帰** | 表示・Miss/Hit/KO/Push/Reset まで確認。Help・Status 毎 Frame 更新維持。Error 0。既知 CS0618 1件 |
| **残存候補** | 小数 `ToString`、`DebugBox2D`（class）の HUD 用 Box 生成、TMP 内部。内訳・Development Build は**未計測** |
| **未記録** | 変更後のフレーム全体 KB・alloc 回数・GC.Collect（推測しない） |

詳細: `docs/component_and_scene_guide.md` §16.14。

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
- 最新コミット済み HEAD: `5bd43f7`。Training Reset 位置復帰・最小 Visual State は実装済み（Docs 反映時点では後者が未コミットの場合あり）。正式 Stage 番号なし
- `FightDebugScene` は**練習・検証モード**。戦闘コア共通、KO 後処理はモード側（§1.1）
- Visual State: Session が Facing×入力で Idle/WalkF/WalkB/Attack を決定。Walk は Idle Sprite 流用。Animator 未使用
- 右向き Walk / 壁際 Walk / Attack 優先 / Reset 後 Idle は Editor 確認済み。左向き実測・P1 被弾側・Dev Build は未確認
- 正式な次 Stage 番号は未定義。候補は順不同（Walk Sprite、Animator、位置入れ替え／ジャンプ、Kick、対戦モード分離など）
