# 2D格闘ゲーム Trial 実装テンプレート v3

このパッケージは、フレーム単位の2D格闘ゲーム判定を**試作・学習**するための実装用ひな形です。

## 学習・可読性方針（重要）

このプロジェクトでは、処理効率やコードの短さより、**人間が読んで目的と処理順を追えること**を優先します。
日本語しか分からない人でも、クラスの責務と「いつ何が起きるか」が分かることを目標にします。

詳細は **`docs/learning_and_readability.md`** を読んでください。要約:

- 多少冗長でも処理順が見える構成を選ぶ
- C# では日本語コメントを十分に入れる
- 英語の名前だけで理解できる前提にしない
- Unity の `Awake` / `Update` / `FixedUpdate` 等も説明するが、**ゲーム仕様の正本は 60Hz の SimulationTick / CombatFrame**（`docs/rules.md` §10）
- 過度な LINQ・ワンライナー・難解な省略を避ける
- Inspector には Tooltip
- まず読みやすい基準実装を残し、最適化は後
- 中間状態をデバッグ表示する

### 学習用の読み順（Unity 再学習）

実装状況の確認と、Scene / Component の理解は資料を分けています。

1. **`docs/learning_and_readability.md`** … 可読性・学習方針
2. **`docs/component_and_scene_guide.md`** … FightDebugScene を題材にした Scene / Component / Inspector / 実行経路、および Managed Heap / GC / GC.Alloc / Profiler 実習の教材（§16.13 **GC-1**、§16.14 **GC-2** の実測記録あり。いずれも正式 Stage ではない）
3. **`docs/rules.md`** … ゲーム仕様（SimulationTick / HitStop などの意味）
4. **`docs/unity_implementation_status.md`** … 段階ごとの実装済み・未実装・次工程（状況確認用）。補助改善 GC-1 / GC-2 は §5.1
5. コード … 教材 §13 の推奨順（`DebugAttackData` → 入力 → ClockDriver → Session → …）

「今どこまで実装されたか」を知りたいときは 4。
「Hierarchy と Inspector とコードを結びたい」ときは 2。
「GC.Alloc を Profiler でどう見るか／GC-1・GC-2 の実測」も 2（§16）。実装状況の正本と混同しない。
GC-2（Editor）: `DebugHudView.Update` 約17.2 KB → 約3.2 KB / frame。Development Build は未計測。GC Alloc 0 ではない。

## Unity 実装の到達点（要約）

ブランチ `unity` 上で、**段階1〜15まで完了**しています。
内容反映済み基準コミット（push済みHEAD）: **`f9e56d3`**（Document air kick assist safety validation）。
過去履歴の例: `988f36f` / `03bfd72`（AirKick Assist・Air双方未適用）、`6be8bb4` / `96f8148`（地上攻撃変換・Delay・異技Clash）、`6bc5f03` / `5bd3627`（Clash整理＋基本P2鏡写し）。
今回の未コミット範囲: P2 Debug 最小立ちガード（`DebugP2StanceGuardMode`／専用GuardStun／片側Guard分岐）、GuardStun終了ログ、GuardStun値7/9、Play実測、および上記のDocs反映。
（Assist実装・Air双方未適用・Assist予約安全・地上Delay破棄Playは push済みで、今回の未コミットには含めない。）
段階14全体（14A+14B）・段階15（J Punch 攻撃データ化）: **完了・push 済み**（SO 化は見送り）。
GC-1 / GC-2（補助改善・正式 Stage ではない）: **完了・push 済み**。
Training Reset 位置・向き復帰: **実装・Editor 確認済み**（正式 Stage 番号なし）。
Visual Sequence + Sprite Sheet 移行: **実装・Editor 確認済み・push済み**（正式 Stage 番号なし）。
PixelLab 再構築版Sprite Sheet正式採用（41 sub-sprite / PPU 39）: **実装・Editor確認済み・push済み**（`5bcc3fa` / Docs `d98a99d`）。Idle／Walk／Jump／J Punch／Ground Kick／Air Kick専用Flying KickをP1/P2へ接続済み。
ジャンプ基盤（Neutral / Forward / Backward・LogicalY 軌道・Jump Visual・計測ログ）: **実装・Editor 確認済み**（正式 Stage 番号なし）。
Training Reset 共通 release gate（全操作 release まで入力抑制）: **実装・Editor 確認済み**（正式 Stage 番号なし）。
正式な次工程番号は**未定義**（新 Stage 番号は作らない）。

