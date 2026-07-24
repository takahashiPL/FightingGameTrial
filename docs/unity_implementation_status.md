# Unity 実装状況・次工程（段階1〜13B-1到達後）

最終更新: 2026-07-24
対象ブランチ: `unity`
最新コミット済み HEAD: **`43ac815`**（Document participant knockback completion）
段階13Aまで: **完了・push 済み**
段階13B-1（ステージ端を考慮した Participant 共通 Push 補正配分）: **検証済み・ドキュメント反映時点では未コミット**（作業ツリー）

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

## 2. 段階1〜13B-1の到達点

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
| **13B-1** | ステージ端を考慮した Participant 共通 Push 補正配分 | **実装済み・確認済み** |

既存工程表の段階13B（ステージ端・壁際 Push 配分）は **13B-1 で充足**。
段階13計画のうちノックバック基盤＋壁際 Push 再配分は完了。壁バウンド／壁やられ／KB壁停止などは工程番号を新設せず未実装として残す。
次の番号付き工程は既存計画どおり**段階14**。

### 2.1 責務分担（要約・維持）

| 入れ物 | 担当 |
|---|---|
| **`DebugFighterAttackState`** | 攻撃進行の正本 |
| **`DebugFighterHitState`** | 被 Hit / HitStun / ノックバック速度の正本 |
| **`DebugFighterMotor`** | LogicalX 書き込み・**TryMoveLogicalXBy（実移動量）** |
| **`DebugFighterPushResolver`** | 等分 Push ＋壁際未解消の再配分（段階13B-1） |
| **`SimulationTimeState`** | 共有時間・共有 HitStop |
| **`SimulationSession`** | tick 進行、入力移動→ノックバック→Push→Facing→Action→Hit→Visual→HitStun |

### 2.2 HitStop / HitStun / ノックバック（段階12A / 13A・維持）

HitStop=共有6F、HitStun=機体ごと12CF、ノックバック初速0.18／減速0.015（暫定）。
HitStop 中は位置・KB速度・Stun すべて維持。HitStop 後の Combat から固定フレーム移動。
速度正本は HitState、位置は Motor。詳細は段階13A 時点の記載を維持。

### 2.3 段階13B-1で確定した壁際 Push 配分

| 入れ物 | 担当 |
|---|---|
| **`Motor.TryMoveLogicalXBy(deltaX)`** | 要求移動を試み、minX/maxX Clamp 後の**実移動量（signed）**を返す |
| **`Motor.SetLogicalX`** | 位置の正本書き込み（Transform 直接書き込みはしない） |
| **`DebugFighterPushResolver`** | 重なり算出・半分要求・実移動取得・未解消の再配分 |
| **`SimulationSession`** | Resolver 呼び出し、壁際再配分ログの立ち上がり抑制 |

左右判定: **LogicalX**（P1/P2 名では分岐しない）。同 X 時は既存どおり呼び出しの A を左扱い。

計算手順:
1. 必要分離量 `separationNeeded` を算出
2. 左へ半分・右へ半分を要求（`TryMoveLogicalXBy`）
3. 各 Motor の実移動量を取得
4. 未解消量を**右へ**再配分
5. さらに残れば**左へ**再配分
6. 両者とも動けなければ、移動可能範囲で停止

通常位置: Clamp が働かないため従来どおり半分ずつ。壁際再配分ログは出ない。
ステージ端: 端側が動けなかった分を反対側へ再配分。`minX=-7` / `maxX=7` は既存値のまま。

Push は LogicalX のみ変更。**KnockbackVelocityX は変更しない**。Facing は Push 後。
`Time.deltaTime` / Rigidbody / Collider による物理解決は不使用。

ログ: 壁際再配分の**立ち上がり時だけ**
`[FightDebug] Push wall redistribute left=... right=... overlap=... half=... leftMoved=... rightMoved=... reToRight=... reToLeft=... afterDist=... min=...`
同一接触中の毎 Frame 大量出力はしない。

