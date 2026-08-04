# CHANGELOG

## 未コミット: Hurt／Push Box Facing対応と Push World中心化（2026-08-04）

前提（push済み）: 異種Clash Assist `ab6b938` / P1 Back・JPunchAfterDelay `0a4886e` / P2 Debug StandGuard `446f053`。

### 実装（C#のみ・Scene未変更）
- **Changed** `EvaluateWorldHurtBox` / `EvaluateWorldPushBox`: local `CenterX` を Facing に応じて反転（Hit Box と同型。Facing Right=`LogicalX+local`／Left=`LogicalX-local`）
- **Changed** `DebugFighterPushResolver`: overlap／左右／必要距離を Participant `LogicalX` 中心ではなく `EvaluateWorldPushBox()` の World 中心＋HalfWidth 基準へ
- World 中心の移動 delta を同じ量だけ `TryMoveLogicalXBy` へ適用。Stage端 Clamp・壁際再配分は維持
- **Changed** `SimulationSession`: HUD／空中 Push skip 時の `lastPushCenterDistance` を World Push 中心距離へ統一
- CombatFrame 内の処理順（移動→KB→Push→Facing→Action→Hit→Visual）は**変更なし**（既知制約として維持）
- FightDebugScene の既定値は未変更（Hurt／Push CenterX=0、HalfWidth=0.75）

### Play確認（Scene保存なし・Play中Inspector一時値）
- 一時値（案A・P1/P2両方）: Hurt CenterX=`0.10` HalfWidth=`0.75`／Push CenterX=`0.05` HalfWidth=`0.65`
- 正面対向: 枠が相手側へ前寄り。`Push correct ... dist 1.25->1.30 min=1.30`。壁際再配分 `afterDist=1.300 min=1.300`
- 飛び越し後左右入れ替わり: Facing・前寄り CenterX が反転。World Push で `dist ... ->1.30 min=1.30`
- 左向き JPunch（入れ替わり後）: Pending→Normal Hit Damage=10・HitStop／KB／HitCount=1。非0 Hurt CenterX の Facing 反転が実判定で使用されることを確認
- Training Reset・正面接触・すり抜けなし・Console Error／新規 Warning なし
- Play終了後、P1/P2とも CenterX=0／HalfWidth=0.75 へ戻り、Scene差分なしを確認
- **未確認／未実施**: Guard／Ground Clash の追加回帰、KB中・HitStun中密着Push詳細、Air Push skipログ実測、入れ替わり厳密1CFのFacing差観察、案AのScene既定採用判断

## P1 JPunch × P2 GroundKick 異種Clash Assist（push済み `ab6b938`）

前提（push済み）: P1 Back／JPunchAfterDelay `0a4886e` / P2 Debug StandGuard `446f053`。

### 実装
- **Added** `DebugP2MirrorAttackMode.P1JPunchP2GroundKickClash`（=4）: 異種地上技Clashの**単発**検証専用 Assist（正式Gameplay／AIではない）
- P2 GroundKick を先行し、Startup差（GroundKick 9 − JPunch 4 = **5CF**）後に P1 JPunch を発火
- 双方とも `SimulationInputState` 経由（`StartJPunch`／`StartGroundKick` 直接呼び出しなし）。P2方向は Neutral 固定
- 位置・HitBox／HurtBox／PushBoxは変更しない。距離はユーザー調整（推奨 1.5〜2.0）
- Mirror OFF／Mode離脱／Training Reset／Awake で状態クリア。Mode再入場で再実行可
- P2発火不能時は予約破棄して再待機。P1発火不能時は試行終了（自動再試行なし）
- 既存 SameAsP1／Swap／NoAttack／JPunchAfterDelay／AirKick Assist の挙動は維持。Scene既定は NoAttack のまま

### Play確認
- 距離 **1.50**: P2 GK started CF17218 → P1 JP started CF17223（差5）。双方 Active／Pending 同一 CF17227 → **Ground Clash**（P1=JPunch／P2=GroundKick／Damage=0）
- AttackResult=Clash、HitCount増加なし、Normal Hitなし、Stand Guard分岐なし、HitStop後 Action停止、双方 ClashRecoil
- Assistは単発（追加自動発火なし）

## P1 Back StandGuard と P2 solo JPunch 繰り返し Assist（push済み `0a4886e`）

前提（push済み）: P2 Debug StandGuard `446f053` / Assist予約安全Docs `f9e56d3` / Assist実装 `03bfd72` / Air双方Docs `988f36f`。

- **Added** P1 Gameplay Back 保持による最小立ちガード（Facing基準・接触CF・入力履歴なし）。ログ `via=Back`
- **Excluded** Down+Back・Left+Right同時・Forward／Neutral を立ちガードから除外（しゃがみガード自体は未実装。Down+Back除外は将来のしゃがみガード候補を守るため）
- **Added** `DebugP2MirrorAttackMode.JPunchAfterDelay`: P1攻撃を鏡写しせず、P2だけが Delay 間隔で JPunch を繰り返す Guard 検証専用 Assist（Mirror Replayではない）
- JPunchAfterDelay中のP2入力: 方向・Kickは Neutral 固定。Attack のみ発火CFの1tick。`StartJPunch`直接呼び出しなし
- 繰り返し: 攻撃可能→Delay→Attack 1tick→通常経路でJPunch→終了待ち→終了後から次Delay。攻撃中は次Attackを重ねない。開始不能時は可能になってからDelay数え直し
- Mirror OFF／Mode離脱／Training Reset／Awake で Assist 内部状態をクリア
- **Expanded** `debugP2MirrorAttackDelayFrames` を 0〜120 CombatFrame（60Hzで60CF≒1秒・120CF≒2秒）。SameAsP1／Swapの選択範囲のみ拡張（処理内容は変更なし）。AirKick Assist Delayは従来どおり 0〜15
- **Play確認**: P2がDelayごと繰り返しJPunch（Delay=60／90）。攻撃重複なし・方向移動なし・P1自動攻撃なし。P1 Backのみで `via=Back`・GuardStun=7・Chip0・`GuardStun ended slot=P1`。右向き／左向きの Facing 切替確認。Neutral／Forward／Left+Right／Down+Backは Normal Hit。Console赤エラーなし
- 注: Play中にInspectorスライダーをドラッグすると中間値で1サイクル予約されることがある（異常ではない。通常はPlay前にDelay設定）

