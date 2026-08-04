# Unity_FightingGameTrial 技術解説

同梱対象: `Unity_FightingGameTrial` 配布物
最終更新: 2026-08-04
対象: Unity版 `FightDebugScene` の現行実装

---

## 1. この資料について

この文書は、`Unity_FightingGameTrial` の成果物に同梱し、第三者が実装内容、設計判断、確認済み範囲、未実装範囲を把握するための技術要約です。

配布物では、操作方法は同梱の `Unity_FightingGameTrial_あそびかた.md`、技術的な構成と確認範囲は本書で自己完結して読める想定です。

---

## 2. 作品概要

| 項目 | 内容 |
|---|---|
| プロジェクト名 | Unity_FightingGameTrial |
| エンジン | Unity 6.3 LTS系 |
| ジャンル | 2D格闘ゲーム戦闘コアの学習・検証 |
| 主なScene | `FightDebugScene` |
| 現在の位置づけ | 正式な練習・検証モード。完成した対戦モードではない |
| 主な学習対象 | 60Hz固定論理フレーム、入力、状態遷移、判定箱、Hit解決、HitStop、ノックバック、HP／KO、可視化 |
| 主な確認環境 | Unity Editor Play Mode |

`FightDebugScene` は、勝敗演出やラウンド進行を遊ぶSceneではなく、戦闘コアの各段階をHUD、判定箱、Consoleログで観察するためのSceneです。

---

## 3. 現在の到達点

### 実装済み

- 60Hz `SimulationTick`
- `CombatFrame`／`ActionFrame`
- Pause／1 Tick送り
- P1入力
- 左右移動とFacing
- Neutral／Forward／Backward Jump
- Ground Push
- 空中Push無効と飛び越し
- Push／Hurt／Hit Box可視化
- Facing対応した左右非対称Hurt／Push Box
- J Punch
- Ground Kick
- Air Kick最小検証版
- Hit候補収集 → 分類 → 一括適用
- Normal Hit
- Ground Clash
- 最小立ちガード
- Damage／HP／KO
- HitStop／HitStun／GuardStun
- 横ノックバック
- Training Reset
- Sprite SequenceによるVisual同期
- P2向け各種検証Assist
- **P2 TEST MODE UI**（BuildでもInspector不要で切替可能な検証入口。本番AIではない）

### 暫定または検証用

- 各技のStartup／Active／Recovery値
- Damage、Stun、Knockback量
- Air Kickの正式仕様
- 立ちガードの最小実装
- P2 Mirror／Delay／StandGuard／AirKick Assist
- ClashRecoilの専用演出

### 未実装

- Round／勝敗／タイマー／リザルト
- 正式P2操作／AI
- しゃがみガード／Just Guard／正式Chip Damage
- Air Hit／Air Knockback／縦ノックバック／正式Air Clash
- HPバー
- コンボ／Cancel／Counter Hit／Attack Priority
- 必殺技
- 正式Character Data ScriptableObject
- Animatorベースの最終アニメーション構成

---

## 4. 時間を3種類に分ける設計

本プロジェクトでは、Unityの描画Frameだけで戦闘を進めず、時間を明示的に分けています。

| 名称 | 役割 | HitStop中 |
|---|---|---|
| `SimulationTick` | 60Hzで常に進む。入力サンプリングや入力履歴 | 進む |
| `CombatFrame` | 戦闘ロジックの進行 | 止まる |
| `ActionFrame` | 現在の攻撃・動作フレーム | 止まる |

この分離により、HitStop中でも入力観測を継続しながら、攻撃、移動、ジャンプ、硬直などの戦闘進行だけを停止できます。

---

## 5. 1 CombatFrameの主な処理順

現在のFightDebugSceneでは、概ね次の順で処理します。

```text
入力サンプリング
  → 入力移動
  → ノックバック移動・減速
  → Push補正
  → Facing更新
  → ActionFrame進行
  → Hit候補収集
  → 候補分類
  → 結果一括適用
  → Visual更新
  → 硬直残り消費
```

同一CombatFrame内の攻撃候補を即時適用せず、一度すべて収集してから分類するのが重要です。

これにより、P1を先に処理したかP2を先に処理したかというコード順依存を避け、Ground Clashや双方Hit候補を公平に判定できます。

---

## 6. Hit候補収集・分類・適用

Hit解決は次の3段階です。

### 1. 候補収集

- P1 → P2
- P2 → P1

をそれぞれ独立して調べます。

攻撃側のWorld Hit Boxと、防御側のWorld Hurt Boxが重なり、攻撃対象条件などを満たしたとき、Pending Hit候補になります。

### 2. 分類