再配分順は現在「右→左」。通常の片側壁際は左右対称に検証済み。両者とも移動余地不足時の公平性・優先順位は今後の判断対象。

### 2.4 確認済みの挙動（段階13B-1）

**コンパイル**: Error 0 / Warning 0

**中央の通常 Push**:
- 例: P1 X=2.28、P2 X=3.28、Push Dist=1.00
- `Push correct` ログあり、`Push wall redistribute` なし
- 半分ずつ補正

**右端**:
- 初期テスト: P1=5.8 / P2=6.8 → 最終 P1=6.00 / P2=7.00、Dist=1.00
- P2 は maxX=7 を越えない
- ログ例: left=P1 right=P2、overlap=0.050、half=0.025、leftMoved=0.025、rightMoved=0.000、reToRight=0.000、reToLeft=0.025、afterDist=1.000
- 右側不足分を左側へ再配分

**左端**:
- 正しい初期テスト: P2=-6.8 / P1=-5.8 → 最終 P2=-7.00 / P1=-6.00、Dist=1.00
- P2 は minX=-7 を越えない
- ログ例: left=P2 right=P1、overlap=0.050、half=0.025、leftMoved=0.000、rightMoved=0.025、reToRight=0.025、reToLeft=0.000、afterDist=1.000
- 左側不足分を右側へ再配分
- 同一接触中にログが毎 Frame 大量出力されないことを確認

**Stage 13A 回帰**（Scene 初期 P1=0 / P2=3）:
- Hit: `Punch hit attacker=P1 defender=P2 CombatFrame=381 KB=0.180`
- HitStop 終了後、最終例: P1 X=2.33、P2 X=4.50、HitCount=1、Stun=0 / Idle、KB Vx/Act=0.000 / 0
- HitStop / Knockback / HitStun 終了が正常。Push による KnockbackVelocityX 変更なし

テスト注意: 左端の初期案内で P1/P2 配置を一度誤ったが実装不具合ではない。Scene / Font のテスト差分は restore 済み。

### 2.5 未実装・注意（段階13B-1は完了扱い）

- Knockback 中に壁へ到達した際の速度停止仕様
- 壁バウンド、壁やられ、Corner 専用状態
- 両者とも移動余地が不足する特殊ケースのゲーム仕様
- Y 方向 Knockback、ステージ端の視覚表示
- minX/maxX のデータ化、Character 別 Push 重量
- HP / Damage / Guard / Down（段階14）
- 再配分順「右→左」の両者不足時の公平性は今後の判断対象

### 2.6 相打ち・キャラ差し替え

- 相打ち: 両方向判定の土台のみ。P2 Neutral のため実動作確認は未実施
- キャラ差し替え・複数 Hurt/Hit Box: 未実装

---

## 3. Facing / Push（維持・13B-1更新）

Facing 分離・Push 等分分離は実装済み。壁際では Motor 実移動量に基づき未解消を反対側へ再配分（段階13B-1）。
HitStun / ノックバック中も Push / Facing は維持。Push は KB 速度を触らない。

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
| **13A** | Participant 共通ノックバック基盤 | **完了** |
| **13B / 13B-1** | ステージ端・壁際 Push 配分（13B-1で充足） | **完了** |
| **13**（計画要約） | ノックバック＋壁際 Push 再配分 | **計画項目は充足**（壁バウンド等は未実装のまま残課題） |
| **14** | HP、Damage、KO | **次回候補** |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | 未実装 |

その後の候補（順不同・未着手）:

- ノックバック壁到達時の速度停止、壁バウンド、壁やられ、Corner
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

- 段階1〜**13B-1**まで到達（壁際 Push 再配分完了。段階13Bは13B-1で充足）
- 最新コミット済み HEAD: `43ac815`。段階13B-1 は検証済み・未コミット
- 通常は半分ずつ、壁際は実移動量の不足を反対側へ再配分。Push は KB 速度を触らない
- 次は既存計画どおり段階**14**（**HP、Damage、KO**）