## P2 Debug 最小立ちガード（push済み `446f053`）

前提（push済み）: Assist実装 `03bfd72` / Air双方Docs `988f36f` / Assist予約安全Docs `f9e56d3`。

- `DebugP2StanceGuardMode`（Normal／StandGuard・Scene既定Normal）: P2専用の立ちガード検証補助。正式後ろ入力・AIではない。ログ `via=DebugStandGuard`
- 分類: 双方地上Clash／Air未適用は維持。片側候補のみ `TryApplyStandGuard` → Guard、失敗時 Normal Hit
- 専用`GuardStun`（HitStun非流用）・Chip0・HitCount非加算・小Pushback・共有HitStop・`MarkGuarded`（AttackResult=Guard）
- GuardStunFrames: JPunch **7**／GroundKick **9**（初回実装の8／10は指定ずれのため修正。修正後JPunch=7／GroundKick=9をPlay再確認）
- 終了ログ: SessionがTick前後比較で`[FightDebug] GuardStun ended slot=`（**JPunch CF798／GroundKick CF1450でPlay確認済み**）
- AirKick: `CanStandGuard=false`。StandGuard有効中も Normal Hit（Play確認済み）。正式な空中攻撃ガード仕様は未決定
- **Play確認**: 修正前JPunch（CF1478・当時8）／GroundKick（CF3114・当時10）で成立・Idle復帰。Training ResetでGuardStun／HitStopクリアと状態初期化。修正後JPunch（CF798・7）／GroundKick（CF1450・9）で`GuardStun ended slot=P2`（各1回）。**Mode=Normalで`Normal hit attack=JPunch`・Damage=10・HitCount=1**。**StandGuard中AirKickは Normal Hit・Damage=14・HitCount=1（Stand guardログなし）**

## P2 AirKick Assist予約安全確認（Docs push済み `f9e56d3`）

前提（push済み）: Assist実装 `03bfd72` / Air双方未適用Docs `988f36f`。地上Delay破棄Playは既確認済み（地上Delayとは別系統）。

- **Mirror OFF**: Delay15予約（例CF670→fire685）後、Pause中にMirror OFF。発火予定CF通過でも`fired`なし／P2 AirKick・GroundKick開始なし。`reason=MirrorOff`明示ログは未確認（予約→OFF→未発火のPlay結果で確認）
- **Assist OFF**: Mirror ON維持のままAssistのみOFF（例CF938→fire953）。発火予定CF通過でも`fired`なし／P2攻撃開始なし。`reason=AssistOff`明示ログは未確認
- **Training Reset（Pause中R）**: Delay15予約（例CF681→fire696）後、Pause中にR。`Training reset (...LogicalX/LogicalY/Jump/Facing)`ログあり。発火予定CF通過でも`fired`なし。位置0.00/3.00・HitCount0・Idle・Jump解除・HitStop0・HP/KO初期化・Gameplay input再有効化。Pause中Rの受理・主要状態初期化・Assist予約破棄・解除後input再有効化はPlay確認済み。詳細総合回帰は未確認。`reason=Reset`明示ログは未確認（Training Resetログ＋未発火で確認）
- **接地時破棄**: 発火時P2着地済みで`cancelled ... reason=P2CannotStartAirKick`（CF1367／CF1856）。AirKickなし・GroundKick化なし（実ログ確認済み）
- **複合条件**: P1 AirKickがP2へNormal Hitした試行で、発火予定時にP2がCombatReaction中となり誤発火なし。CombatReaction単独の専用Playではない
- **確認済み**: AssistのMirror OFF／Assist OFF／Training Resetによる予約無効化、OFF/Reset後の発火予定CF通過でも古い予約が発火しない、接地時P2CannotStartAirKick、着地後GroundKick化防止、Reset後の主要戦闘状態初期化と入力再有効化
- **未確認**: KO／HitStun／ClashRecoil等CombatReaction／ActionPlaying／AirKickUsedThisJumpの各単独破棄、Assist Delay 1〜14境界、Play再開始後の警告再出力、異種Air双方、正式Air Clash／正式P2操作・AI

## P2 AirKick検証アシスト・Air双方未適用実測（`03bfd72` / Docs `988f36f`・push済み）

- `debugEnableP2AirKickAssist`（既定OFF）／`debugP2AirKickDelayFrames`（0〜15・既定0）: Air双方候補の未適用分岐と警告1回制御の検証専用補助。正式P2操作・本番AIではない。SimulationInputState経由（`StartAirKick`直接呼び出しなし）
- **Play実測**: CF1352〜1358で双方AirKick・双方Pending・未適用＋警告。同一Playで警告1回（CF387→CF881）。最終HUD AttackResultは既存Miss表示
- Scene既定: Mirror OFF / Assist OFF / Delay 0。Play中Inspector変更はScene既定値変更ではない

