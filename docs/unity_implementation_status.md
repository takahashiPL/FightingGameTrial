# Unity 実装状況・次工程（段階1〜12A到達後）

最終更新: 2026-07-24
対象ブランチ: `unity`
最新コミット済み HEAD: **`1d660fb`**（Document hit and hurt box collision completion）
段階11Bまで: **完了・push 済み**
段階12A（被 Hit / HitStun / 被 Hit 表示）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜12Aの到達点

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

既存工程表の段階12（被 Hit 状態、HitStun、被 Hit 表示）は **12A で完了**。
段階12に残る別項目は工程表上ない。次は既存計画の**段階13**。

### 2.1 責務分担（要約・維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterAttackState`** | 攻撃進行の正本 |
| **`DebugFighterHitState`** | 被 Hit / HitStun の正本（段階12A） |
| **`SimulationTimeState`** | 共有時間・**共有 HitStop** |
| **`SimulationSession`** | tick 進行、移動→Push→Facing→Action→Hit→Visual→**HitStun消費** |

### 2.2 HitStop と HitStun の違い（段階12A）

| | HitStop | HitStun |
|---|---|---|
| 所有者 | `SimulationTimeState`（試合共有） | 各 `Participant.HitState`（機体ごと） |
| 長さ | Hit 時 **6F**（既存） | 被 Hit 時 **12 Combat Frame**（暫定 `hitStunFrames`） |
| 効果 | Combat 全体停止（移動・Action・判定も止まる） | 被弾側のみ行動不能（移動入力・新規攻撃不可） |
| 進行 | SimulationTick 側で残りを減らす | **Combat 末尾**で1減らす。HitStop 中は減らさない |

流れ: Hit成立 → HitStop=6・Stun=12（成立CFでは Stun 非減算） → HitStop中 Stun 維持 → HitStop後の各 Combat 末尾で 12→11→…→0 → Idle / 通常色。

### 2.3 段階12Aで確定した被 Hit 責務

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterHitState`** | HitStunRemaining / TotalHitCount / IsInHitStun / wasHitThisCombatFrame / lastHitCombatFrame |
| **`Participant.HitCount`** | `HitState.TotalHitCount` の互換読み取り（旧独立正本は削除） |
| **`Participant.hitStunFrames`** | 既定 **12**（SerializeField） |
| **`AttackState.InterruptByHit`** | 被弾側の実行中攻撃を Miss なしで中断 |
| **表示色** | HitStop/HitStun 中は赤系 Tint。優先: **Hit > Attack > Idle** |

HitStun 中に止める: 本人の移動入力、新規攻撃開始、（被弾側）実行中攻撃。
維持: **Push 補正、Facing、Box 可視化**。ノックバック / Transform 移動はしない。

Reset（R）: 両 Participant の AttackState / HitState / HitCount / HitStun / 表示色、共有 HitStop。位置・Facing は現行どおり維持。

### 2.4 HUD（段階12A）

- P2 Stun/State、HitStop / HitStun 状態を追加
- **レイアウト**: 状態表示=左上、操作説明=左下（別 TMP）。`DebugHudView` がランタイム自動生成。Scene 手動配線不要
- 16:9 で下端切れを解消（状態行増加後の縦積みは廃止）

### 2.5 確認済みの挙動（段階12Aまで）

- 近距離 J → Hit、P2 HitCount=1、Stun=12、State=HitStop、被Hit色
- HitStop 6F: Tick のみ進行、Combat 停止、Stun=12 維持
- HitStop 後: 最初の Combat 末尾で 12→11、以降1CFごと減少
- Stun=0: Idle、色解除、HitCount は保持
- 同一攻撃で二重 Hit なし。Push/Facing/Box 維持
- R Reset で HitCount/Stun/State/AttackResult/PunchHitDone/HitStop/色が初期化。位置/Facing 維持
- HUD 16:9 で全行表示。Error / Warning 0

### 2.6 未検証・将来注意（段階12Aは完了扱い）

- P2 入力がないため、HitStun 中の移動禁止・攻撃開始禁止・攻撃中断は**コード経路確認済み／P2実操作未検証**
- 将来 P1 被 Hit（2P 入力 / AI）時に共通経路を実操作確認
- Facing Left 攻撃の実操作確認は引き続き将来項目
- ノックバック、HP、ダメージ、ガード、ダウンは未実装（段階13〜14）
- キャラ固有データ化、攻撃データ SO 化は未実装（段階15）

### 2.7 相打ち・キャラ差し替え

- 相打ち: 両方向判定の土台のみ。P2 Neutral のため実動作確認は未実施
- キャラ差し替え・複数 Hurt/Hit Box: 未実装

---

## 3. Facing / Push（維持）

Facing 分離・Push 等分分離は実装済み。HitStun 中も Push / Facing は維持（段階12A）。

---

## 4. 判定箱方針（維持）

Push / Hurt / Hit の可視化（11A）と Hit×Hurt 重なり判定（11B）は完了。複数 Box・データ化は未実装。

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A / 10B-2 / 10B-3** | Facing、2体共通化、Push Box | **完了** |
| **11A / 11B** | Box 可視化、Hit×Hurt 重なり判定 | **完了** |
| **12 / 12A** | 被 Hit 状態、HitStun、被 Hit 表示（工程表どおり。12Aで充足） | **完了** |
| **13** | ノックバック、押し戻し、ステージ端（壁際 Push 配分の再検討を含む） | **次回候補** |
| **14** | HP、Damage、KO | 未実装 |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | 未実装 |

その後の候補（順不同・未着手）:

- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- ガード、コンボ
- Animation 本接続
- 2P 入力 / CPU、ラウンド進行（HitStun 行動制限の実操作確認を含む）
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

- 段階1〜**12A**まで到達（被 Hit / HitStun / 被 Hit 表示完了。段階12は工程表どおり充足）
- 最新コミット済み HEAD: `1d660fb`。段階12A は検証済み・未コミット
- HitStop=共有6F、HitStun=機体ごと12CF。HitStop中は Stun 非減算
- HitStun 中も Push / Facing 維持。ノックバック・HP は未実装
- HUD: 左上=状態、左下=操作（ランタイム分離）
- 次は既存計画どおり段階13（**ノックバック、押し戻し、ステージ端**）
