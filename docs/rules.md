# 2D格闘ゲーム Trial ルール仕様

学習用プロジェクトの**ゲーム仕様の正本**です。
Unity / UE などのエンジンAPIではなく、**60Hz論理シミュレーション**上のルールを定義します。

実装時の読みやすさ方針は `docs/learning_and_readability.md` および `README.md` を参照してください。

「GameFrame」という曖昧な単語だけに依存せず、時間の種類を次のように区別します（§10）。

| 名称 | 意味 |
|---|---|
| **SimulationTick** | 60Hzで常に進む。入力履歴・入力バッファ用 |
| **CombatFrame** | 戦闘進行の論理フレーム。HitStop中は進まない |
| **ActionFrame** | キャラクターの現在ポーズ／技コマ番号。HitStop中は進まない |

---

## 0. 確定度の区分

### 0.1 確定ルール

- SimulationTick と CombatFrame / ActionFrame の分離（HitStopでも入力tickは進む）
- 1 CombatFrame の処理順（§10）
- 同一 CombatFrame の接触は全件収集してから結果を決め、双方へ一括適用する
- Clash は一次分類。Trade は双方向がともに Hit のときだけ集約する複合結果
- ガード成立は攻撃接触時の入力判定。後ろ単独は後退。Down+Back はその場しゃがみ（水平移動なし）
- JustGuard は **Back の新規押下エッジ**（Down 保持後に Back を足した場合を含む）。窓は暫定3 SimulationTick
- BlockStun が戦闘硬直の所有者。GuardPosture / DisplayAnimation は別プロパティ
- 空中 Pushbox 無効。着地で復活＋等分分離（壁際は未消化を相手へ転送）
- 入力解釈用 Facing（前 CombatFrame 終了時）と、当フレーム最終 Facing（Push後）を分離
- ActionFrame は新状態入場時に 0。その CombatFrame は frame 0 データを使用。末尾で duration を1減らす
- `attack_category` は分類ラベルのみ
- 攻撃対象はフラグと幾何の両方
- 無敵接触は AttackInstance 非消費。WakeUp 無敵は `CombatState == WakeUp` のみ
- しゃがみキックの潰しは Clash のみ（優先度勝利ではない）
- 通常 Hit/Guard の壁際 Pushback は未消化を攻撃側へ100%転送（初期）
- Clash の壁際未消化反動は初期版では**転送せず破棄**
- **J Punch（A）は地上専用**。ジャンプ中は開始できない。空中パンチは採用しない
- 必殺技は未実装・非表示
- `state_transitions.csv` は草案（完全実行用SMではない）

### 0.2 初期検証用の暫定値

| 項目 | 扱い |
|---|---|
| JustGuardWindow | 3 SimulationTick（contact 含む直前3） |
| Damage / HitStun / BlockStun / Pushback | `moves.csv` provisional |
| 判定箱サイズ | `boxes.csv` |
| Jump 移動量 | 未収録（Unity デバッグは `FighterJumpSettings`。§15.7） |
| LandingRecovery フレーム数 | 暫定・未決定（Unity デバッグは `LandingFrames=2`） |
| ClashRecoil 量 | 未収録・暫定 |

### 0.3 未確定事項

- 多段技、必殺技、キャラ差
- Clash 壁際の未消化転送（将来候補。初期は破棄）
- 高度な壁際補正、結果別 `transfer_ratio`
- LandingRecovery の具体フレーム数
- Air Kickの正式値・専用Sprite・正式な空中命中／被弾挙動（最小検証版は実装済み）
- **Air Hit / Air Knockback**（空中にいるキャラが被弾したときの専用処理。空中攻撃とは別）

> 初期資料・`moves.csv` の `AIR_PUNCH` / `AIR_KICK` 行は過去の配置・数値案の残骸であり、**現行仕様の正本ではない**。履歴は `CHANGELOG.md` を参照。

---

## 1. 操作

| 入力 | 動作 |
|---|---|
| A | **地上パンチ（J Punch）**。ジャンプ中は開始不可 |
| B（Unity: K） | 接地中はGround Kick、すでに空中ならAir Kick。地上Up+KはGround Kick |
| X | 垂直ジャンプ |
| X + 前 | 前ジャンプ |
| X + 後ろ | 後ろジャンプ |
| 下 | しゃがみ |
| 後ろ | **後退移動**（接触時は立ちガード判定の入力にもなる） |
| 下 + 後ろ | **その場でしゃがむ**（水平移動しない）。接触時はしゃがみガード |

前後は、**前 CombatFrame 終了時に確定していた Facing** で解釈する（§2.3）。

### 1.1 Down + Back（確定）

- Down + Back は**その場でしゃがむ**
- **水平方向には移動しない**
- 攻撃接触時はしゃがみガード
- 「しゃがみ後退」という表現は使わない（誤解を招くため）

### 1.2 後退と立ちガード（確定）

- 後ろ入力のみのとき、通常の**後退移動**を行う
- ガード成立は常時の独立防御状態ではない
- 攻撃接触時に後ろ入力が有効なら立ちガード
- `frames.csv` の `can_guard` は「そのポーズでガード成立可能か」

### 1.3 GuardPosture と DisplayAnimation（確定）

戦闘状態と姿勢表示を分離する。

| プロパティ | 役割 | 例 |
|---|---|---|
| **CombatState** | 戦闘上の状態。硬直の残りを持つ | `BlockStun` / `Neutral` / `StandPunch` |
| **GuardPosture** | ガード姿勢（立ち／しゃがみ） | `Stand` / `Crouch` / `None` |
| **DisplayAnimation** | 表示アニメ・Hurtbox姿勢の参照名 | `StandGuard` / `CrouchGuard` |

- `StandGuard` / `CrouchGuard` は**独立した CombatState ではない**
- `state_transitions.csv` でも、これらを独立戦闘状態として遷移させない

---

## 2. ジャンプ・Facing・着地