## P2鏡写し攻撃変換・CombatFrame遅延・異技Clash実測（`96f8148` / Docs `6be8bb4`・push済み）

- `DebugP2MirrorAttackMode`: SameAsP1 / SwapPunchAndKick / NoAttack（当時。後に `JPunchAfterDelay` を追加・未コミットDocs）。双方接地時のみ攻撃入力変換。Air Kick非対象。Start技の直接呼び出しなし
- `debugP2MirrorAttackDelayFrames`（当時0〜15、既定0。後に0〜120へ拡張・未コミット）: P2 Attack/Kick押下開始だけをCombatFrame予約／発火。検証専用（本番AI反応時間ではない）
- **Play実測（SwapPunchAndKick・P1 K・近距離）**: Delay4=P2 JPunch先勝ち、Delay5=異技Ground Clash（**P1 GroundKick → P2 JPunch** / Damage0）、Delay6=P1 GroundKick先勝ち
- **確認済み**: 攻撃変換モード、CombatFrame遅延、検証ログ、**P1 GroundKick → P2 JPunch**（Delay5）、Delay 4/5/6境界、NoAttack時Normal Hit、**Mirror OFFおよびTraining Resetによる地上Delay予約破棄**（Delay15・Swap・発火予定CF通過でfiredなし／P2 JPunch開始なし）
- **逆向き（P1 JPunch → P2 GroundKick）**: 本節時点では未確認だった。後続 Assist `P1JPunchP2GroundKickClash`（push済み `ab6b938`）でPlay実測済み
- 注: Assist Delay（`debugP2AirKickDelayFrames`）は別系統。地上Delayと混同しない

## Fighter Sprite Sheet再構築版の正式採用（`5bcc3fa` / Docs `d98a99d`・push済み）

- PixelLab由来の再構築版を正式な`Game/Assets/Art/Characters/Fighter_SpriteSheet.png`へ整理。GUID `42345be00e0994144ba94bc5f1362757`と41 SpriteのinternalIDを維持
- 192×192固定セル＋Center Pivotは透明余白中心が基準となり表示が上へずれたため不採用。各セル内の実画素Trim Rect＋Center Pivotを正式方式とした
- 旧GUID `dcb7851d129f2305be49fac973bf47b4`のGame/Assets参照を0件にして削除し、旧PNG/metaはAssets外へバックアップ
- P1/P2のSpriteRenderer初期表示とIdle／Walk／Jump／Punch／Ground Kickを正式GUIDへ統一。Scene内正式GUID参照76件、不明internalID 0件
- `DebugFighterVisual.airKickSequence`を追加し、`FighterRebuilt_AirKick_00`〜`05`を1CF/枚、Loop OFF、Hold Last Frame ONで接続。RecoveryはJumpFallを維持
- Unity Import、コンパイル、Missing Sprite／参照例外なしを確認。IdleとAir Kick専用Flying Kick表示を確認済み
- Hurt／Push BoxはCenterX 0、HalfWidth 0.75の左右対称暫定値。Facing対応と前後非対称化はPush Resolverを含む後工程へ保留（**当時の記録**。現行の Facing反転＋World Push中心化は未コミットC#／本CHANGELOG先頭節）

## Air Kick最小検証版・通常技暫定調整（`f19cc22` 系〜専用Sequence `5bcc3fa`・push済み）

- `AirKick`をGround Kickとは別の`DebugAttackId` / `DebugAttackData` / `FighterVisualState`として追加
- K入力を接地状態で一括分岐。接地中はGround Kick、すでに空中ならAir Kick。地上Up+KはGround Kick、J+KはJ Punch優先、空中Jは攻撃なし
- Air Kickは1ジャンプ1回、着地即終了。後続の正式Sprite採用で専用`airKickSequence`へ接続済み
- 暫定値: J Punch `S/A/R=4/3/8`、Ground Kick `9/4/13`、Air Kick `5/5/10`
- Active中だけHit Boxを有効化。Recovery中は内部行動不能を維持し、J Punch AF8〜10 / Ground Kick AF14〜19は振り切りVisual、後半はIdle。Air Kick RecoveryはJumpFall
- Play確認: J Punchは軽い技として見やすく、Air Kickは立ち相手へ以前より当てやすい。Ground Kickの見た目改善は限定的で、脚の伸び・シルエットを改善したSprite再制作後に再調整する
- Air Hit / Air Knockback / 縦Knockback / Air Clash / 正式Tradeは未実装。Air KickをAir Hit基盤完成とは扱わない

## Debug HUD font glyph atlas（`1b47fc6`・push済み・過去履歴）

- debug HUD用フォントglyph atlas更新。現在の基準HEADではない（現在のpush済みHEADは `6be8bb4`）

## Ground Clash recoil separation（`78c4e94`）

- 2026-07-30、`unity` / `origin/unity` へ push 済み
- Ground Clashを通常`ReceiveHit` / `HitStun`経路から分離し、専用状態`ClashRecoil`を追加
- ClashではDamage 0、HitStop、双方反動、攻撃終了、Idle復帰を維持
- Clash時は通常HitCountへ加算しない。JPunch同士／Ground Kick同士で`P2HitCount=0`をEditor確認
- Clash専用色を黄色系として追加。専用Sprite Sequence未設定時はIdleへfallback
- Scene差分なし。コード側の初期値で同動作を再確認
- 当時の検証フラグ `debugForceP2AttackWithP1ForClashTest` は通常OFF（後に `debugMirrorP1InputToP2` へ置換）

