# 学習方針と可読性ガイドライン

このプロジェクトは、フレーム単位の2D格闘判定を**学習・理解する**ことが目的です。  
実行速度やコードの短さより、**人間が読んで目的と処理順を追えること**を優先します。

日本語しか分からない読者でも、クラスの役割と「いつ何が起きるか」が追えることを目標にします。

関連するゲーム仕様の正本は `docs/rules.md` です。

---

## 1. 優先順位

1. 仕様どおり動くこと
2. 処理順と責務が読み取れること
3. デバッグで中間状態を確認できること
4. （将来）最適化

効率のための難読化、仕様説明の省略、短いが意図不明な書き方は避けます。

アート制作についても同様に、一発の完成結果より**追跡可能な段階工程**（骨格→シルエット→仮ドット→連続確認→清書）を優先します。詳細は `docs/production_spritesheet_spec.md`。

---

## 2. 実装時の方針（C# / Unity）

実装を始める段階で、次を守ります。

- 処理効率より理解しやすさを優先する
- 多少冗長でも、処理順が見える構成を選ぶ
- C# では**日本語コメントを十分に入れる**
- 英語の変数名・関数名だけで理解できる前提にしない
- クラスの責務、関数の目的、呼ばれるタイミングをコメントまたは Docs で説明する
- `Awake` / `Start` / `Update` / `FixedUpdate` などの Unity イベントも、「何のために使うか／使わないか」を説明する
- ゲーム仕様の進行は Unity イベントそのものではなく、**SimulationTick / CombatFrame / ActionFrame** の手続きとして書く（`docs/rules.md` §10）
- 「GameFrame」だけで済ませず、入力用tickと戦闘進行の違いをコメントで説明する
- 過度な LINQ、ワンライナー、難解な省略記法を避ける
- Inspector 項目には Tooltip を付ける（英語ID＋日本語説明）
- 将来最適化する場合も、まず読みやすい基準実装を残す
- デバッグしやすいよう、中間状態（CombatState・箱・方向別接触結果・Trade集約・AttackInstance など）を可視化する

---

## 3. 推奨する書き方の例（方針）

良い方向:

- 1GFの各ステップを、名前付きの関数やブロックコメントで区切る
- `// 11. Clash解決` のように `rules.md` の番号と対応させる
- 「なぜこの順序か」を短く日本語で書く

避ける方向:

- 接触収集・Clash・Hit適用を1つの長い式にまとめる
- 仕様にないショートカットで片方だけ先に勝たせる
- コメントなしの短縮名だけのコード

### 3.1 Hit解決とP2入力ソース（現行Unityデバッグの読み方）

Hitの正本は `CollectAndResolveHitsForCombatFrame` である。

1. P1→P2 / P2→P1 の命中候補を収集する
2. 組み合わせを分類する
3. Ground Clash または Normal Hit として適用する

候補発見時にDamage / HitStunを即時適用しない。片側だけ先にHitStunへ入れると、同じCombatFrameで成立していたもう片側の攻撃が消え、処理順で結果が変わるためである。

P2鏡写しDebug（`debugMirrorP1InputToP2`）は、戦闘ロジックを分岐させず **入力ソースだけ** を差し替える例である。

```text
P1 CurrentInput → 鏡写し／地上攻撃変換／地上Delay／任意でAirKick Assist → p2MirrorInput → ResolveInput(P2) → 通常の移動・Jump・攻撃開始
```

`DebugP2MirrorAttackMode` と `debugP2MirrorAttackDelayFrames`（0〜120）は地上Clash／Guard検証用。`JPunchAfterDelay`はP1攻撃を鏡写しせずP2 solo JPunchを繰り返すGuard検証補助（方向Neutral・Attack 1tick。Mirror Replayではない）。`P1JPunchP2GroundKickClash`は異種地上Clashの**単発**検証補助（P2 GK先行→5CF後P1 JP。正式Gameplayではない）。`debugEnableP2AirKickAssist` はAir双方未適用分岐のPlay確認用で、いずれも入力埋めの段階だけを触る。StartJPunch／StartGroundKick／StartAirKickを直接呼ばない。正式P2操作や本番AIではない。

`debugP2StanceGuardMode`（Normal／StandGuard）は**結果分類側**の検証補助である。入力ソースではなく、片側候補を Guard にするかどうかを切り替える（P2・`via=DebugStandGuard`）。P1の最小立ちガードは**正式Gameplay Back入力**（`via=Back`）で、希望条件を分離し身体条件は共通。既定NormalならP2側は通常のNormal Hit経路のまま。

学習上の教訓: 予約入力は「発火予定CFまで残る」前提で考えると危険である。Mirror OFF／Assist OFF／Training Resetで予約を捨て、発火時に接地ならKickを載せない（GroundKick化防止）ことを両系統でPlay確認した。地上DelayとAssist Delayは別状態として扱う。OFF/Resetでは`reason=*`明示キャンセルログが出ない場合があり、**発火予定CFを通過しても未発火**という結果で確認する。JPunchAfterDelay／CrossMoveClashも同様にOFF／Mode離脱／Resetで内部予約を消す。