- 垂直・前・後ろの3種類
- X入力受理時点の方向で種類を確定（X押下の SimulationTick と直前2 tick の方向を参照）
- 着地まで軌道変更なし
- 空中Pushbox無効（すれ違い可）
- 空中ガード不可
- **現行**: J Punch / Ground Kickは地上専用。すでに空中で新しくKを押すとAir Kickを開始する。空中Jは攻撃なし
- Air Kickは1ジャンプ1回、上昇・頂点・下降で開始可能。入力予約はせず、着地時に即終了する暫定仕様

> **Unity デバッグ実装メモ（2026-07-28）**: FightDebugScene では Up エッジ＋Facing×左右で Neutral/Forward/Backward を決め、CombatFrame 固定の LogicalY 軌道を使う（Rigidbody 非使用）。空中 Push は高さ差閾値で skip。ジャンプ中の J Punch 開始は**不可**。詳細ルールは **§15.7**、到達点は `docs/unity_implementation_status.md` §1.2。本節の「直前2 tick」バッファ設計とは一致しない点がある。

### 2.1 着地と LandingRecovery

- 着地後は `LandingRecovery`（または Unity デバッグの Landing）へ入る（フレーム数は暫定・未決定）
- LandingRecovery 中は再ジャンプ不可
- **現行**: Air KickはStartup / Active / Recoveryの途中でも着地時に即終了する。地上Recoveryや追加着地硬直は設けない

> Unity デバッグ実装: `LandingFrames=2`。Landing 中は左右移動可・再ジャンプ不可。空中Jは不可、空中KはAir Kick最小検証版として1ジャンプ1回だけ開始可能。

### 2.2 着地時 Pushbox

- 着地 CombatFrame で地上 Pushbox 復活
- 重なりは左右へ等分分離
- 壁で片方が動けない場合、未消化分離量をもう片方へ移す
- 体重差などは導入しない

### 2.3 Facing：入力解釈用と最終確定用（確定）

| 種類 | いつ確定 | 用途 |
|---|---|---|
| **入力解釈用 Facing** | **前 CombatFrame 終了時**に確定していた Facing | 当フレームの前後入力の意味 |
| **最終 Facing** | 当フレームの移動・Push・着地分離の**後に一度だけ** | 表示・判定箱・**次フレームの入力解釈用** |

- 当フレーム途中で位置関係や Facing が変わっても、**そのフレームの入力意味を後から反転しない**
- 判定箱更新は最終 Facing を反映したあと（§10）

#### 2.3.1 Unityデバッグ実装との関係（2026-07-24）

正式方針では、地上では基本的に相手と向き合い、Left/Right 入力は**ワールド移動方向**だけを決める（前進／後退）。
自動振り向きは Push Box（すり抜け防止）とセットで設計する。

Unity デバッグ実装では、Facing と移動入力の分離・相手向き合いは**実装済み**である（段階10A）。
横方向 Push Box／すり抜け防止も**実装済み**である（段階10B-3）。
処理順は 入力移動 → ノックバック移動・減速 → Push 補正 → Facing → ActionFrame → Hit → Visual → HitStun消費。接触後の押し分けは双方等分補正を基本とし、ステージ端で実移動が足りない分は反対側へ再配分する（段階13B-1）。
Push / Hurt / Hit Box の **Game ビュー可視化**も**実装済み**である（段階11A）。
Jパンチ Hit は **Hit Box × Hurt Box の重なり判定**である（段階11B。距離判定は削除済み）。
被 Hit 後は共有 **HitStop（6F）** のあと、被弾側 **HitStun（12 Combat Frame）** と横ノックバックが続く（段階12A / 13A。数値の正本は段階15の攻撃データ）。
HitStop は試合全体の Combat 停止、HitStun は被弾側のみの行動不能である。
ノックバック初速は Hit 成立時に予約し、**HitStop 中は移動・減衰しない**。HitStop 終了後の Combat Frame から固定量で移動する（`Time.deltaTime` 不使用）。
方向は Facing ではなく LogicalX 比較で決める。速度の正本は `HitState`、位置書き込みは `Motor`（`SetLogicalX` / `TryMoveLogicalXBy`）。
HitStun 中も Push / Facing は維持する。ノックバック後のめり込みは同フレームの Push で解消する。Push は KnockbackVelocityX を変更しない。
既存 Motor の minX/maxX（±7）は有効のまま。壁際 Push 再配分は**実装済み**（段階13B-1）。壁バウンド・壁やられ・ノックバック壁停止は未実装。
縦方向の等分 Push（体重差など）は未実装。空中の高さ差による Push skip・飛び越し後の Facing 更新は **Unity デバッグ実装で実装済み**（§15.7。正式 Stage 番号なし）。
Participant 共通の **HP / Damage** は**実装済み**である（段階14A。最大HP暫定100。Jパンチ Damage=10 は段階15で攻撃データ化）。
有効 Hit 成立時に1回だけ減算し、0 未満にはしない。
**HP が 0 になると KO 状態へ一度だけ遷移**する（段階14B）。段階14Aの「0HPでも戦闘継続」暫定は終了した。
最後の一撃の HitStop / HitStun / Knockback は通常どおり成立し、HitStun 終了後も KO は Reset まで維持する。
KO 中は本人の入力移動と新規攻撃を禁止し、KO 済み防御者への追加 Hit は成立させない。
表示色の現在仕様は **HitStun 被 Hit 表示 > KO 暗色 > 通常 Tint**（段階15で回帰修正。KO 状態の開始時点は変えない）。
Reset（R）は練習モードの **Training Reset** である（§15.4）。HP は最大へ全回復し、KO も解除する。加えて P1/P2 の**論理位置 X/Y**とジャンプ状態を Scene 開始時へ戻し、両者の位置復帰後に位置関係から Facing を再計算する（**実装済み・Editor 確認済み**。Transform 直書きではない）。Reset 受理フレームは通常 SimulationTick へ進めない。Reset 後は全ゲーム操作を一度離すまで有効入力を抑制する（R 自体は解除条件に含めない）。Pause 中 R による Training Reset の受理、主要状態初期化、AirKick Assist 予約破棄、Pause 解除後の Gameplay input 再有効化は Play 確認済み。全戦闘状態・全入力条件・Development Build を含む詳細総合回帰は未確認。HP バー・Guard は未実装。Round 終了・勝敗判定は**対戦モード固有・未実装**であり、FightDebugScene（練習）には混在させない。

