# Unity 実装状況・次工程（段階1〜13A到達後）

最終更新: 2026-07-24
対象ブランチ: `unity`
最新コミット済み HEAD: **`f102e07`**（Document participant hit stun completion）
段階12Aまで: **完了・push 済み**
段階13A（Participant 共通ノックバック基盤）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜13Aの到達点

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
| **13A** | Participant 共通ノックバック基盤（横方向・固定 Combat Frame） | **実装済み・確認済み** |

段階13全体（ノックバック＋押し戻し＋ステージ端）は**未完了**。
13A で横ノックバック基盤は完了。次は既存計画どおり**段階13B（ステージ端・壁際 Push 配分）**。

### 2.1 責務分担（要約・維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterAttackState`** | 攻撃進行の正本 |
| **`DebugFighterHitState`** | 被 Hit / HitStun / **ノックバック速度**の正本（段階12A / 13A） |
| **`DebugFighterMotor`** | LogicalX の書き込み（入力移動・Push・ノックバック共通） |
| **`SimulationTimeState`** | 共有時間・**共有 HitStop** |
| **`SimulationSession`** | tick 進行、入力移動→**ノックバック**→Push→Facing→Action→Hit→Visual→HitStun消費 |

### 2.2 HitStop と HitStun の違い（段階12A・維持）

| | HitStop | HitStun |
|---|---|---|
| 所有者 | `SimulationTimeState`（試合共有） | 各 `Participant.HitState`（機体ごと） |
| 長さ | Hit 時 **6F**（既存） | 被 Hit 時 **12 Combat Frame**（暫定 `hitStunFrames`） |
| 効果 | Combat 全体停止（移動・Action・判定も止まる） | 被弾側のみ行動不能（移動入力・新規攻撃不可） |
| 進行 | SimulationTick 側で残りを減らす | **Combat 末尾**で1減らす。HitStop 中は減らさない |

### 2.3 HitStop とノックバック開始タイミング（段階13A）

| 時点 | 位置 | knockbackVelocityX | HitStun |
|---|---|---|---|
| Hit 成立 CF | **未移動**（初速だけ予約） | 符号付き初速をセット | 12 開始 |
| HitStop 6F 中 | **不変** | **減衰しない** | **減らない** |
| HitStop 終了後の各 Combat | `X += velocity` | 毎 CF 0 へ 0.015 減速 | 末尾で1減 |

流れ: Hit成立 → HitStop=6・Stun=12・初速予約（成立CFは移動なし） → HitStop中は位置・速度・Stun維持 → HitStop後の Combat から移動開始 → Stun=0 で残速度クリア・Idle / 通常色。

方向は **Hit 成立時点の LogicalX 比較**（Facing は正本にしない）:
- `attacker.X < defender.X` → 右（+）
- `attacker.X > defender.X` → 左（-）
- 同位置のみ attacker Facing を fallback

### 2.4 段階13Aで確定したノックバック責務

| 入れ物 | 担当 |
|---|---|
| **`HitState.knockbackVelocityX`** | 速度の正本（非公開） |
| **`HitState.IsBeingKnockedBack`** | `velocityX != 0` から導出 |
| **`Participant.knockbackInitialSpeed`** | 既定 **0.18**（SerializeField・暫定） |
| **`Participant.knockbackDeceleration`** | 既定 **0.015**（SerializeField・暫定） |
| **`Motor.SetLogicalX`** | 位置書き込み。Transform 直接書き込みはしない |

固定フレーム計算（`Time.deltaTime` 不使用）:
- `X += knockbackVelocityX`
- 速度を毎 Combat Frame、0 へ deceleration だけ近づける（符号越えは 0 固定）

処理順（採用）:
入力移動 → **ノックバック移動・減速** → Push → Facing → Action → Hit → Visual → HitStun消費

Push との関係:
- ノックバック後のめり込みは**同フレームの Push**で解消
- Push Resolver は速度を変更しない
- 既存 `minX=-7` / `maxX=7` は有効のまま（仕様変更なし）
- ステージ端・壁際 Push 配分は**今回変更していない**（段階13B）

