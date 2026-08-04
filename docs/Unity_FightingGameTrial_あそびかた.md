# Unity_FightingGameTrial あそびかた

同梱対象: `Unity_FightingGameTrial` 配布物
最終更新: 2026-08-04
対象Scene: `FightDebugScene`

---

## 1. これは何？

`Unity_FightingGameTrial` は、2D格闘ゲームの入力、移動、ジャンプ、攻撃判定、ガード、相打ち、HitStop、ノックバック、HP、KOなどを、フレーム単位で確認するための学習・検証用試作です。

現在の `FightDebugScene` は、完成した対戦ゲームではなく、判定箱や内部状態を見ながら挙動を確認する**練習・検証モード**です。

技術的な構成や実装上の考え方は、同梱の `Unity_FightingGameTrial_技術解説.md` を参照してください。

---

## 2. 起動について

配布物にWindows実行ファイルが含まれている場合は、ZIPを展開し、フォルダ構成を崩さずに `.exe` を起動してください。

Unity Editorから確認する場合は、プロジェクトを開き、`FightDebugScene` をPlayします。

> 現時点の主な確認は Unity Editor の Play Mode です。Standalone Buildやゲームパッド実機については、確認状況が異なる場合があります。

---

## 3. 基本操作

| 操作 | キー | 内容 |
|---|---|---|
| 左へ移動 | ← | P1を左へ動かします |
| 右へ移動 | → | P1を右へ動かします |
| ジャンプ | ↑ | 入力方向とFacingに応じて垂直・前・後ろジャンプを行います |
| 地上パンチ | J | 接地中にJ Punchを出します。空中では開始しません |
| キック | K | 接地中はGround Kick、空中ではAir Kickを出します |
| 立ちガード | 相手から離れる方向を保持 | 攻撃接触時にBack入力が成立していれば立ちガードします |
| Pause切替 | Space | 60Hzシミュレーションの一時停止／再開 |
| 1 Tick送り | .（ピリオド） | Pause中などに1 Tickずつ確認します |
| テストHitStop | H | 検証用HitStopを発生させます |
| テストAction | A | 検証用Actionを開始します |
| Training Reset | R | HP、KO、位置、ジャンプ、攻撃、硬直などを初期状態へ戻します |

### ガード方向について

- P1が右向きなら、左入力がBackです。
- P1が左向きなら、右入力がBackです。
- Down+Back、左右同時、前入力、無入力は、現行の最小立ちガードにはなりません。
- しゃがみガード、Just Guard、正式な空中ガードは未実装です。

---

## 4. 現在使える攻撃

### J Punch

- 地上専用です。
- 空中でJを押しても攻撃は始まりません。
- Damage、HitStop、HitStun、横ノックバックを確認できます。

### Ground Kick

- 接地中にKで開始します。
- J Punchより発生・持続・Recoveryや威力が異なります。
- J Punchとの同時押しでは、現行実装ではJ Punchが優先されます。

### Air Kick

- ジャンプ中にKで開始します。
- 1ジャンプにつき1回だけ使用できます。
- 着地すると攻撃途中でも終了します。
- 空中攻撃の最小検証版であり、Air Hit／Air Knockbackや正式なAir Clashは未完成です。

---

## 5. 何を確認するScene？

### 通常Hit

P1をP2へ近づけ、JまたはKで攻撃します。

確認できること:

- Startup → Active → Recovery
- Hit BoxとHurt Boxの重なり
- 1攻撃1Hit
- Damage
- HitStop
- HitStun
- 横ノックバック
- HP減少
- KO

### 立ちガード

相手の攻撃が当たる瞬間にBack方向を保持します。

現行の立ちガードでは、次を確認できます。

- HPが減らない
- Chip Damageは0
- HitCountは増えない
- GuardStun
- 小さなPushback
- `AttackResult=Guard`

### Ground Clash

同一CombatFrameで双方の有効な地上攻撃候補が成立するとGround Clashになります。

- J Punch同士
- Ground Kick同士
- J PunchとGround Kickの異種組み合わせ

現行では、Damage 0、HitCount非加算、共有HitStop、双方のClashRecoilを確認できます。

### ジャンプと左右入れ替わり

P1がP2を飛び越えると、位置関係に応じてFacingが反転します。

現在のHurt／Push Boxは左右非対称で、Facingに応じて前後が反転します。

---

## 6. 画面右上の P2 TEST MODE

Game画面の右上に、**P2 TEST MODE** パネルがあります。Windows Buildでもマウスで操作でき、Unity Inspectorを開かずにP2の検証反応を切り替えられます。

これは**本番AIや正式な2P操作ではありません**。検証用の入口です。Dropdown操作中もシミュレーションは継続します。