| 区分 | 内容 |
|---|---|
| **実装済み** | 60Hz SimulationTick、Pause/Step、HUD、HitStop、入力、左右移動、Facing、Participant / AttackState / HitState、Push Box、Box可視化、Hit×Hurt判定、J Punch、Ground Kick、**Air Kick最小検証版**、1攻撃1Hit、横ノックバック、HP/Damage、KO、Training Reset、Visual Sequence、ジャンプ基盤、**Hit候補収集→分類→適用**、**Ground Clash（同技・異技とも技種不問）**、**P2鏡写しDebug**（`debugMirrorP1InputToP2` / `DebugP2MirrorAttackMode` / `debugP2MirrorAttackDelayFrames`）、**P2 AirKick検証アシスト**（`debugEnableP2AirKickAssist`／Delay、既定OFF・検証専用） |
| **暫定** | J Punch `4/3/8`、Ground Kick `9/4/13`、Air Kick `5/5/10`。Active中だけHit Boxを出す。J PunchはAF8〜10、Ground KickはAF14〜19まで振り切りVisualを残し、後半はIdleへ戻すが内部Recoveryは継続。Air Kickは空中K・1ジャンプ1回・着地即終了。Startup／Activeは専用Flying Kick、RecoveryはJumpFall表示。Hurt／Push BoxはCenterX `0`、HalfWidth `0.75`の左右対称暫定値。Airを含む双方候補は結果未適用（正式Air Clashではない。Play実測済み）。**P2 Debug StandGuard**（未コミット・既定Normal。正式後ろ入力Guardではない） |
| **未実装（方針確定含む）** | 対戦モード進行、HPバー、**正式後ろ入力Guard／しゃがみ／Just Guard**、Animator、正式Character Data SO、複数Hit/Hurt Box、コンボ・Cancel、Counter Hit、Attack Priority、**Air Hit / Air Knockback・縦Knockback・Air Clash・正式Trade**、本番P2 AI。Air Kickは空中攻撃の最小検証であり、Air Hit基盤完成ではない |

詳細・次工程は **`docs/unity_implementation_status.md`** を正とする。
ジャンプ・Visual・Reset gate は同ファイル §1.1・§1.2 および教材 §17.7〜§17.9。

### FightDebugScene の位置づけ（方針確定）

`FightDebugScene` は**対戦モードではなく、正式な練習・検証モード**として扱う。

- 詳細 HUD・戦闘ログ・判定確認・KO 観察・R Reset を維持する
- KO 後に WIN/LOSE やラウンド終了へ進まない（KO 状態を観察できる）
- **戦闘コア**（入力〜KO 成立・見た目同期まで）は練習／対戦で共通化する方針
- KO 後に何をするかは**モード側**の責務。対戦進行は FightDebugScene に混在させない
- 対戦モード（Round / 勝敗 / タイマー / リザルト等）は**将来の別モード・未実装**

詳細は `docs/unity_implementation_status.md`（モード責務）と `docs/rules.md`（戦闘コアとモード分離）。

## ブランチ構成

| ブランチ | 内容 |
|---|---|
| `main` | 共通資料（docs / data / schemas / art） |
| `unity` | 共通資料 + Unity プロジェクト（`Game/`） |

`1f294cc`（Add initial Unity 2D project）は、**Unity プロジェクトを追加したときの基準コミット**です。
Universal 2D テンプレートで作成済みです。

### Git 管理について

Git 管理対象: `Game/Assets`、`Game/Packages`、`Game/ProjectSettings`
Git 管理外: `Game/Library`、`Temp`、`Logs`、`UserSettings`、`obj` など

## 今回Unityを選定した理由

特定エンジンの継続学習が主目的ではありません。2D座標、固定論理フレーム、入力バッファ、状態遷移、判定箱、CSV駆動を、小規模かつ明示的に実装・可視化しやすいためです。
将来は同仕様を基準に UE 版との比較余地を残します。

## 正本の優先順位

1. `docs/rules.md`（ゲーム仕様）
2. `data/*.csv`
3. `schemas/data_dictionary.md`
4. `docs/learning_and_readability.md`（実装スタイル）
5. `docs/unity_implementation_status.md`（Unity実装の到達点・暫定/正式・次工程）
6. `docs/component_and_scene_guide.md`（Scene / Component / GC.Alloc 学習教材。仕様・実装状況の正本ではない）
7. `docs/production_spritesheet_spec.md`
8. `docs/debug_screen_spec.md`
9. 各種PNG参考画像