Jパンチの **攻撃設定値の正本**は `DebugAttackData.JPunch` である（段階15。ScriptableObject ではない読み取り専用データ）。
Startup/Active/Recovery・Damage・HitStop・HitStun・Knockback・local Hit Box をここから参照する。
攻撃状態の正本は各 `DebugFighterParticipant.AttackState` である。
被 Hit / HitStun / ノックバック速度の正本は各 `DebugFighterParticipant.HitState` である。
HP と KO の正本は各 `DebugFighterParticipant`（HitState には持たせない）。
`SimulationTimeState` は SimulationTick / CombatFrame / Pause / HitStop 等の共有時間状態を持つ。
`SimulationSession` は攻撃進行と Hit 適用を行うが、Jパンチ固定値の正本にはならない。
ジャンプ軌道の正本は `DebugFighterMotor` の LogicalY / 速度である（§15.7）。Jump 種類判定と Visual State 決定は Session。

実装済み／暫定／未実装／次工程の一覧は `docs/unity_implementation_status.md` を参照する。
モード構成・Visual State・Sprite・ジャンプ実装ルールは **§15**。

---

## 3. 通常技

**現行（Unity デバッグ）**: 地上J Punch、地上Ground Kick、空中Air Kick最小検証版を実装。空中Jは採用しない。KはCombatFrame開始時点の接地状態でGround / Airへ分岐し、入力予約しない。

**長期設計上の候補（未実装・未確定）**: しゃがみパンチ／キック、Air Kick正式化、Air Hit / Air Knockback。

### しゃがみキック（再掲・長期設計）

- 「潰す」は優先度による自動勝利では**ない**
- 相手 Hitbox と接触すれば Clash。接触せず Hurt に届けば通常 Hit

### 3.1 attack_category

分類ラベルのみ。ガード高さ・Clash・ダメージ等は自動決定しない。

### 3.2 攻撃対象フラグ

`can_hit_ground` / `can_hit_crouch` / `can_hit_air` 等と幾何の**両方**が必要。

### 3.3 ActionFrame の進め方（確定・オフバイワン対策）

目的: Startup が1 CombatFrame 短くなる誤実装を防ぐ。

1. **新しい CombatState（または新しいポーズ）へ入った CombatFrame では `action_frame = 0`**
2. **その CombatFrame の移動・判定箱・攻撃判定には frame 0（そのポーズ）のデータを使用する**
3. **CombatFrame の末尾で、現在ポーズの残り時間（`duration` 残）を 1 減らす**
4. `duration_game_frames = 3` なら、そのポーズは**実際に 3 論理 CombatFrame 使用される**
5. 残りが 0 になった末尾のあと、次の CombatFrame 先頭で次ポーズへ進み `action_frame` を更新する

誤例（禁止）: 入場と同時に「1フレーム消費済み」とみなして実質 duration-1 しか使わない。

HitStop 中は ActionFrame / duration 残を進めない（SimulationTick のみ進む）。

---

## 4. ガードと BlockStun

攻撃接触時、地上側が相手から離れる方向（後ろ／Down+Back）を入力していれば成立。

### 4.1 BlockStun の責務（確定）

- **戦闘状態として残り硬直を所有するのは `CombatState = BlockStun`**
- `GuardPosture` は Stand または Crouch を保持する
- `DisplayAnimation` は StandGuard / CrouchGuard など表示用
- BlockStun 中の後続攻撃は、**入力再要求なし**で `GuardPosture` を維持してガード
- BlockStun 中は**新しい JustGuard を行わない**

### 4.2 BlockStun 終了時（確定）

BlockStun の残りが 0 になったら:

- `GuardPosture == Crouch`、または Down 入力中 → **Crouch**（または Neutral 相当のしゃがみ制御）へ
- それ以外 → **Neutral** へ
- `GuardPosture` をクリア（または姿勢に合わせて更新）
- DisplayAnimation を Idle / Crouch 等へ戻す

初期実装では Guard 解決まで必須としない。

---

## 5. JustGuard（攻撃弾き型 / Parry-like）

### 5.1 成立条件（確定）

- 通常ガードの姿勢・対象条件を満たす
- **Back の新規押下開始エッジ**が、接触の SimulationTick を含む窓に入っている
- 有効窓: `contactTick - 2` 〜 `contactTick`（暫定3）。`JustGuardWindow=3` は暫定値
- それ以前から Back を保持 → 通常ガード
- BlockStun 中は新規 JustGuard なし

### 5.2 Down と Back の順序（確定）

| 操作 | JustGuard |
|---|---|
| Down を先に保持し、その後 Back を押した | Back の**新規押下エッジ**あり → **しゃがみ JustGuard の対象** |
| Back を先に保持し、その後 Down を押しただけ | Back の新規押下エッジが**ない** → JustGuard に**ならない**（通常のしゃがみガード判定） |

### 5.3 効果（方針確定・数値暫定）

- 防御側: 無ダメージ、短い硬直、押し戻し軽減
- 地上攻撃側: 中断 → Stagger 系
- AttackInstance 消費
- **空中攻撃側の JustGuard 効果**（旧案の AirDeflected 等）: 空中攻撃自体が仕様未定のため、現行では定義しない

---

## 6. Clash と Trade

### 6.1 Clash（一次分類・確定）

- 同一 CombatFrame で有効 Hitbox 同士が接触 → **Clash**
- 関与 AttackInstance の相手 Hurt 攻撃は破棄
- 関与双方の AttackInstance を消費
- 双方ダメージなし、攻撃中断
- **ClashRecoil として双方に反動量を与える**
- 「攻撃側」「防御側」という一方向表現は Clash では使わない

### 6.2 Clash 時の壁際（確定・初期版）

- 通常 Hit/Guard の Pushback 転送と、Clash の双方反動は**別処理**
- 壁で片方が動けない場合の未消化反動は、初期版では**相手へ転送せず破棄**する
- 転送する案は将来調整候補として残す（現時点では採用しない）