## Ground Kick + shared hit resolution groundwork（`bc80ddb`）

- 2026-07-30、`unity` / `origin/unity` へ push 済み
- P1 `K` で地上専用 Ground Kick。空中開始なし、着地予約なし、J Punch / Ground Kick 相互キャンセルなし、近い同時押下は J Punch 優先
- Ground Kick: Startup 8 / Active 3 / Recovery 4、Damage 14、HitStop 7、HitStun 14、Horizontal KB 0.24
- local Hit Box を脚へ合わせ `centerX=0.95 / centerY=0.55 / halfWidth=0.60 / halfHeight=0.25` に調整
- `Fighter_Kick_00`〜`_04` を P1/P2 の `kickSequence` へ接続（3CF/枚、Loop OFF、Hold Last Frame ON）
- `DebugAttackId` / `DebugAttackPhase` / `DebugPendingHit` / `DebugHitResolutionType` / `DebugClashTuning` を追加
- 同一 CombatFrame の両方向 Hit 候補を収集してから解決する共通経路を追加
- Ground ClashをEditor実測: JPunch同士／Ground Kick同士で Damage 0、HitStop、双方反動、攻撃終了、Idle復帰を確認
- このコミット時点では、Clash時に被弾用の赤色と`HitStun`表示を暫定流用していた（後続`78c4e94`で`ClashRecoil`へ分離済み）
- Editor確認: 通常 Hit、14 damage、1攻撃1Hit、HitStop、ノックバック、左右 Facing、空中開始禁止、押しっぱなし着地予約なし、Punch/Kick相互キャンセルなし
- 未確認: Development Build。Ground Clashの専用実測は完了

## Docs: 空中パンチ前提の記述整理

- 初期資料には空中パンチ案が含まれていたが、**現在は J Punch を地上専用とし、ジャンプ中のパンチを採用しない**方針へ統一した
- Air Hit / Air Knockback は**空中攻撃ではなく、空中被弾側の処理**として整理した
- Kick Gameplay 接続は、既存 Kick 素材を**地上攻撃**として接続する候補とした（空中キックは仕様未定）
- 将来の空中攻撃は仕様未定とし、空中パンチを前提にしない
- `docs/rules.md` から「空中パンチのガード可否」など、存在しない攻撃に関する未確定事項を削除
- `production_spritesheet_spec.md` の制作計画一覧から空中パンチ／空中キック必須扱いを除外

## PixelLab fighter action sprites 統合

- 正式 Stage 番号なし（段階15完了状態は維持）
- PixelLab Export（116×116 連番 PNG）を 1536×1024 正本シートへ統合。旧 `Fighter_SpriteSheet`（GUID `2bb8ae7896cf21b43bcd9cf17bf228d2`）は参照ゼロ確認後に削除。正本 GUID `dcb7851d129f2305be49fac973bf47b4` を維持して `Fighter_SpriteSheet.png` に一本化（`_New` 中間名解消）
- sub-sprite **31 枚**: Idle 8 / Walk 8 / Jump 8 / Punch 2 / Kick 5。PPU **39**（旧・新 Alpha bbox から算出。Transform Scale は未変更）
- 各アクションを個別 Rect 再スライス。方針: 体幹中心を Rect 水平中央、接地点または最下端を Rect 下端、共通 **Bottom Center** Pivot（Idle 102×116 / Walk 96×115 / Jump 120×105 / Punch 110×113 / Kick 108×117）
- FightDebugScene: P1/P2 とも新正本 GUID 参照。P2 Tint 維持。Idle / Walk / Jump / Punch Sequence 配線済み。JumpRise / JumpFall は `loop=false` + `holdLastFrame=true`
- **Kick**: 素材切り出しのみ。**Gameplay 未接続**（Scene に Sequence 参照なし）
- **Punch**: 素材 2 枚のみ（Recovery 専用コマなし）。Attack Sequence は暫定
- Gameplay ロジック・Transform Scale・当たり判定・Scripts は未変更
- Unity Editor 確認済み: Idle / Walk / Jump / Punch、Missing Sprite なし、動作に支障なし
- 今後候補: Kick Gameplay 接続、Punch 3 枚以上、必要なら攻撃素材再制作
- Docs: README / CHANGELOG / `unity_implementation_status.md` §1.1 / `sprite_art_status.md` / `rules.md` §15.5・§15.6 / `component_and_scene_guide.md` §4・§17 / `learning_and_readability.md` §4.1 / `production_spritesheet_spec.md`

## Visual Sequence + Sprite Sheet 移行

- 正式 Stage 番号なし（段階15完了状態は維持）
- `FighterSpriteSequence`（sprites / framesPerSprite / loop / holdLastFrame）。CombatFrame 基準でコマ解決
- `DebugFighterVisual` を単一 Sprite 切替から Sequence 再生へ拡張。WalkForward / WalkBackward は同一 Walk 2 コマ
- `FighterVisualState`: Idle / WalkForward / WalkBackward / Attack / JumpStart / JumpRise / JumpApex / JumpFall / Landing / HitStun / KO
- Session / Motor: JumpStart・Rise・Apex・Fall・Landing の見た目解決（軌道・攻撃判定は変更なし）
- `Fighter_SpriteSheet.png`（1024×1536、Multiple、個別 Rect）。P1/P2 同一シート、P2 Tint 維持。FightDebugScene 正本・Prefab なし
- 旧単体 PNG（Idle/Punch および Walk/Jump 中間）を参照ゼロ確認後に削除
- Walk 2 コマ切替は動作確認済み。**歩行見た目は暫定**（自然な歩行素材は未完了・今後の改善候補）
- Docs: README / CHANGELOG / `unity_implementation_status.md` / `sprite_art_status.md` / `rules.md` §15.5・§15.6 / `component_and_scene_guide.md` §4・§17 / `learning_and_readability.md`