### スプライト配置の正本

**`data/sprite_frame_requirements.csv`**
`art/spritesheet_layout.csv` と `frames.csv` の sheet 座標はこれに合わせる。参考PNGは配置正本ではない。

## データの完成状態

| データ | 状態 |
|---|---|
| `frames.csv` / `boxes.csv` | 初期検証用サンプル（Idle + StandPunch） |
| `sprite_frame_requirements.csv` | 配置計画正本（`initial` / `planned`） |
| `moves.csv` | 技設計。数値は provisional |
| `state_transitions.csv` | **草案**（完全実行用SMではない） |
| `hit_resolution.csv` | 方向ごとの一次分類参照（Tradeは複合集約・表外） |
| 本番スプライトシート（固定セル清書） | **未完成** |
| FightDebug 用 `Fighter_SpriteSheet` | **正式採用済み**（41 sub-sprite・PPU 39・実画素Trim Rect・Center Pivot・GUID `42345be00e0994144ba94bc5f1362757`） |

## 次の実装候補（要約）

正式な優先順位・次工程番号は**未決定**（順不同・新 Stage 番号は作らない）。

1. Punch 3 枚以上への素材改善（Recovery 含む）
2. ClashRecoil専用Sprite／演出の追加（現在は専用色＋Idle fallback）
3. Animator + Animation Clip
4. Air Hit / Air Knockback（**空中被弾**専用。実装済みAir Kickとは別）
5. Hurt／Push BoxのFacing対応と前後非対称化（現状はCenterX `0`、HalfWidth `0.75`の左右対称暫定値）
5. Guard
6. Character Data ScriptableObject 化（ジャンプ設定の正式データ化含む）
7. ジャンプ数値の調整（現状の実測値を踏まえたチューニング）
8. Development Build Profiler 確認（Editor 上の毎 Frame new / LINQ なしはコード確認済み）
9. 対戦モード用 Scene / Controller / HUD（FightDebugScene とは分離）
10. 既存残課題: KB壁停止、壁バウンド、壁やられ、Corner、HPバー、複数攻撃・バッファ・Cancel、攻撃データ SO 化（必要時）
11. 2P入力/AI時の実操作確認（KO中移動・攻撃禁止、P1被Hit・左方向KB など）

**混同禁止**: J Punch / Ground Kickは地上専用、Air Kickは空中K専用。空中Jは採用しない。Air KickとAir Hit / Air Knockbackは別機能。

ジャンプ基盤・Visual Sequence / Sprite Sheet・計測ログ・Training Reset 共通 release gate・飛び越し Facing は**実装済み**（未実装候補からは外す）。詳細は `docs/unity_implementation_status.md`。

## スプライト制作方針（要約）

専任デザイナーは不在。ChatGPT 支援を前提に、段階工程で作る。

1. 骨格ポーズ → 単色シルエット → 仮ドット絵 → 前後比較 → Unity連続再生 → 修正 → 本番清書
2. いきなり各フレームを独立した完成ドット絵として生成しない
3. 最初の試験対象は **Idle** と **StandPunch** のみ
4. 現在の参考画像は**完成素材ではない**
5. 詳細正本は **`docs/production_spritesheet_spec.md`**（状態は `docs/sprite_art_status.md`）

仮素材のまま判定実装を進めてよい。

## スプライト画像について

- `art/fighter_motion_reference_sheet.png` … デザイン参考。**完成スプライトではない**
- `art/spritesheet_grid_template.png` … 128×128、8×8台紙（セルサイズ。画面等倍表示の意味ではない）
- `Game/Assets/Art/Characters/Fighter_SpriteSheet.png` … FightDebug正式正本（41 sub-sprite、PPU 39、Point、Compression None、Mip Map Off、実画素Trim Rect＋Center Pivot、GUID `42345be00e0994144ba94bc5f1362757`）
- `docs/production_spritesheet_spec.md` … 制作工程とセル仕様の正本
- `docs/sprite_art_status.md` … 現在の素材状態（41 sub-sprite・Ground Kick／Air Kick接続済み）

## ディレクトリ構成

```text
Unity_FightingGameTrial
├─ README.md
├─ CHANGELOG.md
├─ art/
├─ data/
├─ docs/
│  ├─ rules.md
│  ├─ learning_and_readability.md
│  ├─ unity_implementation_status.md
│  ├─ debug_screen_spec.md
│  └─ ...
├─ schemas/
└─ Game/          ← unityブランチで Universal 2D 作成済み
   ├─ Assets / Packages / ProjectSettings  ← Git管理
   └─ Library 等                           ← Git管理外
```