現行の主な分類は次です。

| 条件 | 結果 |
|---|---|
| 双方候補かつ双方とも地上攻撃 | Ground Clash |
| 双方候補かつ片方以上がAir Kick | 現在は結果未適用 |
| 片側候補＋立ちガード成立 | Guard |
| 片側候補＋ガード不成立 | Normal Hit |

### 3. 一括適用

分類結果に応じて、Damage、HitStop、HitStun、GuardStun、Knockback、ClashRecoil、AttackResultなどを適用します。

---

## 7. 攻撃データと攻撃状態

現行の攻撃値は、読み取り専用の `DebugAttackData` にまとめています。

主なデータ:

- Attack ID
- Startup
- Active
- Recovery
- Damage
- HitStop
- HitStun／GuardStun
- Horizontal Knockback
- local Hit Box
- Guard可能フラグ

攻撃状態は各Participantの `DebugFighterAttackState` が保持します。

現行の主要攻撃は次です。

| 攻撃 | 入力 | 状態 |
|---|---|---|
| J Punch | 接地中J | 地上専用・実装済み |
| Ground Kick | 接地中K | 実装済み・数値暫定 |
| Air Kick | 空中K | 最小検証版・1ジャンプ1回 |

空中Jは採用していません。

---

## 8. Hurt／Push／Hit Box

### 役割分担

| Box | 役割 |
|---|---|
| Push Box | キャラクター同士の重なり解消 |
| Hurt Box | 攻撃を受ける身体範囲 |
| Hit Box | Active中の攻撃範囲 |

### localからWorldへの変換

BoxはFacing Right基準のlocal値を持ち、Facing LeftではCenterXを反転します。

```text
Facing Right:
worldCenterX = LogicalX + localCenterX

Facing Left:
worldCenterX = LogicalX - localCenterX
```

HalfWidthは大きさなので反転しません。

### FightDebugSceneの正式値

P1/P2とも、現在のPush／Hurt Boxは同じ値です。

- CenterX = `0.10`
- HalfWidth = `0.60`

右向き:

```text
左辺 -0.50 / 右辺 +0.70
```

左向き:

```text
左辺 -0.70 / 右辺 +0.50
```

正面側の端を維持しつつ、背面側を内側へ寄せています。

PushとHurtは別々に設定できる構造を維持していますが、現時点では、根拠のない差を持ち込まないため同値にしています。

### 表示と実判定の正本統一

可視化だけCenterXをずらし、実判定がLogicalX中心のままだと、見た目と衝突が一致しません。

現在は、Push／Hurtの表示と実判定が同じWorld評価を使用します。Push ResolverもWorld Push Box中心とHalfWidthを基準に重なりを解消します。

---

## 9. Pushと壁際再配分

地上でPush Boxが重なった場合、必要分離距離を計算し、基本的に左右へ等分して移動させます。

```text
必要距離 = 左HalfWidth + 右HalfWidth
現在距離 = 右WorldCenterX - 左WorldCenterX
不足量   = 必要距離 - 現在距離
```

Stage端Clampで片方が十分に動けない場合は、未解消分を反対側へ再配分します。

これにより、壁際でもPush Boxの重なりを残しにくくしています。

### 既知の1CF制約

現在の順序では、PushはFacing更新前、HitはFacing更新後に評価されます。

左右入れ替わりが起きる厳密な1 CombatFrameでは、PushとHitが参照するFacingのタイミングに1CF差が生じる可能性があります。

現状の通常飛び越し、Facing反転、Push、逆向きHitはPlay確認済みですが、この順序変更は別課題として未実施です。

---

## 10. Facingと入力の分離

左右キーはワールド上の移動方向を示し、Facingは相手との位置関係から更新します。

そのため、同じ左入力でも、Facingによって前進／後退のVisual Stateが変わります。

- 右向き＋右移動 → WalkForward
- 右向き＋左移動 → WalkBackward
- 左向き＋左移動 → WalkForward
- 左向き＋右移動 → WalkBackward

飛び越し後も、位置関係に応じてFacingと左右非対称Boxが反転します。

---

## 11. Guardの現行実装

現在は最小立ちガードを実装しています。

### P1 Gameplay Back Guard

- Facing基準のBackのみ保持
- 接地中
- 非KO
- 攻撃中、HitStun中、GuardStun中などではない
- 正しく相手を向いている
- 技がStand Guard可能

Down+Back、左右同時、Forward、Neutralは立ちガードにしません。

### 成立時

- Damage 0
- Chip Damage 0
- HitCount非加算
- 共有HitStop
- 専用GuardStun
- 小Pushback
- `AttackResult=Guard`