### 6.3 Trade（複合結果・確定）

Trade は接触候補の**一次分類ではない**。

手続き:

1. 接触候補を全件収集
2. Clash を解決し、関与分の Hurt 攻撃を除外
3. **P1→P2** と **P2→P1** を、それぞれ Invulnerability / Miss / JustGuard / Guard / Hit として**個別分類**
4. 双方向の最終結果が**ともに Hit** のときだけ、表示・ログ上 **Trade として集約**
5. 片方が Guard / JustGuard / IgnoredByInvulnerability / Miss 等なら **Trade にしない**（各方向の結果をそのまま適用）
6. 処理順で片方だけ勝たせる実装は禁止。分類後に一括適用

`hit_resolution.csv` に Trade を単純な priority 行として置かない。集約はコード側の確定手続き。

---

## 7. 多重ヒット防止と無敵

- AttackInstanceId + HitGroupId + AttackerId + TargetId
- Hit / Guard / JustGuard / Clash、および Trade 集約の元になった Hit で消費
- 無敵接触は消費しない（`IgnoredByInvulnerability`）
- 無敵終了後も Active 継続なら再候補になり得る

---

## 8. ダウン・起き上がり

`KnockdownStart` → `Down` → `WakeUp`（この間のみ無敵）→ `Neutral`（この CombatFrame から被弾可能）

---

## 9. 壁際 Pushback（通常 Hit / Guard 系・初期仕様）

- 防御側が壁で押し戻せない未消化分を攻撃側へ **100% 転送**
- 最大転送量は元の Pushback を超えない
- Hit / Guard / JustGuard で別係数はまだ持たない
- **Clash の反動転送はこの節の対象外**（§6.2）

---

## 10. 時間管理と処理順（正本）

### 10.1 SimulationTick と CombatFrame

- 内部は 60Hz
- **SimulationTick**: 常に進む。入力の押下・解放・保持をこの番号で記録。HitStop中も進む
- **CombatFrame**: 戦闘進行。HitStop中は進まない（状態・技・移動・箱・接触解決を行わない）
- **ActionFrame**: キャラのポーズ番号。HitStop中は進まない
- 描画FPS・Unity Update/FixedUpdate は正本にしない
- デバッグの「1フレーム送り」は、通常は CombatFrame を1進める操作として扱う（HitStop中の扱いは実装時にHUDで明示）

JustGuard 窓は **SimulationTick** 上の入力エッジで測る（接触が起きた CombatFrame に対応する tick を contactTick とする）。

### 10.2 HitStop 中（確定）

| 進む | 止まる |
|---|---|
| SimulationTick（入力記録） | CombatState / ActionFrame / duration 残 |
| 入力履歴・バッファ | 移動・ジャンプ軌道 |
| HitStop 残の減少（手順3） | 判定箱更新・Push・戦闘解決 |
| | CombatFrame 番号 |

詳細な分岐は §10.3（手順1〜3は毎tick、手順4〜18は CombatFrame 進行時のみ）。

### 10.3 SimulationTick ごとの処理順（確定）

毎 **SimulationTick** で以下を実行する。接触結果は片方ずつ即時適用しない。

**範囲の分け方（確定）**

| 手順 | いつ実行するか |
|---|---|
| **1〜3** | **毎 SimulationTick** 必ず実行 |
| **4〜18** | **CombatFrame を進める場合のみ**実行（HitStop中は実行しない） |

流れの要約:

1. 入力サンプリングと入力履歴更新は**毎 SimulationTick**行う
2. その後、HitStop中なら**戦闘進行（手順4〜18）をスキップ**する
3. 状態遷移以降（手順4〜）は、**CombatFrame を進める場合のみ**実行する

#### 手順1〜3（毎 SimulationTick）

| 順 | 処理 |
|---|---|
| 1 | 入力サンプリング（解釈は**入力解釈用 Facing**＝前 CombatFrame 終了時 Facing） |
| 2 | 入力履歴・バッファ更新（**SimulationTick** 基準）。このtickの SimulationTick 番号を進める |
| 3 | HitStop 残を確認する。残がある場合は HitStop 残を 1 減らし、**手順4〜18をスキップ**してこの SimulationTick を終える |

#### 手順4〜18（CombatFrame を進める場合のみ）

HitStop 残が 0 のときだけ実行する。

| 順 | 処理 |
|---|---|
| 4 | 状態遷移要求の決定 |
| 5 | 状態・ActionFrame の入場処理（新規なら action_frame=0。データは frame0） |
| 6 | 移動・ジャンプ軌道反映（frame0〜現在ポーズのデータ） |
| 7 | Pushbox 解決（着地分離含む） |
| 8 | **最終 Facing** 確定（一度だけ。次CFの入力解釈用になる） |
| 9 | 判定箱更新（最終 Facing） |
| 10 | 接触候補を全件収集 |
| 11 | Clash 解決（双方 ClashRecoil。壁際未消化は破棄） |
| 12 | 各方向の無敵判定 |
| 13 | 各方向の JustGuard / Guard / Hit / Miss 等を個別分類 |
| 14 | 双方向とも Hit なら Trade へ集約。結果を双方へ一括適用 |
| 15 | AttackInstance 消費（無敵無視は除く） |
| 16 | 現在ポーズの duration 残を 1 減らす（ActionFrame 進行の末尾処理） |
| 17 | デバッグ情報更新（戦闘側） |
| 18 | **CombatFrame 番号を 1 進める** |

**CombatFrame 番号の進め方（確定）**

- 手順4〜18を1回完了したとき、CombatFrame が1進む
- その明示が手順18「CombatFrame 番号を 1 進める」である
- HitStop中に手順4〜18をスキップした SimulationTick では、CombatFrame 番号は**進まない**

---

## 11. Miss / NoContact / Whiff

| 用語 | 意味 |
|---|---|
| NoContact | 候補なし。毎tickログしない |
| Miss / InvalidTarget | 幾何重なりあるが属性で無効 |
| Whiff | 技終了まで有効接触なし（デバッグ表現） |

---

## 12. 必殺技