## 仮値・未確定（要約）

暫定値: JustGuardWindow=3、Damage、Stun、Pushback、Jump移動量、LandingRecoveryフレーム数、デバッグ用 attackRange=1.35 など。
未確定: 多段技、必殺技、キャラ差、高度な壁際補正、Air Kick正式仕様・専用Sprite、Air Hit / Air Knockback。
詳細は `docs/rules.md` §0。

## 主要仕様（要約）

- 後ろ単独＝後退。Down+Back＝その場しゃがみ（水平移動なし）
- ガードは接触時入力。CombatState=BlockStun、GuardPosture / DisplayAnimation は別
- JustGuard は Back の新規押下エッジ（攻撃弾き型）
- Clash は一次分類。Trade は双方向とも Hit のときの複合集約
- SimulationTick（入力）と CombatFrame / ActionFrame（戦闘）を分離
- 空中 Pushbox 無効。着地で復活＋等分分離
- Facing は相手との位置関係を基本とし、移動入力と分離する。Push 補正後の位置で Facing を更新する（Unity デバッグでは段階10A/10B-3 → `docs/unity_implementation_status.md`）
- `attack_category` はラベルのみ
- 必殺技は未実装・非表示

## Ground Kick 実装メモ（2026-07-30）

正式 Stage 番号は付けない。コミット `bc80ddb` で、地上専用の Ground Kick と共通 Hit 解決基盤を追加した。

- 入力: P1 の `K`。地上専用。空中押下・空中保持から着地しても自動発生せず、押し直しが必要
- J Punch と Ground Kick は相互キャンセル／予約なし。近い同時押下では J Punch 優先
- Ground Kick: Startup 8 / Active 3 / Recovery 4、Damage 14、HitStop 7、HitStun 14、Horizontal Knockback 0.24
- local Hit Box: center `(0.95, 0.55)`、half `(0.60, 0.25)`。左右 Facing 反転を確認済み
- Sprite: `Fighter_Kick_00`〜`_04`、3 CombatFrame/枚、Loop OFF、Hold Last Frame ON（P1/P2）
- Editor確認: 通常 Hit、1攻撃1Hit、14 damage、HitStop、ノックバック、左右向き、空中開始禁止、Punch/Kick相互キャンセルなし
- Ground Clash（同技）は旧P2同時攻撃デバッグ経路でEditor実測済み。JPunch同士／Ground Kick同士とも Damage 0、HitStop、双方反動、攻撃終了、Idle復帰を確認。コミット`78c4e94`で通常Hitから`ClashRecoil`へ分離し、黄色系専用色、`P2HitCount=0`維持、専用Sprite未設定時のIdle fallbackを確認済み
- 現行: Ground Clashは技種一致を条件にしない。異技 **P1 GroundKick → P2 JPunch** は Delay5 でPlay実測済み（`96f8148` / `6be8bb4`）

上記`8/3/4`はコミット`bc80ddb`時点の履歴値。現行暫定値（push済み）は、J Punch `4/3/8`、Ground Kick `9/4/13`、Air Kick `5/5/10`。

## P2 Debug 最小立ちガード（2026-08-03・未コミット）

検証専用。正式な後ろ入力Guard／AIではない。Scene既定は`debugP2StanceGuardMode=Normal`（Play中Inspector変更はScene保存ではない）。推奨: Mirror ON + NoAttack + StandGuard。

- **仕様**: 片側候補のみGuard分岐。双方地上Clash／Air未適用は維持。専用GuardStun（JPunch 7／GroundKick 9）・Chip0・HitCount非加算・小Pushback・HitStop・AttackResult=Guard。AirKickは`CanStandGuard=false`（StandGuard有効中もNormal Hit。正式な空中攻撃ガード仕様は未決定）
- **確認済み（Play）**: JPunch／GroundKickのStand guard成立、Idle復帰、Training Resetクリア。修正後JPunch（CF798・7）／GroundKick（CF1450・9）で`GuardStun ended`。**Mode=Normalで従来Normal Hitへ復帰**。**StandGuard有効中でもAirKickはNormal Hit**
- **未確認**: P1ガード、正式な後ろ入力Guard、しゃがみGuard、Just Guard、正式Chip Damage仕様、正式な空中攻撃ガード仕様、正式P2操作／AI、Development Build

## P2 AirKick Assist予約安全確認（Docs push済み `f9e56d3`）