GuardStunの自然終了は減算で0到達すれば状態が外れる。終了ログが無いだけでは状態残留とは限らない。修正後のPlayではJPunch（CF798・GuardStun=7）とGroundKick（CF1450・GuardStun=9）の双方で`[FightDebug] GuardStun ended slot=P2`を確認した。P1 Back検証では`GuardStun ended slot=P1`もPlay確認済み。

異技ClashのPlay実測では、処理順の有利ではなく **同一CombatFrameに双方候補があるか** でClash／先勝ちが分かれた。**P1 GroundKick → P2 JPunch**（Swap・Delay5）に加え、**P1 JPunch → P2 GroundKick**（単発 Assist・距離1.50・CF17227）も確認済み。Startupが異なる技を同一Activeに揃えるときは、**遅い技を先行**させる（現行 GroundKick Startup 9 と JPunch 4 の差は 5CF）。直接 `Start*` を呼ぶと通常入力経路の検証にならない。距離はタイミングAssistの責務から分離し、初回距離3.0ではMiss、1.50で双方候補とClashを確認した。ログでは「入力注入CF」「Attack started CF」「Active CF」「Pending候補CF」を分けて読む。Air双方（双方AirKick）ではGround ClashにもNormal Hitにもならず結果未適用になることと、警告が同一Playセッション中1回だけであることもPlay実測済み。候補収集→分類→適用の理解に、検証用ログ（Attack started／Pending hit candidate／delay・Assist reserved/fired／Stand guard／GuardStun ended／solo JPunch／CrossMoveClash）を使う（本番恒常ログではない）。

単発Assistとした理由: 繰り返しだとログ対応と「1回の成立確認」が曖昧になるため。Mode再入場／Resetで意図的に再実行する。
将来のP2 AIも、同じ「P2用 SimulationInputState を埋める」箇所を差し替えればよい。詳細と未確認事項は `docs/unity_implementation_status.md`。

---

## 3.2 Hurt／Push Box と Push 中心（2026-08-04）

表示だけ local CenterX をずらすと、枠と押し合い／被弾が一致しない。可視化と実判定で同じ `EvaluateWorldPushBox` / `EvaluateWorldHurtBox` を正本にする。

- Facing 反転は **CenterX の符号だけ**でよい。HalfWidth／HalfHeight／CenterY は反転不要
- `worldCenterX = LogicalX + signedLocalCenterX`（Right なら `+local`、Left なら `-local`）
- 同一 Facing 中は `ΔworldCenterX = ΔLogicalX`。Push Resolver は World 中心で overlap を求め、同じ delta を `TryMoveLogicalXBy` へ渡す
- Stage 端 Clamp で片方が動けない分は、反対側へ再配分する（必要距離を可能な範囲で確保するため）
- HUD／ログの Push 距離は **World Push 中心距離**と LogicalX 距離を混同しない（現行 HUD は World 中心）
- CenterX=`0` のときは Facing 反転や World／Logical の差が見えず、不具合が潜伏する
- 非0値の採用前は Play 中だけ Inspector で試し、Scene を保存しない検証が安全だった（検証候補例: Push `+0.05`/`0.65`、Hurt `+0.10`/`0.75`）。飛び越し後の Facing 反転も確認する
- FightDebugScene の**正式採用値**は CenterX=`0.10`／HalfWidth=`0.60`（Push／Hurtとも・保存済み）。正面端 `+0.70` を維持し背面だけ内側へ寄せた結果である。HalfWidth 正本は `Push Box Half Width`
- 現行処理順では Push は Facing 更新前、Hit は Facing 更新後（1CF差の既知制約）。順序変更は挙動影響が大きいため別タスクに分離した

---

## 4. エンジン非依存

- 論理ボタンは A / B / X / 方向として扱う
- 入力バインド（Unity Input System、UE Enhanced Input）はアダプタ層
- UVや SpriteRenderer は描画アダプタの問題であり、判定仕様の正本ではない

---

## 4.1 スプライト素材の学習メモ（FightDebug）

- 画像のピクセル寸法だけでは見かけサイズは揃わない（**PPU**・Rect・描画内容が効く）。正本は PPU **39**（旧・新 Alpha bbox から算出）。Transform Scale の場当たり調整はしない
- PixelLab由来の再構築版はIdle 8、Walk 8、Jump 9、Punch 3、Kick 7、Air Kick 6の計41 Spriteとして正式採用した
- 192×192固定Rect＋Center Pivotでは、キャラクターではなく透明余白を含むセル中心がTransform基準となり、旧素材より大きく上へずれた
- 現行FightDebug正本は各セル内の実画素外接範囲へTrimし、各Trim RectのCenter Pivotを使う。固定セル方式の失敗と、個別PivotやTransform Scaleで場当たり的に補正しない判断を記録する
- Alpha 全体重心だけでは手足・帯・髪に引っ張られる
- `framesPerSprite` は FPS ではなく、**1 枚を何 CombatFrame 表示するか**
- JumpRise / JumpFall は `loop=false` + `holdLastFrame=true` で空中姿勢の往復を防止
- 旧・新シート並存のままコミットせず、参照検索後に新 GUID を維持して正本名へ一本化する
- 旧素材は新 Scene 参照確認前に削除しない

詳細: `docs/sprite_art_status.md` / `docs/unity_implementation_status.md` §1.1

---

## 5. このファイルの位置づけ

- コーディング開始前の合意事項
- レビュー時のチェックリスト
- `README.md` からの参照先