未実装・非表示。将来もパイプライン共有のみ。通常技性質は自動継承しない。

---

## 13. CSV とコードの責務

### コード側

- 処理順、接触全件収集、Clash、**Trade集約**、AttackInstance、Push、Facing二種、アンチ多重ヒット、SimulationTick/CombatFrame 分離、ActionFrame/duration 末尾減算

### CSV

- フレーム数、ダメージ、硬直、Pushback、箱、対象フラグ、技属性

### アダプタ／文書

- 各エンジンの入力バインド、UV、Sprite、デバッグキー

`state_transitions.csv` は草案。`StandGuard`/`CrouchGuard` を CombatState 遷移先にしない。

---

## 14. 初期実装スコープ

仕様上の初期対象（変更なし）:

1. SimulationTick / CombatFrame 進行（Pause / 1フレーム送り）
2. Idle
3. StandPunch（ActionFrame と duration の正しい進め方）
4. 判定箱表示
5. デバッグHUD初期必須（SimulationTick 等の日本語説明付き）

Unity デバッグ実装の**実際の到達点**（段階1〜15）と次工程は
`docs/unity_implementation_status.md` を正とする。
（工程表の段階14: HP/Damage/KO、段階15: 攻撃データ化 は完了。SO 化は見送り。正式な次 Stage 番号は未定義。後続候補は順不同。Round/勝敗は対戦モード固有。）

本番スプライトシートは未完成。参考画像を完成スプライトとしない。

---

## 15. モード構成・Training Reset・見た目同期（方針確定）

この節は **2026-07-27 に方針確定**した内容である。実装済みと混同しない。新 Stage 番号は定義しない。

### 15.1 戦闘コアとモード進行の分離

| 層 | 責務 |
|---|---|
| **共通戦闘コア** | 入力サンプリング、左右移動、向き、Push / Hurt / Hit Box、Startup / Active / Recovery、Damage、HitStop、HitStun、Knockback、HP、KO 判定、KO 後の追加被弾拒否、攻撃データ、基本的な戦闘状態、戦闘状態から見た目への同期 |
| **モード側** | KO **成立後**に何をするか（観察継続／ラウンド終了／WIN・LOSE など） |

共通側は「KO が成立した」という戦闘結果までを担当する。

### 15.2 FightDebugScene（練習モード）

- 対戦モードではなく、**正式な練習・検証モード**
- 詳細 HUD、戦闘ログ、判定確認、KO 観察、R による Training Reset を維持する
- KO 後に WIN/LOSE やラウンド終了へ進まない
- **対戦進行を FightDebugScene へ混在させない**

練習固有の例: 勝敗なし、ラウンド管理なし、時間制限なし、WIN/LOSE なし、Training Reset、詳細 Debug HUD。

### 15.3 対戦モード（将来・未実装）

ラウンド開始・終了、勝敗、WIN/LOSE、ラウンド数、タイマー、READY/FIGHT、次ラウンド、Match 終了、リザルト、対戦用 HUD、開始前・終了後の入力制限。
別モードとして扱う。現時点では未実装。

### 15.4 Training Reset

練習モードの正式な Training Reset（R）。論理位置（X/Y）・ジャンプ状態・Facing・戦闘状態を Scene 開始時相当へ戻す（正式 Stage 番号は付けない。対戦モード実装ではない）。

**初期化対象**: P1/P2 論理位置 X/Y・Grounded・ジャンプ速度/Type/frames・Landing・Jump 計測イベント、向き、HP、KO、HitCount、HitStun、Knockback、攻撃状態、HitStop、AttackResult、Sampled 有効入力。

**位置の正本**: Transform を Session / Participant から直接書き換えない。Motor が保持する**論理座標**を初期値へ戻し、既存同期経路でクランプと Transform 反映する。

**Facing**: Slot（P1/P2）で向きを決め打ちしない。P1/P2 **両方の論理位置を戻したあと**、既存の `ApplyInitialFacingTowardOpponents()` で位置関係から互いに向き合う向きを再計算する。

**Reset フレームの打ち切り**: `SimulationClockDriver` は R を受理した Unity Update で Reset 適用後に return する。そのフレームでは通常 SimulationTick（Jump / Attack / 移動 / Push / Visual 再処理）へ進まない。

**共通 release gate**: Reset 後は `waitForAllGameplayInputReleaseAfterReset` を立て、Left / Right / Up / Down / Attack がすべて離れるまで Simulation へ渡す有効入力を Neutral 化する。物理 Held（`DebugGameplayInput`）は消さない。**R は DebugPlaybackInput 側であり、解除条件に含めない**。一部だけ離しても解除しない。解除 tick でも有効入力は Neutral のまま、エッジ用 previous を false 再同期し、次の新規押下から受付する。

**責務分担**

| 担当 | 内容 |
|---|---|
| `DebugFighterMotor` | 初期論理 X/Y を保持。`ResetLogicalPositionAndJumpToInitial()` で復帰・Jump 計測破棄 |
| `DebugFighterParticipant.ResetCombatDebugState` | 戦闘状態（HP/KO/攻撃/HitStun 等）のみ。位置・Facing・Jump は触らない |
| `SimulationSession.ResetTestActionForP1` | Reset **順序**と release gate 開始の管理 |
| `SimulationClockDriver` | Reset 受理フレームで通常 Tick へ進まない |

将来の対戦モードでも、位置・Facing・Jump を含む共通 Reset 処理を再利用しうる。**現在の実装入口は Training Reset（練習モード）**である。

**確認状況**: ジャンプ中 R・壁際・KO 後・HitStop 中 Reset、release gate、解除後の再入力を Editor Play Mode で確認済み。Pause 中 R による Training Reset の受理・主要状態初期化・AirKick Assist 予約破棄・Pause 解除後の Gameplay input 再有効化は Play 確認済み。Pause 中 R の詳細総合回帰、Development Build は**未確認**。

### 15.5 キャラクター機能と Visual State

前進・後退・Idle・歩行表現・ジャンプ・キック・Punch・HitStun・ClashRecoil・Knockback・KO・アニメーション状態は、**練習専用ではなく練習／対戦共通のキャラクター機能**とする。