しゃがみガード、Just Guard、正式Chip Damage、正式な空中攻撃ガードは未実装です。

---

## 12. Ground Clash

同一CombatFrameで、双方の有効な地上攻撃候補が成立した場合にGround Clashとなります。

技種一致は条件ではありません。

確認済み例:

- J Punch × J Punch
- Ground Kick × Ground Kick
- Ground Kick × J Punch
- J Punch × Ground Kick

現行結果:

- Damage 0
- HitCount非加算
- 共有HitStop
- 双方ClashRecoil
- 通常Hitへ落とさない

Counter HitやAttack Priorityは未実装です。

---

## 13. JumpとAir Kick

JumpはRigidbody物理を正本にせず、LogicalYとCombatFrame固定計算で進めます。

- Neutral Jump
- Forward Jump
- Backward Jump
- JumpStart／Rise／Apex／Fall／Landing
- 空中Push無効
- 飛び越し可能
- 着地後に地上Push復活

Air Kickは空中攻撃の最小検証版です。

- 空中でKを新規押下
- 1ジャンプ1回
- Startup／Active／Recovery
- 着地時即終了

ただし、Air Hit／Air Knockback、縦方向の被弾、打ち上げ、正式Air Clashは未実装です。

---

## 14. HitStop、HitStun、Knockback

### HitStop

戦闘全体のCombatFrame／ActionFrameを停止します。SimulationTickは進みます。

### HitStun

被弾側だけの行動不能時間です。HitStop終了後も継続します。

### Knockback

Hit成立時に横速度を予約し、HitStop中は移動・減速しません。HitStop終了後のCombatFrameから固定量で移動・減速します。

`Time.deltaTime`を戦闘正本に使わず、60Hz論理フレームで再現性を保つ方針です。

---

## 15. HP、KO、モード責務

HPが0になるとKOへ移行します。

- 最後のHitStop／HitStun／Knockbackは通常どおり成立
- KO中は本人の移動・新規攻撃を禁止
- KO済み防御者への追加Hitを拒否
- KOはTraining Resetまで維持

`FightDebugScene`では、KO後も観察を続けます。

Round終了、WIN／LOSE、次Round、Match Resultなどは、将来の対戦モード側の責務であり、FightDebugSceneには混在させません。

---

## 16. Training Reset

RによるTraining Resetは、練習モードの正式機能です。

初期化対象:

- P1/P2のLogicalX／LogicalY
- Jump状態
- Facing
- 攻撃状態
- HitStop／HitStun／GuardStun
- Knockback
- HP／KO
- HitCount
- AttackResult
- 検証用Assist予約

Resetを受理したUnity Updateでは、通常SimulationTickへ進まずreturnします。

また、Reset直後の押しっぱなし入力による再ジャンプ・再攻撃・再移動を防ぐため、全ゲーム操作を一度離すまで有効入力を抑制します。

---

## 17. Visual構成

現在はAnimatorを主軸にせず、`FighterVisualState`とSprite Sequenceで状態を表示します。

主なVisual State:

- Idle
- WalkForward／WalkBackward
- Attack
- Kick
- JumpStart／JumpRise／JumpApex／JumpFall／Landing
- HitStun
- ClashRecoil
- KO

FightDebug用のSprite Sheetは41 sub-sprite構成で、Idle、Walk、Jump、J Punch、Ground Kick、Air KickをP1/P2へ接続しています。

一部は専用Spriteや演出が未完成で、fallbackや色表示を使います。

---

## 18. 代表的な責務分担

| Component／Class | 主な責務 |
|---|---|
| `SimulationClockDriver` | 実時間から60Hz SimulationTickを進める。Pause／Step／Reset入口 |
| `SimulationSession` | 1 Tickの戦闘処理順、候補収集、分類、結果適用、Visual決定 |
| `SimulationTimeState` | SimulationTick、CombatFrame、Pause、HitStop |
| `DebugGameplayInput` | P1の物理入力観測 |
| `DebugPlaybackInput` | Pause、Step、テストHitStop、Reset等の押下エッジ |
| `DebugFighterParticipant` | HP、KO、AttackState、HitState、BoxのWorld評価 |
| `DebugFighterMotor` | LogicalX／Y、移動、Stage端Clamp、Jump軌道 |
| `DebugFighterAttackState` | 攻撃ID、ActionFrame、1攻撃1Hit管理 |
| `DebugFighterHitState` | HitStun、GuardStun、Knockback速度 |
| `DebugFighterPushResolver` | World Push Box重なり解消と壁際再配分 |
| `DebugPunchHitResolver` | Hit Box × Hurt Boxの幾何判定 |
| `DebugFighterVisual` | Sprite Sequenceと表示同期 |
| `DebugHudView` | HUD表示。戦闘状態の正本ではない |