### 項目一覧

| 項目 | 内容 |
|---|---|
| NO ACTION | P2は自動攻撃・自動ガードを行いません（通常の棒立ち） |
| MIRROR J PUNCH | P1のJ Punch入力に対して、P2もJ Punchを出します |
| MIRROR GROUND KICK | P1のGround Kick入力に対して、P2もGround Kickを出します |
| J PUNCH vs GROUND KICK | P1 J PunchとP2 Ground KickのGround Clash検証用（単発Assist） |
| P2 AIR KICK | P2 Air Kick検証用 |
| STAND GUARD | P2が立ちガード状態になります |
| RESET | HP、位置、攻撃、HitStun、Knockbackなどを初期化します。**選択状態としては残らず、直前のモード表示へ戻ります** |

### Delay（Combat Frame）

一部モードだけ、Dropdown下に `DELAY : n CF` とSliderが表示されます。CFはCombat Frameです。

| モード | Delay |
|---|---|
| MIRROR J PUNCH | 0〜120 CF（Mirror Attack Delay。MIRROR GROUND KICKと共有） |
| MIRROR GROUND KICK | 同上 |
| P2 AIR KICK | 0〜15 CF（Air Kick Delay） |
| その他（NO ACTION／Clash／STAND GUARD） | Delay欄は表示しません |

RESETを選んでも、**現在のモード設定とDelay値は維持**されます（戦闘状態だけ初期化）。

### Clash検証の注意

`J PUNCH vs GROUND KICK` は距離によってMissになる場合があります。Ground Clash結果を見るには、両者が接触できる距離まで近づける必要があります。

---

## 7. 画面表示の見方

画面左側のHUDには、主に次の情報が表示されます。

| 表示 | 意味 |
|---|---|
| Tick | SimulationTick。入力履歴などに使う60Hzの時間 |
| Combat | CombatFrame。HitStop中は進みません |
| AF | ActionFrame。現在の攻撃・動作の進行位置 |
| Pause | Pause状態 |
| HS | HitStop残り |
| LRUD | 現在の方向入力 |
| Atk | 攻撃入力 |
| P1 X | P1の論理X位置 |
| Face | Facing方向 |
| St | 現在のVisual／戦闘状態 |
| HP | P1の現在HP／最大HP |
| Life | AliveまたはKO |
| Sprite | 現在表示中のVisual State |

Consoleには、攻撃開始、Active開始、Hit候補、Normal Hit、Guard、Clash、HitStop終了、攻撃終了などの詳細ログが出ます。

---

## 8. 色付きの判定箱

Gameビューには、検証用の判定箱が表示されます。

| 種類 | 役割 |
|---|---|
| Push Box | キャラクター同士の押し合いとすり抜け防止 |
| Hurt Box | 攻撃を受ける身体範囲 |
| Hit Box | Active中の攻撃範囲 |

Hurt／Push Boxの現在のScene正式値は、P1/P2とも次です。

- CenterX = `0.10`
- HalfWidth = `0.60`

右向きでは正面側が広く、背面側が内側になります。左向きではこの前後関係が反転します。

---

## 9. Training Reset

`R`キー、または右上P2 TEST MODEの **RESET** は練習モード用のTraining Resetです。

主に次を初期化します。

- P1/P2の位置と向き
- ジャンプ状態
- 攻撃状態
- HitStop／HitStun／GuardStun
- ノックバック
- HP／KO
- HitCount
- 検証用予約入力

Reset直後は、押しっぱなしの入力が誤って再発火しないよう、一度すべてのゲーム操作を離すまで入力が抑制されます。

---

## 10. 現在の制限・未実装

- 完成した対戦モードではありません
- Round、READY/FIGHT、タイマー、WIN/LOSE、リザルトは未実装です
- P2の正式なプレイヤー操作やAIは未実装です
- しゃがみガード、Just Guard、正式Chip Damageは未実装です
- Air Hit／Air Knockback、縦ノックバック、正式Air Clashは未実装です
- HPバーは未実装です
- コンボ、Cancel、Counter Hit、Attack Priority、必殺技は未実装です
- 一部のSpriteや演出は仮、または最小構成です
- Standalone Buildへの同梱想定あり（P2 TEST MODEはBuildでも切替可能）。ゲームパッドや全解像度での総合確認は未完了の場合があります

---

## 11. 困ったとき

- 操作が効かない場合は、Gameビューを一度クリックしてフォーカスを当ててください。
- Pause中ではないか確認してください。
- Training Reset直後は、方向・J・Kなどを一度すべて離してください。
- P1/P2が不自然な位置や状態になった場合はRでResetしてください。
- 赤いConsole Errorが出た場合は、その時点の操作とログを記録してください。