**実装済み**: `FighterVisualState` = Idle / WalkForward / WalkBackward / Attack / JumpStart / JumpRise / JumpApex / JumpFall / Landing / HitStun / KO / Kick / ClashRecoil。
Session が状態を決定し、`DebugFighterVisual` が `FighterSpriteSequence` で Sprite を再生する。WalkForward / WalkBackward は同一 Walk 8コマ。素材は`Fighter_SpriteSheet`（GUID `42345be00e0994144ba94bc5f1362757`、41 sub-sprite）のsub-sprite。Animatorは未導入。

**将来候補（未実装含む）**: ClashRecoil専用Sprite／演出、Punch 3 枚以上、HitStun・KO 専用画像、Animator など。

**責務境界（維持する）**

| 担当 | 内容 |
|---|---|
| `SimulationSession` | Visual State の決定（入力 × Facing、Attack / Jump / ClashRecoil / HitStun / KO 優先） |
| `DebugFighterVisual` | 描画専用（Sequence 再生。入力を読まない） |
| `DebugFighterMotor` | 論理位置（X/Y）と Facing、ジャンプ軌道（Apex 表示用フラグ含む） |
| `DebugFighterParticipant` | 戦闘状態（HP / KO / HitStun / ClashRecoil 等） |

戦闘処理が足の角度や Sprite のコマを直接決めない。戦闘状態を Visual State へ変換し、見た目側が Animator または Sprite 差し替えで表現する。Animator 導入後もこの責務境界を維持する。

**前進／後退**は左右キーだけで決めない。移動方向と Facing の組み合わせで決める。Left+Right 同時は Idle。無入力は Idle。

| Facing | 移動入力 | Visual State |
|---|---|---|
| 右向き | 右 | WalkForward |
| 右向き | 左 | WalkBackward |
| 左向き | 左 | WalkForward |
| 左向き | 右 | WalkBackward |

判定は**入力意図**を基準とする。壁際で論理 X が変化しなくても方向入力中は Walk とする。Push / Knockback による受動移動だけでは Walk にしない。

**優先（Sprite）**: KO → ClashRecoil → HitStun → Attack / Kick → JumpStart → JumpRise → JumpApex → JumpFall → Landing → WalkForward / WalkBackward → Idle。

左向き Walk / Jump / J Punch は飛び越し後に Editor で確認済み。

### 15.6 Sprite と Animation（学習方針）

**現在（実装済み）**: `Fighter_SpriteSheet.png`（Multiple、GUID `42345be00e0994144ba94bc5f1362757`、41 sub-sprite、PPU 39）。P1/P2同一シート、P2はTint。`FighterSpriteSequence`でCombatFrame基準のコマ切替。各コマは実画素Trim Rect＋Center Pivot。

**複数コマの用意**

| 方式 | Sprite Mode | 例 |
|---|---|---|
| A. 1コマ1 PNG | 各画像 `Single` | （旧デバッグ単体。現 Scene では未使用） |
| B. 1枚のスプライトシート | `Multiple` + Slice（グリッドまたは個別 Rect） | `Fighter_SpriteSheet.png` |

複数 Sprite を用意しただけではアニメーションしない。現状はコード側 Sequence。将来候補は **Sprite 群 → Animation Clip → Animator Controller → 状態切替**。

学習メモ: 192×192 共通 Rect だけではフレーム内位置ずれで SpriteRenderer が揺れて見える。**Rect 側で体幹中心・足元を揃える**。`framesPerSprite` は FPS ではなく 1 枚あたりの CombatFrame 数。Animator 導入は別候補。

### 15.7 Unity デバッグ実装のジャンプ・入力ゲート（ルール）

本節は FightDebugScene 向け実装ルールである。§2 の長期設計（X 押下＋直前方向バッファ等）と完全一致しない点がある。実装状況の正本は `docs/unity_implementation_status.md` §1.2。

**責務**

- Jump 計算（LogicalY・速度・着地）は Motor
- Jump 種類決定と Visual State 決定は Session
- Visual は描画のみ
- Input は物理入力のみ（ルールや Jump 種類を決めない）

**軌道**

- LogicalY を正本とする。Rigidbody 物理を Jump 正本にしない
- 固定 CombatFrame で更新する。HitStop 中は Jump 軌道も停止する
- Facing × 入力で Forward / Backward を判定する。JumpType は着地まで保持する
- 高さ差が閾値以上なら空中 Push を無効化する。着地付近で Push を復帰する
- Jump Visual はシートの JumpStart / Rise / Apex / Fall / Landing sub-sprite

**Training Reset**

- Reset フレームは通常 Tick へ進めない
- Reset 後は全ゲーム操作（Left/Right/Up/Down/Attack）を一度離すまで入力抑制する
- release gate は物理入力を消さず、有効入力だけ Neutral 化する
- R Reset は release 対象外
- Kick は共通 Held 判定（`HasAnyGameplayInputHeld`）へ追加済み。将来 Guard 等を追加するときも同経路へ追加する

**未実装（混同禁止）**:

| 項目 | 意味 |
|---|---|
| Air Hit / Air Knockback | **空中被弾**側の専用処理。空中攻撃ではない |
| Ground Kick | **実装済み・値は暫定**。接地中K、S/A/R=9/4/13、空中入力予約なし |
| Air Kick | **最小検証版・暫定**。空中K、1ジャンプ1回、S/A/R=5/5/10、着地即終了 |
| Animator / Character Data SO 等 | 別候補 |

## 16. Ground Kick の現行Unityデバッグ仕様