HitStun 終了時: 残速度を 0 へクリア。ノックバックは HitStun 中だけ適用。
Reset（R）: 上記に加え `knockbackVelocityX=0`。位置・Facing は現行どおり維持。

### 2.5 HUD（段階12A / 13A）

- P2 Stun/State、**P2 KB Vx/Act** を状態表示（左上）へ追加
- 操作説明は左下の別 TMP（ランタイム分離維持）
- Scene 手動配線不要

### 2.6 確認済みの挙動（段階13A）

- Hit 成立（例 CF 306）: P2 X=3.85、Stun=12、KB Vx=0.180、HitStop=6、位置未移動
- HitStop 6F: Combat 停止、X=3.85・Vx=0.180・Stun=12 維持
- HitStop 後最初の Combat（例 CF 307）: X=4.03、Vx=0.165、Stun=11
- 途中（例 CF 310）: X=4.48、Vx=0.120、Stun=8
- 終了: X=5.02、Stun=0 / Idle、KB Vx=0 / Active=0、被Hit色解除、HitCount=1 保持
- 攻撃者から離れる方向。Push Dist 1.00→2.17。Facing P1=R / P2=L 維持。Box 追従
- 同一攻撃で初速二重設定なし。R Reset で HitCount/Stun/KB/HitStop/色初期化、位置/Facing 維持
- Error / Warning 0

### 2.7 未実装・未検証（段階13Aは完了扱い、段階13全体は未完了）

- ステージ端・壁、壁際 Push 補正配分（**段階13B**）
- ノックバック中に既存 minX/maxX へ到達した際の正式仕様
- 左方向ノックバックの実操作確認
- P1 が被 Hit する共通経路の実操作確認
- Y 方向ノックバック、HP / Damage / Guard / Down
- Character 固有データ化、攻撃データ SO 化
- Unity Physics は使用していない

### 2.8 相打ち・キャラ差し替え

- 相打ち: 両方向判定の土台のみ。P2 Neutral のため実動作確認は未実施
- キャラ差し替え・複数 Hurt/Hit Box: 未実装

---

## 3. Facing / Push（維持）

Facing 分離・Push 等分分離は実装済み。HitStun / ノックバック中も Push / Facing は維持（段階12A / 13A）。
壁際の片側補正配分は段階13Bで再検討。

---

## 4. 判定箱方針（維持）

Push / Hurt / Hit の可視化（11A）と Hit×Hurt 重なり判定（11B）は完了。複数 Box・データ化は未実装。

---

## 5. 推奨工程順（見直し後）

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A / 10B-2 / 10B-3** | Facing、2体共通化、Push Box | **完了** |
| **11A / 11B** | Box 可視化、Hit×Hurt 重なり判定 | **完了** |
| **12 / 12A** | 被 Hit 状態、HitStun、被 Hit 表示 | **完了** |
| **13A** | Participant 共通ノックバック基盤（横・固定CF） | **完了** |
| **13B** | ステージ端・壁際 Push 配分（既存工程表の段階13残作業） | **次回候補** |
| **13**（全体） | ノックバック＋押し戻し＋ステージ端 | **未完了**（13Aのみ充足） |
| **14** | HP、Damage、KO | 未実装 |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | 未実装 |

その後の候補（順不同・未着手）:

- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- ガード、コンボ
- Animation 本接続
- 2P 入力 / CPU、ラウンド進行（HitStun 行動制限・P1被Hit・左方向KBの実操作確認を含む）
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

- 段階1〜**13A**まで到達（横ノックバック基盤完了。段階13全体は未完了）
- 最新コミット済み HEAD: `f102e07`。段階13A は検証済み・未コミット
- HitStop中は位置・KB速度・Stunすべて維持。HitStop後の Combat から固定フレーム移動
- 速度正本は HitState、位置書き込みは Motor。Push は速度を触らない
- 次は既存計画どおり段階**13B**（**ステージ端・壁際 Push 配分**）