## ジャンプ基盤・計測ログ・Training Reset 共通 release gate

- 正式 Stage 番号なし（段階15完了状態は維持）。Docs 反映時点ではジャンプ関連コードが未コミットの場合あり
- キャラクター別ジャンプ基盤: `FighterJumpType`（None / Neutral / Forward / Backward）、`FighterJumpSettings` / `JumpArcSettings`（種類ごと Min/Max 高さ・DurationFrames・HorizontalDistance）
- 入力保持補間: `JumpHoldFramesToMax` / `DirectionHoldFramesToMax`。逆入力は種類反転せず小さな空中制御のみ（`ReverseAirControlPerFrame`）
- 軌道: Rigidbody 物理は使わない。固定 CombatFrame で `LogicalX` / `LogicalY` を更新。HitStop 中はジャンプ軌道も停止。Stage 境界クランプ維持
- Jump Visual State: `JumpRise` / `JumpFall` / `Landing` を追加。専用 Sprite 未実装のため Idle Sprite 流用。Animator / Animation Clip は未実装
- 飛び越し: 高さ差が閾値以上なら空中 Push Box 解決をスキップ。着地付近で Push 復帰。飛び越し後は位置関係から Facing 更新（Editor 確認済み）
- Jump 計測ログ: 1ジャンプ最大3本（started / apex / landed）。`enableJumpDebugLog`（既定 true）。毎 Frame ログではない
- Training Reset: Reset 受理フレームは `SimulationClockDriver` が通常 Tick へ進めない（同一 Update return）
- Training Reset 後の共通 release gate: 全ゲーム操作（Left/Right/Up/Down/Attack）を一度すべて離すまで有効入力を Neutral 化。物理入力は消さない。R は解除条件に含めない
- Editor Play Mode 確認済み: 基本ジャンプ、短押し／長押し差、Forward／Backward、飛び越し・Facing、計測ログ、Reset 後の再ジャンプ／再移動なし、全 release 後の再受付、HitStop 中 Reset
- 未実装: Air Hit / Air Knockback（空中被弾）、Kick Gameplay、Animator、正式 Character Data SO、P2 操作。ジャンプ中の攻撃開始は現行でも不可（J Punch 地上専用）
- 未確認（当時）: Development Build Profiler、Pause 中 R の詳細など。当時未確認だったPause中Rは、後続のAirKick Assist予約破棄検証で基本動作をPlay確認済み。詳細総合回帰は未確認
- Scene / Prefab / Sprite / Animator / Font 変更なし
- Docs: README / `unity_implementation_status.md` §1.1・§1.2 / `rules.md` §15.4・§15.5・§15.7 / `component_and_scene_guide.md` §17.7〜§17.9

## 最小 Visual State（Idle / WalkForward / WalkBackward / Attack）

- 正式 Stage 番号なし（段階15完了状態は維持）。練習モードの見た目基盤
- 新規 `FighterVisualState`。`SimulationSession` が CurrentInput × Facing で State を決定し、`DebugFighterVisual.Apply` が描画
- Walk は Idle Sprite 流用。Animator / Animation Clip / Walk 専用 Sprite は未実装
- 優先: HitStun・KO → Attack → Walk → Idle。入力意図基準（壁際でも方向入力中は Walk）。Left+Right / 無入力は Idle
- Editor 確認済み: Idle / WalkF / WalkB（右向き）/ 同時入力 Idle / 壁際 Walk / 移動中 Attack 優先 / Reset 後 Idle
- 未確認: 左向き Walk（現仕様ですり抜け不可のため実測困難）、P1 被弾側、Development Build
- Scene / Prefab / Sprite / Animator / Font 変更なし
- Docs: README / `unity_implementation_status.md` §1.1 / `rules.md` §15.5 / `component_and_scene_guide.md` §17.8
- コードと Docs を同一コミットにまとめる予定（本作業では Cursor は commit しない）

## Training Reset の位置・向き復帰

- 正式 Stage 番号なし（段階15完了状態は維持）。練習モードの Training Reset 拡張
- `DebugFighterMotor`: Scene 開始時の `initialLogicalX` を保存。`ResetLogicalXToInitial()` → `SetLogicalX` で論理座標を正本として復帰（Transform 直書きしない。Y/Z・移動範囲・速度は変更なし）
- `SimulationSession.ResetTestActionForP1`: 戦闘状態 Reset 後に P1/P2 論理 X を戻し、`ApplyInitialFacingTowardOpponents()` で Facing を再計算（Slot 固定の決め打ちなし）
- `Participant.ResetCombatDebugState` は戦闘状態のみ。位置・Facing は Session 経路
- Editor Play Mode 確認済み: 壁際 P1X=6.00 / P2X=7.00 → R → 0.00 / 3.00・Facing 初期どおり。KO 後も Alive/100・HitCount=0
- 未確認（当時）: Pause 中 R、Reset 直後の再移動／再攻撃、Development Build。当時未確認だったPause中Rは、後続のAirKick Assist予約破棄検証で基本動作をPlay確認済み。詳細総合回帰は未確認
- Scene / Prefab / Sprite / Animator / Font 変更なし
- Docs: README / `unity_implementation_status.md` §1.1・§2.3 / `rules.md` §15.4 / `component_and_scene_guide.md` §17.7
- コードと Docs を同一コミットにまとめる予定（本作業では Cursor は commit しない）

