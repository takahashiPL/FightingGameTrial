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
- 着地で空中攻撃強制終了 → LandingRecovery（フレーム数は暫定・未決定）
- 必殺技は未実装・非表示
- `state_transitions.csv` は草案（完全実行用SMではない）

### 0.2 初期検証用の暫定値

| 項目 | 扱い |
|---|---|
| JustGuardWindow | 3 SimulationTick（contact 含む直前3） |
| Damage / HitStun / BlockStun / Pushback | `moves.csv` provisional |
| 判定箱サイズ | `boxes.csv` |
| Jump 移動量 | 未収録 |
| LandingRecovery フレーム数 | 暫定・未決定 |
| ClashRecoil 量 | 未収録・暫定 |

### 0.3 未確定事項

- 空中パンチのガード可否
- 多段技、必殺技、キャラ差
- Clash 壁際の未消化転送（将来候補。初期は破棄）
- 高度な壁際補正、結果別 `transfer_ratio`
- LandingRecovery の具体フレーム数

### 0.4 空中パンチのガード（未確定）

`moves.csv` の `AIR_PUNCH.can_*_guard` は暫定値であり仕様確定ではない。

---

## 1. 操作

| 入力 | 動作 |
|---|---|
| A | 地上パンチ / 空中パンチ |
| B | 地上キック / 空中キック |
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
- 空中攻撃は1ジャンプ1回

### 2.1 着地と空中攻撃

- 着地した CombatFrame で空中攻撃を強制終了
- Active/Recovery は地上へ持ち越さない
- `LandingRecovery` へ入る（フレーム数は暫定・未決定）
- LandingRecovery 中は再ジャンプ不可

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
処理順は 移動 → Push 補正 → Facing → ActionFrame → Hit。接触後の押し分けは双方等分補正の**暫定仕様**。
Push / Hurt / Hit Box の **Game ビュー可視化**も**実装済み**である（段階11A）。
Jパンチ Hit は **Hit Box × Hurt Box の重なり判定**である（段階11B。距離判定は削除済み）。
縦方向 Push、ノックバック、画面端専用処理、飛び越え反転は未実装。
壁際の補正配分はステージ境界実装時に再検討する。

攻撃状態の正本は各 `DebugFighterParticipant.AttackState` である。
`SimulationTimeState` は SimulationTick / CombatFrame / Pause / HitStop 等の共有時間状態を持つ。

実装済み／暫定／未実装／次工程の一覧は `docs/unity_implementation_status.md` を参照する。

---

## 3. 通常技

（立ちP／立ちK／しゃがみP／しゃがみK／空中P／空中Kの内容は従来どおり。初期実装対象は立ちパンチ。）

### しゃがみキック（再掲）

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
- 空中攻撃側: Hitbox消去 → AirDeflected 系
- AttackInstance 消費

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

Unity デバッグ実装の**実際の到達点**（段階1〜11B）と次工程は
`docs/unity_implementation_status.md` を正とする。
（Hit は Hit×Hurt 重なり判定。段階11の単一 Box 基盤は完了。次工程は段階12: 被 Hit / HitStun / 被 Hit 表示。）

本番スプライトシートは未完成。参考画像を完成スプライトとしない。