- 入力は `K`、Grounded時のみ開始
- J PunchとGround Kickは相互キャンセルしない。押下が重なる場合はJ Punchを優先する
- 他攻撃中に押した攻撃を終了後へ予約しない
- 空中でKを押す／保持したまま着地するだけでは開始しない。いったん離して再押下が必要
- J Punchは Startup 4 / Active 3 / Recovery 8、Damage 10、HitStop 6、HitStun 12、横KB 0.18
- Ground Kickは Startup 9 / Active 4 / Recovery 13、Damage 14、HitStop 7、HitStun 14、横KB 0.24
- local Hit Boxは Facing Right基準 center `(0.95, 0.55)`、half `(0.60, 0.25)`。Facing LeftではParticipantが反転する
- 1攻撃1Hit。Active中の複数CombatFrameで重なっても追加Damageしない
- 同一CombatFrameの両方向Hit候補を収集してから解決する（正本: `CollectAndResolveHitsForCombatFrame`）
- 候補発見時点ではDamage / HitStunを即時適用しない。先に片側だけ適用すると処理順で結果が変わるため
- Ground Clash: 双方候補かつ双方とも地上攻撃（現行: JPunch / GroundKick）。技種一致は条件にしない
- Ground Clash結果: Damage 0、双方HitStop、双方ClashRecoil、通常HitStunへ入れない、通常HitCount非加算、攻撃をClash終了
- 同技Clash（JPunch同士／Ground Kick同士）は旧検証フラグでEditor実測済み
- 異技Clash（**P1 GroundKick → P2 JPunch**）は鏡写しSwap＋Delay5でPlay実測済み（Damage 0）。発生差により片側Normal Hitになる場合もある（Delay4/6で実測）
- Counter Hit専用補正、Attack Priority、技の固定重み、先出し／後出し勝敗は導入しない
- Air Kickを含む双方候補はGround Clashへ分類しない。結果未適用・仮Air Clashなし・片側Normal Hitへ落とさない・MarkHitしない（正式Air Clashではない）。双方AirKick同士はPlay実測済み（§18.7）。Airを含む異種双方候補は未確認
- 分類の要約: (1) 双方候補かつ双方地上攻撃 → Ground Clash (2) 双方候補かつ片方以上がAirKick → 未適用 (3) 片側候補のみ → Normal Hit。Counter Hit／Attack Priorityなし

## 17. Air Kick最小検証版と攻撃Visualの暫定仕様

2026-07-31更新: Air Kickの攻撃ルールとS/A/R `5/5/10`は変更せず、表示だけをGround Kick流用から専用`airKickSequence`へ分離した。Startup／Activeは`FighterRebuilt_AirKick_00`〜`05`、RecoveryはJumpFallを使う。専用画像接続済みである一方、Air Hit／Air Knockback／縦Knockback／Air Clash／正式Tradeは未実装のままである。

BoxはP1/P2ともHurt／Push CenterX `0`、HalfWidth `0.75`の左右対称暫定値。Facing対応のCenterX反転と前後非対称化は未実装であり、Push Resolverの判定中心も同時にWorld Push Box中心へ揃えるまで値だけを前寄せしない。

- `K`押下エッジを一か所で処理し、CombatFrame開始時点で接地中ならGround Kick、すでに空中ならAir Kick
- 地上Up+KはGround Kick、J+KはJ Punch優先。空中Jは攻撃を開始しない
- Air Kickは1ジャンプ1回。次のジャンプ開始時に使用済み状態を解除し、着地時は途中Phaseでも即終了
- J Punch / Ground Kick / Air KickともHit Boxは`DebugAttackData.IsActiveFrame()`がtrueの間だけ有効。Recovery中はVisualに関係なくHit Boxなし
- Visualと内部状態を分離する。J PunchはAF1〜10がAttack、AF11〜15がIdle。Ground KickはAF1〜19がKick、AF20〜26がIdle。Air KickはStartup / ActiveがAirKick、RecoveryがJumpFall
- 攻撃Visualを残すRecovery前半は振り切り表現であり、追加判定ではない。VisualがIdle / JumpFallでも内部Recoveryと行動制限はTotalまで継続
- 地上／空中相手とも現行Hit Box対Hurt Boxの幾何学判定で命中可能。被弾は既存HitStun＋横KBを暫定流用
- Air Kickは専用`airKickSequence`へFlying Kick 6枚を接続済み。Startup／Activeで使用し、RecoveryはJumpFall
- Air Hit / Air Knockback / 縦KB / Air Clash / 正式Tradeは未実装。Air KickをAir Hit基盤完成とは扱わない
- 全フレーム値・Visual境界・Hit Boxは専用モーション完成後に再調整する暫定仕様
- Clashは通常Hitとは別の`ClashRecoil`状態で表示する。Damage 0、通常HitCount非加算、黄色系専用色、専用Sprite未設定時Idle fallbackをEditor確認済み

## 18. P2鏡写しDebug入力経路（Unityデバッグ・本番AIではない）

旧 `debugForceP2AttackWithP1ForClashTest` を `debugMirrorP1InputToP2` へ置換済み。Scene既定はOFF。OFF時はP2 Neutralの既存挙動を変えない。

目的: 同技／異技Clash確認、左右対称動作確認、将来のP2 AI入力ソース差し替え入口の確認。本番AIではない。

```text
P1 CurrentInput
→ UpdateDebugMirrorP2InputFromP1（左右反転・地上攻撃モード変換・地上Delay・任意でAirKick Assist）
→ p2MirrorInput
→ ResolveInputForParticipant(P2)
→ P2の通常の移動・Jump・攻撃開始処理
```

### 18.1 DebugP2MirrorAttackMode（コード既定 SameAsP1）

- SameAsP1: P1 J→P2 JPunch、P1 K→P2 GroundKick
- SwapPunchAndKick: P1 J→P2 GroundKick、P1 K→P2 JPunch（異技Clash確認用）
- NoAttack: 地上攻撃をP2へ渡さない（片側Normal Hit確認用）。左右・Jump鏡写しは維持

攻撃変換は双方接地時のみ。StartJPunch／StartGroundKickは直接呼ばない。地上変換はAir Kick非対象。FightDebugSceneのScene保存値はNoAttack（検証用。コード既定のSameAsP1とは異なり得る）。

### 18.2 debugP2MirrorAttackDelayFrames（0〜15・既定0）

鏡写しON時だけ、P2へ渡すAttack／Kickの押下開始をCombatFrame数だけ遅らせる検証補助。左右・Up・Downは遅延しない。本番AIの反応時間ではない。攻撃性能やClash条件は変えない。