## 練習モードと対戦モードの責務整理（Docs）

- Docs 更新のみ。コード / Scene / Prefab / Font Asset / Animator / Animation Clip / Sprite の追加・変更なし
- `FightDebugScene` を正式な**練習・検証モード**として明記（対戦モードではない）
- 共通戦闘コアは「KO 成立」まで。KO 後の進行はモード側。対戦進行を FightDebugScene へ混在させない
- Training Reset（R）方針を整理。HP/KO 等の初期化は確認済み。**位置・向きの初期復帰は未実装**（実装完了扱いにしない）
- Visual State（戦闘状態→見た目）、前進／後退判定、Sprite Single/Multiple、Animator 関係を方針として記載
- Animator・歩行・キック・ジャンプは**未実装**。正式な次 Stage 番号は作らない（段階15完了状態は維持）
- 更新: README / `docs/unity_implementation_status.md` §1.1・§5 / `docs/rules.md` §15 / `docs/component_and_scene_guide.md` §2・§17

## GC-2（補助改善・Status HUD の StringBuilder 再利用）

- 正式 Stage 番号ではない（GC 学習・計測の便宜区分）。機能 Stage（Round/Guard 等）とは別枠
- `DebugHudView`: Status HUD を再利用 `StringBuilder(2048)` + `Clear` + `Append` で構築。`hudText.SetText(statusTextBuilder)`
- 毎描画 Frame 更新・表示内容・Help（GC-1）・戦闘処理は維持。小数書式用 `ToString` は一部残存
- Editor Play Mode / 通常待機: `DebugHudView.Update` 約17.2 KB → 約3.2 KB（約14.0 KB・約81.4%削減）。初期状態（約17.8 KB）比 約82.0%
- Miss/Hit/KO/Push/Reset まで回帰確認。Development Build・フレーム全体 KB / alloc 回数 / GC.Collect・内訳は未計測／未記録
- `docs/component_and_scene_guide.md` §16.14 / `docs/unity_implementation_status.md` §5.1 / README を更新

## GC-1（補助改善・DebugHudView 固定 Help の毎 Frame 再構築停止）

- 正式 Stage 番号ではない（GC 学習・計測の便宜区分）。機能 Stage（Round/Guard 等）とは別枠
- `DebugHudView`: 固定 Help 本文を Awake → EnsureSplitHudLayout で1回だけ設定。Update から毎 Frame 再構築・再代入を削除
- Status HUD は GC-1 では未変更（毎 Frame 更新を維持）。Help 文面・レイアウト・表示は維持
- Editor Play Mode / 通常待機の実測: `DebugHudView.Update` 約17.8 KB → 約17.2 KB（約0.6 KB・約3.4%削減）
- 変更後のフレーム全体 KB・alloc 回数・GC.Collect、Development Build は未記録／未計測
- 続く GC-2（Status HUD StringBuilder）でさらに削減（上記 GC-2 項）
- `docs/component_and_scene_guide.md` §16.13 / `docs/unity_implementation_status.md` §5.1 / README を更新

## v3.15（Unity段階15完了・J Punch攻撃データ化）

- 段階15完了を明記。`DebugAttackData.JPunch`（static readonly）が J Punch 設定正本。SO/Inspector 化は見送り
- S/A/R=3/3/6・Total=12・Damage=10・HitStop=6・HitStun=12・KB=0.180/減速0.015・local Hit Box をデータへ集約
- Session は進行と Hit 適用のみ。Participant が local→world / Facing。PunchHitResolver は重なりのみ。Frame境界・1攻撃1Hit・KO処理順は維持
- HUD: `JPunch Data`（データから生成）。Truncate 非表示を P2 KB 直後配置＋HelpBlock 高さ調整で修正（Scene 変更なし）
- KO表示回帰: 14B時点の KO>HitStun を履歴として残し、現在仕様は HitStun赤>KO暗色>通常Tint（ApplyDisplayColor のみ）
- 検証: Miss/1Hit/Frame境界/KO/Reset/Stage13・14回帰、Error 0・既存 CS0618 Warning 1
- 工程表の段階15は完了。次は既存計画の後続候補（Round/勝敗、Guard、複数攻撃、SO化の要否など・順不同）
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.14（Unity段階14B完了・Participant共通KO状態・KO遷移）

- 段階14B完了を明記。KO正本は Participant.isKnockedOut。API: TryEnterKnockout / ClearKnockoutForReset / BuildLifeLabel
- 処理順 ReceiveHit→ApplyDamage→TryEnterKnockout→MarkHit→HitStop。最後の一撃の HitStop/Stun/KB は維持
- KO中は入力移動・新規攻撃禁止、KO済み防御者への追加Hit拒否（DefenderKO）。視覚優先 KO>HitStun>通常（段階15で HitStun>KO へ回帰修正）
- HUDに P1/P2 Life。検証: 10HitでKO、追加攻撃はMiss、ResetでAlive/100、Stage13/14A回帰正常
- 未直接検証: P2 Dummyのため KO側の実操作入力禁止はコード経路のみ
- 工程表の段階14（HP/Damage/KO）は14A+14Bで完了。次工程は既存計画の段階15（攻撃データ化）
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.13（Unity段階14A完了・Participant共通HP・Damage基盤）