検証専用。Scene既定はMirror OFF / Assist OFF / Delay 0のまま（Play中Inspector変更はScene保存ではない）。

- **確認済み**: Mirror OFF／Assist OFF／Training ResetでAssist予約が無効化され、発火予定CFを越えても`fired`なし。接地時は`P2CannotStartAirKick`で破棄しGroundKick化しない。Reset後は位置・戦闘状態初期化とGameplay input再有効化。OFF/Resetの`reason=*`明示キャンセルログは未確認（未発火のPlay結果で確認）
- **複合条件**: 被弾・CombatReactionを含む条件でも誤発火しないことを確認（単独専用テストではない）
- **未確認**: KO／HitStun／CombatReaction／ActionPlaying／UsedThisJumpの各単独破棄、Assist Delay 1〜14境界、Play再開始後の警告再出力、異種Air双方、正式Air Clash

## P2 AirKick検証アシスト・Air双方未適用実測（`03bfd72` / Docs `988f36f`・push済み）

- 目的: Air双方候補の未適用分岐と警告1回制御のPlay確認用Debug補助。SimulationInputState経由。`StartAirKick`直接呼び出しなし
- **Play実測**: CF1352〜1358で双方AirKick・双方Pending・未適用＋警告。同一Playで警告1回。最終HUD AttackResultは既存Miss表示

## P2鏡写し攻撃変換・CombatFrame遅延・異技Clash実測（`96f8148` / Docs `6be8bb4`・push済み）

- `DebugP2MirrorAttackMode`／`debugP2MirrorAttackDelayFrames`、攻撃入力のCombatFrame予約／発火、検証用ログ
- **異技Clash Play実測**（Swap・P1 K・近距離）: Delay4=P2先勝ち、Delay5=Clash（**P1 GroundKick → P2 JPunch** / Damage0）、Delay6=P1先勝ち
- **確認済み**: Mirror OFFおよびTraining Resetによる**地上Delay**予約破棄（Assist Delayとは別系統）
- **未確認（継続）**: **P1 JPunch → P2 GroundKick** で双方候補が同一CFになる条件でのClash実測

## Air Kick最小検証版・攻撃フレーム調整（push済み）

- 空中で新しく`K`を押すとAir Kick。CombatFrame開始時点で接地中ならGround Kick、すでに空中ならAir Kick
- 地上`Up+K`はGround Kick、`J+K`はJ Punch優先、空中Jは攻撃なし
- Air Kickは1ジャンプ1回。着地時にStartup / Active / Recovery途中でも即終了
- 地上／空中相手とも既存Hit Box対Hurt Boxの幾何学判定で命中可能。被弾は既存HitStun＋横KBを暫定流用
- `DebugFighterVisual.airKickSequence`へFlying Kick 6枚を接続済み（`5bcc3fa`）。Startup／Activeは専用Sequence、Recoveryは従来どおりJumpFall。Ground Kickの`kickSequence`とは分離
- Play確認評価: J Punchは軽い技として見やすくなった。Air Kickは`5/5/10`で立ち相手へ当てやすくなった。Ground Kickは見た目改善が限定的で、コード不具合と断定せずSpriteの脚の伸び・シルエット不足を再制作候補とする
- Air Hit / Air Knockback / 縦KB / Air Clash / 正式Tradeは未実装

## Fighter Sprite Sheet正式採用（`5bcc3fa` / Docs `d98a99d`・push済み）

- PixelLab由来の再構築版を正式な`Game/Assets/Art/Characters/Fighter_SpriteSheet.png`として採用。GUID `42345be00e0994144ba94bc5f1362757`、41 Sprite、既存internalIDを維持した
- 192×192固定セル＋Center Pivotでは透明余白の中心が基準となり、旧素材よりキャラクターが大きく上へずれたため不採用。各セル内の実画素範囲へTrimしたRect＋Center Pivotを採用した
- 旧GUID `dcb7851d129f2305be49fac973bf47b4`はGame/Assets内参照0件を確認してから削除。旧正本は`D:\project\withAI\UNITY\Unity_FightingGameTrial_Backups\20260731_Fighter_SpriteSheet_Legacy_Before_Rebuilt_Adoption`へ退避済み
- Sub-Asset名`FighterRebuilt_...`は参照破損を避けるため意図的に維持。未整理ではない
- P1/P2の初期表示と全Sequenceを正式GUIDへ接続。正式GUID参照76件、不明internalID 0件。Air Kick専用Flying Kick表示はユーザー操作で確認済み