---

## 19. 検証用P2 Assistと P2 TEST MODE UI

P2は本番入力やAIではなく、検証を再現しやすくするAssistを持ちます。

代表例（内部）:

- P1入力の鏡写し
- Punch／Kick変換
- CombatFrame遅延
- P2 Debug StandGuard
- P2 AirKick Assist
- P2単独JPunch繰り返し
- P1 JPunch × P2 GroundKick Clash単発Assist

これらは、通常の `SimulationInputState` 経路を通して戦闘コアを検証するための機能です。Scene既定では通常動作へ影響しない設定を基本とし、本番P2操作／AIとは区別します。

### 画面右上 UI（Build向け検証入口）

| 要素 | 役割 |
|---|---|
| `DebugP2TestMode` | UI用モードenum（RESETは含めない） |
| `DebugP2TestModeView` | DebugCanvas上で実行時にパネル生成。既存EventSystemを再利用 |
| `SimulationSession.ApplyP2TestMode` | 関連Debug設定を一度安全解除し、必要項目だけON |
| `SimulationSession.ResetTrainingFromUI` | 既存Training Reset（Rキーと同経路） |
| Delay Getter／Setter | Mirror Delay 0〜120／Air Kick Delay 0〜15。Session側Clamp。正本はInspector SerializeField |

方針:

- UIからprivate Debug fieldを直接触らない
- RESETはenumに追加せず、Dropdown index=6の一時コマンド。実行後も`ActiveP2TestMode`とDelayを維持し、表示は直前モードへ戻す
- Mirror J Punch／Ground Kickは同じMirror Delayを共有。P2 Air Kickは専用Delay。Clash／NoAction／StandGuardではDelay UI非表示
- UIは実行時生成。`TMP_DefaultControls`とUnity 6で存在しない`UI/Skin/UIMask.psd`等は使わない（`RectMask2D`、`Image.sprite=null`、明示色）
- CaptionとArrowTextを分離し「▼」を右端固定

---

## 20. 確認済みの主な項目

- 60Hz Tick、Pause、Step
- 左右移動とFacing
- Jump、着地、飛び越し
- 左右入れ替わり後のFacing反転
- Hurt／Push／Hit Box可視化
- 非対称BoxのFacing反転
- World Push中心による分離
- Stage端Clampと壁際再配分
- J Punch Normal Hit
- Ground Kick Normal Hit
- Air Kick最小動作
- Damage、HitStop、HitStun、Knockback
- HP、KO、KO後追加Hit拒否
- 最小立ちガード
- 同技／異技Ground Clash
- Training Reset
- Sprite Sequence同期
- Console Errorなしの個別Play確認
- **P2 TEST MODE UI**（右上表示、全Dropdown項目、▼常時表示、Delay切替、RESET一時コマンド、Inspector Delay同期、Error/Warning 0件）

正式値 `CenterX=0.10 / HalfWidth=0.60` 採用後のGuard、Clash、Knockback中密着Pushなどは、追加回帰対象として残っています。

**未確認**: Windows Build実機でのP2 TEST MODE操作（Editor Play確認済み）。

---

## 21. 制限・今後の課題

| 項目 | 状態 |
|---|---|
| 対戦モード | 未実装。FightDebugSceneとは分離予定 |
| P2正式操作／AI | 未実装 |
| しゃがみ／Just Guard | 未実装 |
| 正式Chip Damage | 未実装 |
| Air Hit／Air Knockback | 未実装 |
| 正式Air Clash／Trade | 未実装 |
| HPバー | 未実装 |
| コンボ／Cancel／Counter／Priority | 未実装 |
| 必殺技 | 未実装 |
| Animator最終構成 | 未実装 |
| Character Data SO | 未実装 |
| Clash／HitStun／KO等の専用Sprite改善 | 今後候補 |
| Windows Build実機（P2 TEST MODE含む） | Editor Play確認済み。Build実機は未確認 |
| Development Build Profiler | 未確認 |
| ゲームパッド実機 | 未確認 |

---

## 22. 配布物について

配布物には、Unity Build 本体と、同梱の `Unity_FightingGameTrial_あそびかた.md`／本書（技術解説）を含めます。

操作方法はあそびかた、技術的な構成・確認済み範囲・未実装範囲は本書で把握できる想定です。

開発用リポジトリには、より詳細な処理順・Inspector設定・検証ログ・失敗例・設計理由などの追加資料が存在する場合があります。それらは**配布物には含まれません**。