- 段階14A完了を明記。HP正本は Participant（maxHitPoints=100、currentは実行時）。HitStateはHP非所有
- 有効Hit時のみ ApplyDamage（J Punch暫定10）。接続順 ReceiveHit→ApplyDamage→MarkHit→HitStop。1攻撃1Damage
- 0未満Clamp。0HPでも14AではKOせず戦闘継続（暫定）。ResetでHP全回復。HUDにP1/P2 HP
- 検証: 初回90、2回目80、10Hitで0、11Hit以降actual=0、Resetで100、Stage13回帰正常、Error/Warning 0
- 段階14全体は未完了。次工程は既存計画の段階14B（KO状態・KO遷移）
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.12（Unity段階13B-1完了・壁際Push補正配分）

- 段階13B-1完了を明記。`Motor.TryMoveLogicalXBy` と PushResolver の壁際再配分を Docs に反映
- 通常は左右半分ずつ。端で実移動が足りない分を反対側へ再配分（再配分順は右→左）。同XはAを左扱い
- PushはLogicalXのみ変更し KnockbackVelocityX は触らない。FacingはPush後。minX/maxX=±7維持
- 検証: 中央 Dist=1.00・wallログなし、右端/左端で再配分ログと Dist=1.00、13A回帰（HitStop/KB/Stun）正常、Error/Warning 0
- 段階13B（工程表の壁際Push配分）は13B-1で充足。次工程は既存計画の段階14（HP、Damage、KO）
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.11（Unity段階13A完了・Participant共通ノックバック基盤）

- 段階13A完了を明記。`HitState.knockbackVelocityX` と Participant 暫定初速0.18／減速0.015を Docs に反映
- Hit成立時に LogicalX 比較で方向決定（Facingは正本にしない。同位置のみ Facing fallback）
- HitStop中は位置・速度・Stunすべて維持。HitStop後の Combat から `X+=V`・毎CF 0.015減速（deltaTime不使用）
- 処理順: 入力移動→ノックバック→Push→Facing→Action→Hit→Visual→HitStun。速度正本はHitState、位置はMotor
- HitStun終了／Resetで残速度0。既存 minX/maxX は有効のまま。ステージ端・壁際Pushは未変更
- HUD: P2 KB Vx/Act。左上状態／左下操作の分離維持
- 段階13全体は未完了。次工程は既存計画の段階13B（ステージ端・壁際 Push 配分）
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.10（Unity段階12A完了・被Hit/HitStun/被Hit表示）

- 段階12A完了を明記。`DebugFighterHitState`（Participant 共通）と HitStun 12CF・被Hit色を Docs に反映
- HitStop（共有6F）と HitStun（機体ごと）の違い、HitStop中は Stun 非減算、Combat末尾で12→0を明記
- HitStun中は移動・新規攻撃不可、Push/Facing/Box は維持。TotalHitCount 正本は HitState
- Reset（R）で両体 Attack/HitState/色と共有 HitStop を初期化。位置/Facing は維持
- HUD: P2 Stun/State。レイアウトは左上=状態・左下=操作（DebugHudView ランタイム分離、16:9下端切れ解消）
- 段階12（工程表の被Hit/HitStun/表示）は12Aで充足。次工程は既存計画の段階13（ノックバック／押し戻し／ステージ端）
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.9（Unity段階11B完了・Hit×Hurt 重なり判定）

- 段階11B完了を明記。距離判定から Hit Box × Hurt Box 重なり判定への置換を Docs に反映
- `DebugBox2D.Overlaps`（境界接触も Hit）。`attackRange` / 距離・Facing距離 fallback は削除済み
- 可視化と同じ `EvaluateWorldHitBox` / `EvaluateWorldHurtBox` が実判定の正本。Active は AF 4〜6
- 検証済み: 遠距離 Miss、近距離 Box Hit、1攻撃1Hit、6F HitStop、Pause/Step、Push 非変更、Error/Warning 0
- 段階11（単一 Push/Hurt/Hit 可視化＋判定基盤）を完了扱い。次工程は既存計画の段階12（被 Hit / HitStun / 被 Hit 表示）
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.8（Unity段階11A完了・Push/Hurt/Hit Box 可視化基盤）

- 段階11A完了を明記。`DebugBox2D` / Participant の Box 定義 / `DebugFighterBoxView`（LineRenderer）を Docs に反映
- Push/Hurt 常時表示、J Punch Hit は Active（AF 4〜6）のみ。Facing Left は Hit Local X のみ反転（コード確認済み）
- 論理接地 Y: `boxOriginLocalY = -(sprite.pivot.y / PPU)` を Awake で一度導出。WorldCenterY = LogicalGroundY + LocalCenterY
- 検証済み: 足元〜頭付近の枠、追従、Active 連動、Pause/Step、距離 Hit / Push Resolver 非変更、Error/Warning 0
- 未検証として Facing Left 攻撃の実操作表示を残す（11A 未完了にはしない。2P入力/テスト経路追加時の確認項目）
- Box は可視化専用。次工程を段階11B（Hit×Hurt 重なり判定→距離判定置換）へ更新
- README / `docs/unity_implementation_status.md` / `docs/rules.md` を更新

## v3.7（Unity段階10B-3完了・Push Box／すり抜け防止）