流れ: 意図作成 → 立ち上がりを予約 → 予約CFでHeldを1回true → 通常SampleAttack／TryStart。OFF／NoAttack／Training Reset等で予約破棄。
**Mirror OFFおよびTraining Resetによる地上Delay予約破棄はPlay確認済み**（Swap・Delay15。発火予定CF通過で`Mirror attack delay fired`なし／P2 JPunch開始なし。Reset時は位置・HP・HitCount・攻撃状態も初期化）。Assist Delay（§18.7）とは別系統。

### 18.3 異技Clash Play実測（Swap・**P1 GroundKick → P2 JPunch**・近距離・push済み）

| Delay | 結果 |
|---:|---|
| 4 | P2 JPunch先勝ち（Normal Hit）。P1 GroundKickは同CFで候補成立前 |
| 5 | Ground Clash。双方Active／双方候補が同一CF。**P1 GroundKick → P2 JPunch** / Damage0 |
| 6 | P1 GroundKick先勝ち（Normal Hit）。P2 JPunchは同CFでまだActive前 |

これにより、処理順の有利ではなく、同じCombatFrameに双方候補があるかで結果が決まることを実測確認した。

### 18.4 検証用ログ（本番恒常ログではない）

Attack started（SimulationTick／CombatFrame／mirrorDelay／mode等）、Attack active/recovery started、Pending hit candidate、Mirror attack delay reserved/fired。AirKick Assist時は reserved／fired／cancelled（状態変化時のみ）。

### 18.5 確認状態（地上鏡写し・push済み `96f8148` / `6be8bb4`）

**確認済み**: 左右／Jump鏡写し、攻撃変換・遅延・検証ログ、NoAttack時Normal Hit、異技Clash **P1 GroundKick → P2 JPunch**（Delay5）、Delay 4/5/6境界、Mirror OFFおよびTraining Resetによる地上Delay予約破棄。

**未確認**: **P1 JPunch → P2 GroundKick** で双方候補が同一CFになる条件でのClash実測。将来のP2 Movement／Stance／Guard Mode。

### 18.6 将来のP2検証設定案（未実装）

- P2 Movement Mode: Neutral / Mirror P1
- P2 Stance／Guard Mode: Normal / Force Stand / Force Crouch / Stand Guard / Crouch Guard

Down入力の扱いはしゃがみ実装時に再検討する。

### 18.7 P2 AirKick検証アシスト（`03bfd72` / Docs `988f36f`・push済み・検証専用）

Air双方候補の未適用分岐と、未対応警告の同一Playセッション中1回制御をPlay確認するためのDebug補助。正式P2操作・本番AI・対戦ルールではない。Inspector上も検証専用。既定OFF。地上攻撃変換とは別責務。確認時は`debugP2MirrorAttackMode=NoAttack`併用を推奨。`debugMirrorP1InputToP2` ON時のみ有効。Scene既定はMirror OFF / Assist OFF / Delay 0。Play中Inspector変更はScene既定値変更ではない。

```text
P1がAirKick開始可能なKickエッジ
→ Assist予約（debugP2AirKickDelayFrames）
→ 指定CFでP2 SimulationInputStateへKickを1tick合成
→ ResolveInput → SampleAttack → TryStartKick → CanStartAirKick → StartAirKick
```

`StartAirKick`直接呼び出し・PendingHit注入なし。Jump／AttackState／Hit・Hurt Box／候補収集→分類→適用を通す。Delay 0では予約と発火が同じ入力段階CFになり得るが、Attack開始は通常処理側の次CombatFrame。発火時にP2が開始不能ならKickを載せない（GroundKick化防止）。予約はAwake／Training Reset／Mirror OFF／Assist OFF／発火成功／発火不能破棄でクリア（地上Delay予約とは別状態）。

#### Play実測（Air双方未適用・push済み）

| CF | 内容 |
|---:|---|
| 1352 | Assist reserved＋fired |
| 1353 | P1/P2 AirKick開始（同一CF） |
| 1358 | 双方Active開始・双方Pending候補・Unsupported mutual hit警告 |

警告文: `Unsupported mutual hit (includes air attack). No NormalHit / GroundClash applied until Air Clash is defined.`

結果: Ground Clash／Normal Hitなし。HP等不変。自然終了。最終HUD AttackResultは既存Miss表示。同一Playで警告1回（CF387→CF881）。

#### Assist予約安全確認（2026-08-03・未コミット・Play実測）

Delay15・NoAttack・初期距離3.0・Neutral Jump。地上Delay（§18.2）とは別系統。

| 確認 | Play結果 |
|---|---|
| Mirror OFF | 予約後Pause中にMirror OFF。発火予定CF通過でも`fired`なし／P2 AirKick・GroundKickなし（例CF670→685）。`reason=MirrorOff`明示ログは未確認 |
| Assist OFF | Mirror ONのままAssistのみOFF。同様に未発火（例CF938→953）。`reason=AssistOff`明示ログは未確認 |
| Training Reset | Pause中R。未発火（例CF681→696）。位置0.00/3.00・HitCount0・Idle・Jump解除・HitStop0・HP/KO初期化・Gameplay input再有効化。`reason=Reset`明示ログは未確認 |
| 接地時破棄 | 発火時P2着地済みで`cancelled ... reason=P2CannotStartAirKick`（CF1367／CF1856・実ログ確認）。GroundKick化なし |

複合条件: 被弾・CombatReactionを含む条件下でも誤発火しないことを確認（CombatReaction単独の専用Playではない）。

**確認済み（Assist）**: SimulationInputState経路、Air双方未適用＋警告1回、Mirror OFF／Assist OFF／Training Resetによる予約無効化、OFF/Reset後の発火予定CF通過でも古い予約が発火しない、接地時P2CannotStartAirKick、着地後GroundKick化防止、Reset後の主要状態初期化と入力再有効化。

**未確認（Assist）**: KO／HitStun／ClashRecoil等CombatReaction／ActionPlaying／AirKickUsedThisJumpの各単独破棄、Assist Delay 1〜14境界、Play再開始後の警告再出力、異種Air双方、正式Air Clash／正式P2操作・AI。