- 段階10B-3完了を明記。Participant 共通の横方向 Push Box／すり抜け防止を Docs に反映
- 責務: `Participant.pushBoxHalfWidth`（既定 0.5）、`Motor.SetLogicalX`、`DebugFighterPushResolver`、Session が移動→Push→Facing 順を管理
- 補正は中心距離不足分の左右等分分離。Motor min/max で片側が止まった分は相手へ転送（現段階の暫定仕様）
- 検証済み: 非重なり・非すり抜け、接触時 Push Dist=1.00、押し分け、Facing 安定、接触中 J/Hit/HitStop、Pause/Step、Error/Warning 0
- 縦方向・ノックバック・画面端専用処理・飛び越え反転は未実装。壁際配分はステージ境界実装時に再検討
- 段階10の Push Box 残作業を完了へ移し、次工程を段階11（Box 可視化または判定基盤）へ更新
- README / `docs/unity_implementation_status.md` / `docs/rules.md` §2.3.1・§14 を更新

## v3.6（Unity段階10B-2完了・AttackState正本化）

- 段階10A/10B-2完了を明記。最新実装コミット `582619b`（Move fighter attack and hit state to participants）
- 攻撃状態の正本を各 `DebugFighterParticipant.AttackState` に移したことを Docs に反映
- `SimulationTimeState` は共有時間・Pause・HitStop 担当と明記（旧攻撃フィールド削除済み）
- Hit を `TryResolveJPunchHit(attacker, defender)` 共通化。P2 は共通経路を持つが Neutral のため通常攻撃しない
- `DebugDummyTarget` 廃止を Docs に反映（コード・Scene とも削除済み）
- Facing 分離・相手向き合いは実装済み。段階10の残りは Push Box・すり抜け防止
- 相打ちは両方向判定の土台のみ。P2入力/AIによる実動作確認は未実施と明記
- README / `docs/unity_implementation_status.md` / `docs/rules.md` §2.3.1 を更新

## v3.5（Unity段階1〜9到達・Facing方針・工程見直し）

- Unity 実装の到達点・暫定/正式・次工程を `docs/unity_implementation_status.md` に集約
- 段階1〜9完了を明記。最新実装コミット `0b84a81`（Add minimal punch hit detection）
- 段階9の Hit は距離＋向きの**暫定**であり、正式 Box 判定ではないことを明記
- Facing: 入力方向で向きを変える現状は暫定。正式は相手向き合い＋移動入力分離（未実装）
- Push / Hurt / Hit Box 可視化・重なり判定は未実装と整理
- 推奨工程を段階10（Facing分離・Push Box）→11（Box判定置換）→12以降（被Hit/KB/HP/データ化）に見直し
- README を実装開始済みの要約へ更新（旧「実装未開始」記述を修正）
- `docs/rules.md` §2.3 にデバッグ暫定 Facing との関係を追記
- `docs/sprite_art_status.md` にデバッグ用透過試験PNGの存在を追記（本番清書ではない）

## v3.4（スプライト段階制作方針）

- 専任デザイナー不在・ChatGPT支援前提の制作工程を `docs/production_spritesheet_spec.md` に正本として追加
- 工程: 骨格 → シルエット → 仮ドット絵 → 前後比較 → Unity連続再生 → 修正 → 清書
- 骨格基準点、身体固定項目、ChatGPT/人間の担当分離、Idle+StandPunch試験、代替方針を明記
- 128×128は素材セルサイズであり画面等倍表示ではないこと、表示パラメータは未確定と明記
- `docs/sprite_art_status.md` で現状が未完成であること・ステージ区分を更新
- README に制作方針の短い要約を追加
- `docs/learning_and_readability.md` にアートも段階工程を優先する旨を追記

## v3.3（曖昧点の追加確定）

- Down+Back をその場しゃがみ（水平移動なし）に確定。「しゃがみ後退」表現を削除
- JustGuard: Down保持後の Back 新規エッジは対象。Back保持後の Down のみでは対象外
- Trade を一次分類から外し、双方向とも Hit のときの複合集約と明記。hit_resolution から Trade 行を削除
- SimulationTick / CombatFrame / ActionFrame を区別。HitStop中は入力tickのみ進行
- ActionFrame 入場時0・末尾で duration 減算（オフバイワン対策）を明記
- 入力解釈用 Facing（前CF終了時）と最終 Facing（Push後）を分離
- BlockStun（CombatState）と GuardPosture / DisplayAnimation の責務分離。state_transitions から StandGuard 遷移を除去
- Clash 壁際の未消化反動は初期版で破棄（転送しない）。通常 Pushback 転送とは別処理
- README: `1f294cc` を「Unity追加時の基準コミット」と表現。未コミット資料更新を明記
- debug_ui_fields: SimulationTick を初期必須に。CombatFrame / GuardPosture 等を追加

## v3.2（仕様レビュー反映・ルール確定）

- ガード接触時判定、JustGuard（攻撃弾き型）、Clash/Trade、1GF処理順、着地分離などを確定
- 学習・可読性方針ドキュメントを追加
- CSVとコードの責務分離を明記

## v3.1（資料・CSV整合）

- スプライト配置正本の一本化、Idle+StandPunch サンプル整理
- README を Game/ 作成済みに更新（基準コミット `1f294cc`）

## v3

- 参考画像と本番素材の区別を明確化

## v2

- Unity選定理由、デバッグ画面資料を追加

## v1

- 初回実装テンプレート
