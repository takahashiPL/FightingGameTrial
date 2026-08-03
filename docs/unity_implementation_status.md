# Unity 実装状況・次工程（段階1〜15到達後）

最終更新: 2026-08-03
対象ブランチ: `unity`
内容反映済み基準コミット（push済みHEAD）: **`988f36f`**（Document air mutual-hit validation）
直前のコード到達: **`03bfd72`**（Add P2 air kick mutual-hit debug assist）
push済み到達点の例: `03bfd72` / `988f36f`（AirKick Assist・Air双方未適用）、`96f8148` / `6be8bb4`（地上攻撃変換・Delay・異技Clash）、`5bd3627` / `6bc5f03`（Clash整理＋基本P2鏡写し）
過去履歴の例（現在の基準ではない）: `1b47fc6`（HUD font atlas）、`69c9385`（Document clash recoil state separation）
段階14全体（14A+14B）・段階15（攻撃データ化）: **完了・push 済み**
GC-1 / GC-2（補助改善・正式 Stage ではない）: **完了・push 済み**
Training Reset 位置・向き復帰: **実装・Editor 確認済み**（正式 Stage 番号なし）
Visual Sequence + Sprite Sheet 移行: **実装・Editor 確認済み・push済み**（正式 Stage 番号なし）
PixelLab再構築版Sprite Sheet正式採用: **実装・Editor確認済み・push済み**（`5bcc3fa` / Docs `d98a99d`）
ジャンプ基盤・Jump Visual・計測ログ: **実装・Editor 確認済み**（正式 Stage 番号なし）
Training Reset 共通 release gate: **実装・Editor 確認済み**（正式 Stage 番号なし）
Ground Kick + shared hit resolution groundwork: **実装・Editor確認済み・push済み**（正式 Stage 番号なし）
Ground Clash recoil separation: **実装・Editor確認済み・push済み**（`ClashRecoil`専用状態・黄色系表示・通常HitCount非加算。正式 Stage 番号なし）
Air Kick最小検証版・攻撃フレーム調整・専用Sequence: **実装済み・Play確認済み・push済み・暫定**（`f19cc22` 系〜 `5bcc3fa`）
Clash判定整理＋基本P2鏡写し（`debugMirrorP1InputToP2`）: **実装・Docs反映済み・push済み**（`5bd3627` / `6bc5f03`）
P2鏡写し攻撃変換・CombatFrame遅延・異技Clash実測: **実装・Docs反映済み・push済み**（`96f8148` / `6be8bb4`）
P2 AirKick検証アシスト・Air双方未適用実測: **実装・Docs反映済み・push済み**（`03bfd72` / `988f36f`）
Debug HUD font glyph atlas: **push済み・過去履歴**（`1b47fc6`。現在の基準ではない）
**今回の未コミット範囲のみ**: P2 AirKick Assist予約の安全確認Play実測（Mirror OFF／Assist OFF／Training Reset／接地時破棄）および上記のDocs反映
正式な次工程番号: **未定義**（新 Stage 番号は作らない）

このファイルは、Unity 側の**実装済み / 暫定 / 未実装 / 次回候補 / 正式方針**を混同せずに追うための正本です。
ゲーム仕様そのものの正本は引き続き `docs/rules.md` です。
「GC-1」「GC-2」等は正式 Stage 番号ではなく、学習・計測用の補助区分である。

---

## 1. 区分の読み方

| 区分 | 意味 |
|---|---|
| **実装済み・確認済み** | FightDebugScene で動作確認済み |
| **暫定実装** | 動くが、正式仕様へ置き換える前提 |
| **正式方針（未実装）** | 今後そうする、と決めた設計。コード未反映 |
| **コード上対応** | コード経路はあるが、今回の実測対象外／未確認 |
| **未確認** | 実装はあるが Editor / Dev Build で未検証 |
| **未実装** | まだ作っていない |
| **次回候補** | 工程上の次ステップ（番号未割当含む） |

---

## 1.1 モード位置づけ（方針確定・実装は段階15到達時点）

`FightDebugScene` は**対戦モードではない**。**正式な練習・検証モード**として扱う。

| 項目 | 状態 |
|---|---|
| 詳細 HUD・戦闘ログ・判定／KO 確認・R Reset | **実装済み・維持** |
| KO 後の WIN/LOSE・ラウンド終了への進行 | **しない**（方針。現コードも対戦進行なし） |
| KO 状態の観察 | **できる**（確認済み） |
| 対戦モード（Round / 勝敗 / タイマー等） | **未実装**。FightDebugScene へ混在させない |

### 責務表（方針）

| 層 | 担当 | 備考 |
|---|---|---|
| **共通戦闘コア** | 入力サンプリング、左右移動、ジャンプ、向き、Push/Hurt/Hit Box、S/A/R、Damage、HitStop、HitStun、Knockback、HP、KO 判定、KO 後の追加被弾拒否、攻撃データ、基本戦闘状態、戦闘状態→見た目同期 | **「KO が成立した」まで**。練習／対戦で共通化する方針 |
| **練習モード（現 FightDebugScene）** | 勝敗なし、ラウンドなし、時間制限なし、WIN/LOSE なし、Training Reset（R）、詳細 Debug HUD、判定・座標・攻撃・HitStop 等の観察 | KO 後は観察継続。将来候補: ダミー回復、自動回復、ガード設定、行動記録、判定表示、フレーム表示など |
| **対戦モード（将来・未実装）** | ラウンド開始・終了、勝敗、WIN/LOSE、ラウンド数、タイマー、READY/FIGHT、次ラウンド、Match 終了、リザルト、対戦用 HUD、開始前・終了後の入力制限 | 別 Scene / Controller / HUD を想定。現 Scene に混在させない |

共通側は KO 成立という**戦闘結果**まで。KO 後に何をするかは**モード側**が決める。

### Training Reset（実装済み・Editor 確認済み）

練習モードの正式な **Training Reset**（R）。論理位置（X/Y）・ジャンプ状態・Facing・戦闘状態を Scene 開始時相当へ戻す（正式 Stage 番号は付けない）。

**R で初期化する項目**

- P1 / P2 論理位置 X / Y、Grounded、ジャンプ速度・JumpType・Jump frames・Landing、Jump 計測イベント
- Facing（位置関係から再計算）
- HP、KO、HitCount、HitStun、Knockback
- 攻撃状態、HitStop、AttackResult
- Sampled 有効入力のクリア＋共通 release gate 開始

**位置の戻し方**: Transform を Session から直接書き換えない。Motor が保持する **論理座標**を Scene 開始時の初期値へ戻し、既存同期経路でクランプと Transform 反映する。

| 項目 | 状態 |
|---|---|
| HP / KO / HitCount / HitStun / Knockback / 攻撃 / HitStop / AttackResult | **実装済み・Editor 確認済み** |
| 論理位置 X/Y・Facing・ジャンプ状態の初期復帰 | **実装済み・Editor 確認済み** |
| Reset 受理フレームの同一 Update 打ち切り（通常 Tick へ進まない） | **実装済み・Editor 確認済み** |
| 共通 release gate（全ゲーム操作 release まで有効入力 Neutral） | **実装済み・Editor 確認済み** |
| Pause 中 R（受理・主要状態初期化・Assist予約破棄・解除後input再有効化） | **Play確認済み**（AirKick Assist予約破棄検証） |
| Pause 中 R の詳細総合回帰、Development Build | **未確認** |
| 複数初期配置プリセット、P2 が左側の別 Scene | **未確認／対象外** |

**実装構造（要約）**

1. `SimulationClockDriver.Update`: R 受理 → `ResetTestActionForP1()` → **そのフレームは return**（Step / AccumulatedTime の通常 Tick へ進まない）
2. `DebugFighterMotor.ResetLogicalPositionAndJumpToInitial()`: X/Y・ジャンプランタイム・計測 pending を初期化
3. `SimulationSession.ResetTestActionForP1()`: 戦闘状態 Reset → 両体位置/Jump Reset → Facing → `waitForAllGameplayInputReleaseAfterReset=true` → 有効入力クリア → Visual Idle
4. 抑制中: 物理 Held は `DebugGameplayInput` に残す。`CurrentInput` へ渡す有効入力だけ全 false。解除条件は Left/Right/Up/Down/Attack がすべて false（**R は含めない**）
5. 解除 tick: 有効入力は Neutral のまま。エッジ用 previous を false 再同期。次の新規押下から受付。ログ `[FightDebug] Gameplay input re-enabled after Training Reset`

**Editor Play Mode 実測（要約）**

| 確認 | 結果 |
|---|---|
| ジャンプ中 R → 初期 X/Y・Grounded・Idle・JumpType=None | **確認済み** |
| Reset 直後に Jump started / Attack started / 古い apex・landed | **出ない** |
| 抑制中の定期ログ L/R/U/D/Attack=0、Visual=Idle、P1X=0.00 | **確認済み** |
| 一部ボタンだけ離しても解除しない／全離しで解除 | **確認済み** |
| 解除後の新規入力で移動・Jump・Attack | **確認済み** |
| HitStop 中 Reset、連続 Reset で古い Jump event なし | **確認済み** |
| Pause 中 R → Training resetログ・主要状態初期化・Assist予約未発火・解除後input再有効化 | **Play確認済み** |

詳細は §1.2、教材 §17.7・§17.9、`docs/rules.md` §15.4・§15.7。

### Visual Sequence + Sprite Sheet（実装済み・Editor 確認済み）

正式 Stage 番号は付けない。Animator / Animation Clip は未使用。`FightDebugScene` が正本（Prefab 化なし）。Gameplay（入力・Jump 軌道・攻撃判定）は変更していない。

**構造**

| 要素 | 内容 |
|---|---|
| `FighterSpriteSequence` | `sprites[]` / `framesPerSprite` / `loop` / `holdLastFrame`。CombatFrame 経過でコマ解決 |
| `DebugFighterVisual` | State ごと Sequence。`Apply(state, advanceElapsed)`。WalkF/B は同一 `walkSequence` |
| `FighterVisualState` | Idle / WalkForward / WalkBackward / Attack / JumpStart / JumpRise / JumpApex / JumpFall / Landing / HitStun / KO / Kick / ClashRecoil |
| `SimulationSession` | Visual 優先解決。JumpStart / Apex は見た目用窓（軌道計算には使わない） |
| `DebugFighterMotor` | `HasPassedJumpApex` / `FramesSinceJumpApex`（表示専用） |

**優先順位（Sprite）**: KO → ClashRecoil → HitStun → Attack / Kick → JumpStart → JumpRise → JumpApex → JumpFall → Landing → WalkForward / WalkBackward → Idle

HitStun / ClashRecoil / KO は専用 State。専用 Sequence 未設定時は Idle Sequence へ fallback。色は`ApplyDisplayColor`で分離し、ClashRecoilは黄色系、HitStunは赤、KOは暗色、通常時はTint。

**素材（FightDebug 正本）**: `Assets/Art/Characters/Fighter_SpriteSheet.png`（Multiple、GUID `42345be00e0994144ba94bc5f1362757`）。PixelLab再構築版の**41実画素Trim Rect**。PPU **39** / Full Rect / Point / Compression None / Physics Shape Off / Mip Map Off。全sub-sprite **Center**。P1/P2同一シート参照、P2はTint区別。

| グループ | 枚数 | Scene 接続 |
|---|---|---|
| Idle | 8（`Fighter_Idle_00` … `_07`） | **接続済み・確認 OK** |
| Walk | 8（`Fighter_Walk_00` … `_07`） | **接続済み・確認 OK**（WalkF / WalkB 共有） |
| Jump | 8（`Fighter_Jump_00` … `_07`） | **接続済み・確認 OK** |
| Punch | 2（`Fighter_Punch_00` / `_01`） | **接続済み・確認 OK**（Recovery 専用コマなし・暫定） |
| Kick | 5（`Fighter_Kick_00` … `_04`） | **Ground Kickへ接続済み・確認 OK** |

| State | Sequence 割当 |
|---|---|
| Idle | `Fighter_Idle_00` … `_07`（8 コマ） |
| WalkForward / WalkBackward | `Fighter_Walk_00` … `_07`（8 コマ） |
| JumpStart | `Fighter_Jump_00` |
| JumpRise | `Fighter_Jump_01` / `_02`（`loop=false`, `holdLastFrame=true`） |
| JumpApex | `Fighter_Jump_03` |
| JumpFall | `Fighter_Jump_04` / `_05`（`loop=false`, `holdLastFrame=true`） |
| Landing | `Fighter_Jump_06` / `_07` |
| Attack | `Fighter_Punch_00` / `_01` |

共通 Rect サイズ: Idle 102×116 / Walk 96×115 / Jump 120×105 / Punch 110×113 / Kick 108×117。

Characters フォルダは正本 PNG + `.meta` の 1 組のみ。

**評価**

| 項目 | 状態 |
|---|---|
| PixelLab 正本シート統合 | **完了** |
| 41 sub-sprite切り出し・正式採用 | **完了** |
| Idle / Walk / Jump / Punch Sequence | **Editor 確認済み** |
| Kick 素材 | **切り出し済み** |
| Ground Kick Gameplay | **接続済み・Editor確認済み** |
| P1/P2・P2 Tint | **確認済み** |
| Missing Sprite | **なし（Editor 確認済み）** |
| Gameplay ロジック変更 | **なし** |

**次回改善候補**: ClashRecoil専用Sprite／演出、Punch 3 枚以上、必要なら攻撃素材再制作。Animator 導入は別候補。

詳細は教材 §17.8・§17.9、`docs/rules.md` §15.5・§15.6、`docs/sprite_art_status.md`。

---

## 1.2 ジャンプ基盤（実装済み・Editor 確認済み）

正式 Stage 番号は付けない。Docs 反映時点ではコード未コミットの場合あり。正式仕様のジャンプ節（`rules.md` §2）は長期設計を含み、Unity デバッグ実装の詳細は本節と §15.7 を正とする。

### 実装済み

| 項目 | 内容 |
|---|---|
| 種類 | Neutral / Forward / Backward（`FighterJumpType`） |
| 設定 | `FighterJumpSettings` + `JumpArcSettings`（種類ごと Min/Max Height・DurationFrames・HorizontalDistance） |
| 共通設定 | JumpHoldFramesToMax、DirectionHoldFramesToMax、ReverseAirControlPerFrame、LandingFrames、PushBoxVerticalSeparationThreshold |
| 軌道 | CombatFrame 固定更新の LogicalX/Y。Rigidbody 物理は使わない |
| HitStop | ジャンプ軌道・LogicalY も停止。Jump Visual 保持 |
| Push | 地上維持。空中で高さ差≥閾値なら解決スキップ（飛び越し可）。着地付近で復帰 |
| Facing | 飛び越し後は位置関係から更新（Slot 固定ではない） |
| Visual | JumpStart / JumpRise / JumpApex / JumpFall / Landing（シート sub-sprite。§1.1） |
| 計測ログ | started / apex / landed（最大3本/ジャンプ）。`enableJumpDebugLog` |
| Reset | Y/Jump 状態復帰 + 同一フレーム Tick 打ち切り + 共通 release gate |

### 責務

| 担当 | 内容 |
|---|---|
| `SimulationSession` | 入力読取、種類判定、Jump 開始要求、Push 有効判定、Facing 順、Visual 決定、Jump debug log、release gate |
| `DebugFighterMotor` | LogicalX/Y、速度、Grounded、JumpType、frame 進行、上昇/落下/着地、計測、Transform 同期、クランプ |
| `DebugFighterParticipant` | HP / HitStun / Knockback / KO / Attack 等。Jump 計算はしない |
| `DebugFighterVisual` | State → Sprite/Label。Jump 計算・入力判定はしない |
| `SimulationInputState` / Input | 物理・論理 Held。Jump 種類決定はしない |

### 種類判定（開始時の Facing × 入力）

FacingRight=true: Up のみ Neutral / Up+Right Forward / Up+Left Backward
FacingRight=false: Up のみ Neutral / Up+Left Forward / Up+Right Backward
Left+Right 同時: Neutral
開始後: `CurrentJumpType` は着地まで保持（空中で種類を完全反転しない）

### 初期パラメータ（コード設定値）

| 種類 | Height | DurationFrames | HorizontalDistance |
|---|---|---|---|
| Neutral | 1.8–2.2 | 28–34 | 0–0 |
| Forward | 1.7–2.1 | 28–34 | 2.3–2.8 |
| Backward | 1.6–2.0 | 26–32 | 1.7–2.1 |

共通: HoldMax=8、DirHoldMax=10、ReverseAirControl=0.02、LandingFrames=2、Push垂直閾値=0.85

**注意**: 実測 maxHeight 等は計算結果として設定 Min/Max を少し上回る場合がある（例: Neutral 長押し maxHeight=2.66）。設定値と実測を混同しない。

### Editor 確認済み（実測要約）

| 項目 | 結果 |
|---|---|
| Compile | Error なし。既知 TMP CS0618 以外の新規 Warning なし |
| 基本 Jump | Rise→Fall→Landing→Idle。空中再ジャンプなし。着地後再ジャンプ可 |
| Neutral 短め | jumpHeld=6、maxHeight=2.52、totalFrames=36、horizontalDistance=0.00 |
| Neutral 長押し | jumpHeld=8、maxHeight=2.66、totalFrames=38、horizontalDistance=0.00 |
| Neutral さらに短め | jumpHeld=3、maxHeight=2.13、totalFrames=32、horizontalDistance=0.00 |
| Forward 代表 | held=8/dir=10、maxHeight=2.50、frames=38、distance≈3.11 |
| Backward 代表 | held=8/dir=10、maxHeight=2.40、frames=36、distance=2.35 |
| 空中制御 | Neutral 中の水平移動あり（例 0.94 / 1.18 / 1.34）。現状値を採用・調整可能 |
| 飛び越し・Facing | P1 が P2 を越え Facing 反転。反対側から再飛び越しで復帰。左向き Forward/Backward・Walk・J Punch 成立 |
| ジャンプ中の J Punch | **開始不可**（J Punch は地上専用。HUD Attack=1 でも Jump 中は開始しない） |
| Landing | LandingFrames=2、Landing Visual 確認。Landing 中左右移動可・再ジャンプ不可 |

### コード上対応（実測と分離）

- HitStun 中: Jump 開始不可、プレイヤー空中制御不可。空中なら入力なしで軌道継続
- KO 中: Jump 開始不可、入力空中制御不可。KO Visual 優先

### 未実装 / 未確認 / 将来候補

| 区分 | 内容 |
|---|---|
| **未実装** | Animator / Animation Clip、**Air Hit / Air Knockback（空中被弾）**、正式 Character Data SO、高度な着地硬直・入力予約 |
| **未確認** | Development Build Profiler、Pause 中 R の詳細総合回帰（基本動作は Assist予約破棄検証でPlay確認済み） |
| **将来候補（仕様未定含む）** | 将来の空中攻撃（種類未定・空中パンチは対象外）、空中キック（未定）、Animator、Guard、Jump 値調整、SO 化 |

### GC（コード確認）

毎 Frame new / LINQ なし。Jump 設定は保持再利用。計測ログはイベント時のみ。release gate 判定で割り当てなし。**Development Build Profiler は未確認**。

---
## 2. 段階1〜15の到達点

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
| **13A** | Participant 共通ノックバック基盤 | **実装済み・確認済み** |
| **13B-1** | ステージ端を考慮した Participant 共通 Push 補正配分 | **実装済み・確認済み** |
| **14A** | Participant 共通 HP・Damage 基盤 | **実装済み・確認済み** |
| **14B** | Participant 共通 KO 状態・KO 遷移 | **実装済み・確認済み** |
| **15** | J Punch 攻撃データ化（固定値の参照元整理） | **実装済み・確認済み** |

既存工程表の段階14（**HP、Damage、KO**）は **14A + 14B で充足・完了**。
既存工程表の段階15（**攻撃データ化**）は **完了**（ScriptableObject 化は見送り。コード内の読み取り専用データ）。
Round 終了・勝敗判定は工程表上の段階14/15には含まれず、**対戦モード側の後続候補**として残す（練習モードである FightDebugScene には混在させない。§1.1）。
正式な次工程番号は未定義。新 Stage 番号は作らない。

段階14Aの暫定「0HPでも戦闘継続」は**終了**。段階14Bから 0HP 到達で KO へ遷移する。

### 2.1 責務分担（要約・段階15後）

| 入れ物 | 担当 |
|---|---|
| **`DebugAttackData` / `DebugAttackData.JPunch` / `DebugAttackData.GroundKick`** | **J Punch / Ground Kick 攻撃設定値の正本**（進行状態は持たない） |
| **`DebugFighterAttackState`** | 攻撃進行の正本（ActionFrame / HasCurrentJPunchHit 等） |
| **`DebugFighterHitState`** | 被 Hit / HitStun / ノックバック速度（HP・KO・攻撃データは持たない） |
| **`DebugFighterParticipant`** | **HP 正本** + **KO 正本**（`isKnockedOut`）。local→world Hit Box 変換・Facing 反転 |
| **`DebugFighterMotor`** | LogicalX/Y・`initialLogicalX/Y`・ジャンプ軌道・Training Reset 時の位置/Jump 復帰（攻撃データ非所有。歩行 Visual 判定はしない） |
| **`DebugFighterVisual`** | `FighterVisualState` → Sprite。`Apply` / `CurrentVisualState` / `IsAttackPoseActive`（入力判定はしない） |
| **`DebugFighterPushResolver`** | 等分 Push ＋壁際再配分（攻撃データ非所有） |
| **`DebugPunchHitResolver`** | world Hit×Hurt 重なり判定（攻撃全体の設定正本にはしない） |
| **`SimulationTimeState`** | 共有時間・共有 HitStop |
| **`SimulationSession`** | tick 進行、攻撃開始/終了、Jump 種類判定・開始要求、Hit 適用、HitStop 開始、KO 接続、Push skip 判断、**Visual State 決定**、release gate（数値の正本にはしない） |

### 2.2 段階14A（HP / Damage・維持）

- HP 正本: Participant（maxHitPoints=100、current は実行時）
- J Punch Damage は段階15以降 **攻撃データ**（値は従来どおり 10）。有効 Hit 1回につき 1 Damage（MarkHit ガード）
- 0 未満 Clamp。Reset で全回復

### 2.3 段階14Bで確定した KO（維持）

| 項目 | 内容 |
|---|---|
| **正本** | `DebugFighterParticipant.isKnockedOut`（HitState / Motor / Push 非所有） |
| **API** | `IsKnockedOut` / `TryEnterKnockout` / `ClearKnockoutForReset` / `BuildLifeLabel` |
| **遷移条件** | 有効 Hit → ApplyDamage 後 `CurrentHitPoints <= 0` かつ未 KO |
| **TryEnterKnockout** | 初回のみ true。進行中 Attack は `InterruptByHit` |

**処理順（最後の一撃・段階15でも維持）**:
`ReceiveHit` → `ApplyDamage` → `TryEnterKnockout` → `MarkHit` → HitStop 開始

ReceiveHit 時点で HitCount / HitStun / Knockback は設定済み。
最後の一撃の Damage / HitCount / HitStop / HitStun / Knockback は通常どおり成立。
KO 後もその最後の一撃の Knockback / HitStun は処理される。HitStun 終了後も KO は解除しない（Reset まで維持）。

**KO 中の制御**（戦闘処理は段階15で変更なし）:
- 入力移動禁止: `ProcessOneFighterMovement` 先頭（Knockback は別経路で継続）
- 新規攻撃禁止: `TryStartJPunchForParticipant` 先頭
- KO 済み防御者への追加 Hit 拒否: `CollectPendingHit` 内（旧 `TryResolveJPunchHit` は削除済み）
  → Damage / HitCount / HitStop / HitStun / Knockback 再設定なし
  → 攻撃側 Action は開始・終了し結果は Miss。HUD ラベル `DefenderKO`

**KO 視覚（履歴と現在仕様）**:
- 段階14B 時点の記録: 優先 **KO > HitStun > 通常 Tint**（最後の一撃の HitStun 中も KO 色）。Scene 変更なし。
- **段階15 検証中の回帰修正後（現在仕様の正本）**: 優先 **HitStun 被 Hit 表示 > KO 暗色 > 通常 Tint**。
  - `ApplyDisplayColor` のみ変更。`hitState.IsInHitStun` を既存どおり使用。
  - KO 状態（`isKnockedOut`）の開始時点・`TryEnterKnockout` 処理順は変更していない。
  - 最後の一撃でも赤表示のあと HitStun 終了で KO 暗色へ移行。KO 後追加 Hit では赤にならない。

**HUD**: `P1 Life` / `P2 Life`（Alive / KO）。HP・HitCount・Stun・KB と併記。

**ログ**: 初回のみ `Fighter KO`。Punch hit に `KO=0/1`。Reset に `/KO`。

**Reset（Training Reset）**:

- **従来（段階12A〜位置復帰追加前）**: HP 最大 + KO 解除 + Attack/HitCount/HitStun/Knockback/HitStop 等。**位置・Facing は維持**していた。
- **現在**: 上記に加え、P1/P2 の論理 X を Scene 開始時へ戻し、両者復帰後に位置関係から Facing を再計算する（**実装済み**。正式 Stage 番号なし）。
- **Editor 確認済み**: 壁際（P1X=6.00 / P2X=7.00）→ R → 0.00 / 3.00、Facing 初期どおり。KO 後も R で Alive/100・HitCount=0。Push / Hit / HitStop / KO 回帰維持。Compile Error なし（既知 CS0618 以外の新規警告なし）。
- **未確認**: Pause 中 R の詳細総合回帰（基本動作はPlay確認済み。§1.1）、Reset 直後の再移動／再 J Punch、Development Build、Y/Z 復帰、P2 左側配置の別 Scene。

### 2.4 段階15で確定した攻撃データ化

**目的**: J Punch 固有の固定値を Session 等へ散在させず、1つの読み取り専用攻撃データから参照する。

| 項目 | 内容 |
|---|---|
| **型** | `Game/Assets/Scripts/Combat/DebugAttackData.cs` |
| **性質** | MonoBehaviour ではない / ScriptableObject ではない |
| **保持内容** | 攻撃の設定値のみ（実行中の攻撃進行は持たない） |
| **正本** | `static readonly DebugAttackData.JPunch`（`CreateJPunch()` で1回生成） |
| **Session 参照** | `private static readonly DebugAttackData JPunchData` |
| **生成頻度** | 毎 Frame / 毎 Hit で `new` しない |

**`DebugAttackData.JPunch` の値**:

| 項目 | 値 |
|---|---|
| AttackId | JPunch |
| StartupFrames | 3 |
| ActiveFrames | 3 |
| RecoveryFrames | 6 |
| TotalFrames | 12 |
| Damage | 10 |
| HitStopFrames | 6 |
| HitStunFrames | 12 |
| KnockbackInitialVelocityX | 0.180 |
| KnockbackDecelerationPerCombatFrame | 0.015 |
| Hit Box local CenterX / CenterY | 0.75 / 1.25 |
| Hit Box HalfWidth / HalfHeight | 0.55 / 0.35 |

**Frame 境界（既存実装を移しただけ。境界自体は変更なし）**:

| 区間 | ActionFrame |
|---|---|
| Startup | 1〜3 |
| Active | 4〜6 |
| Recovery | 7〜12 |
| 攻撃終了 | `ActionFrame >= TotalFrames`（12） |

**データ参照へ置換した内容**:
Startup / Active / Recovery 判定、TotalFrame と攻撃終了、Damage、HitStop、HitStun、Knockback 初速・減速、local Hit Box、Attack started ログ、Punch hit の値、HUD の JPunch Data。

**1攻撃1Hit**: 従来どおり `HasCurrentJPunchHit` + `MarkHit`。Active 3Frame でも 1攻撃1Damage。処理変更なし。

**ScriptableObject 化を見送った理由**:
- 今回は固定値の参照元整理が目的
- Inspector 編集や Asset 依存を増やす前段階
- 複数攻撃・Character 別データがまだない
- SO 化は後続で必要性を判断する
- 当面はコード内の不変データとして保持

**段階15で実装していないこと**:
ScriptableObject 化、Inspector 編集、Character 別攻撃データ、複数攻撃、弱/中/強、技コマンド、コンボ、Guard、Counter Hit、攻撃キャンセル、アニメーションイベント、JSON/CSV、Round/勝敗/Result、KO 専用アニメ、HP バー。

### 2.5 HUD / ログ（段階15）

**HUD（攻撃データから生成。固定値の再記述なし）**:
`JPunch Data : S/A/R 3/3/6 Dmg 10 HStop 6 HStun 12 KB 0.180`

- 初回配置は状態 HUD 末尾付近にあり、`Truncate` と高さ制限で非表示だった
- 修正（HUD のみ・Scene 変更なし）:
  - `JPunch Data` 行を `P2 KB Vx/Act` 直後へ移動
  - `HelpBlockHeightPixels` 120→92（ランタイム `EnsureSplitHudLayout`）
- 操作説明の欠け・重なりなしを確認

**ログ**:
- Attack started: `[FightDebug] Attack started slot=P1 attack=JPunch S/A/R=3/3/6 Damage=10 HitStop=6 HitStun=12 KB=0.180`
- Attack ended: `[FightDebug] Attack ended slot=P1 attack=JPunch`
- Punch hit: 既存項目を維持し、値は `DebugAttackData.JPunch` から参照

### 2.6 確認済みの挙動（段階15）

**A. コンパイル**: Error 0。Warning 1（既存 CS0618: `TMP_Text.enableWordWrapping`。Stage 15 由来の新規 Warning なし）。

**B. 初期**: 両体 HP=100、Life=Alive、HitCount=0、Stun=0、KB=0、HitStop=0。JPunch Data HUD 表示確認。

**C. 遠距離 Miss**: Attack started/ended、`attack=JPunch`、S/A/R=3/3/6。P2 HP/HitCount 不変、HitStop=0、AttackResult=Miss。

**D. 1Hit**: Damage=10、HP=90、HitCount=1、KO=0、KB=0.180。HitStop 6Frame（SimulationTick と CombatFrame の差で確認）。Active 3Frame でも 1Hit のみ。HitStun 終了後 Idle、KB→0。

**E. Frame 境界**: Startup 1〜3 / Active 4〜6 / Recovery 7〜12。Recovery 中 AF7/8/10 ログ確認。HitStop 中 ActionFrame 停止。AF>=12 で終了。既存境界維持。

**F. KO 回帰**: 9Hit で HP=10 Alive HitCount=9。10Hit で HP=0 Life=KO HitCount=10、KO ログ1回。最後の一撃の HitStop/Stun/KB 維持。KO 後暗色維持。

**G. KO 表示回帰修正**: 最終 Hit で赤→その後 KO 暗色を目視確認。通常 Hit の赤維持。KO 後追加 Hit は赤なし。Reset で Alive 色。戦闘処理・Scene/Prefab 変更なし。

**H. Reset（段階15時点の記録）**: 当時は両体 100/Alive、HitCount=0、Stun/KB/HitStop=0、AttackResult=None。**位置と Facing は維持**（当時仕様）。位置復帰は後続の Training Reset 拡張で追加（§1.1・§2.3）。

**I. Stage 13・14 回帰**: 中央 Push・右端再配分・Dist=1.00・Clamp・HP Clamp・KO 追加 Hit 拒否・Reset 維持。

### 2.7 未直接検証（実装済み・P2 実操作未確認）

現在 P2 は Dummy（Neutral）のため、次は**コード経路確認済み／P2 実操作では未検証**:
- KO した Participant 自身の左右入力禁止
- KO した Participant 自身の新規攻撃禁止

将来 2P 入力 / AI 時に共通経路を実操作確認する。検証済みとは書かない。

### 2.8 段階14B 時点の確認メモ（履歴・上書きしない）

段階14B 検証時: 10Hit で KO、追加攻撃は Miss、Reset で Alive/100、Stage 13/14A 回帰正常。
当時の Visual 優先は **KO > HitStun**（段階15 で現在仕様へ修正。§2.3 参照）。

### 2.9 未実装（段階15計画外・後続候補）

正式な優先順位・工程番号は未割当（順不同）。

**対戦モード固有（未実装・FightDebugScene に混在させない）**

- Round 終了、勝敗判定、WIN/LOSE、KO 後時間停止、リザルト、ラウンド再開始
- 複数ラウンド、タイマー、READY/FIGHT、Match 終了、対戦用 HUD、開始前・終了後の入力制限

**練習モード／共通まわりの候補**

- KO 専用アニメ、Down 物理、HP バー、Guard
- Character 別 KO / 攻撃データ、KO 演出制御
- 壁バウンド等（工程番号なし残課題）
- 攻撃データの ScriptableObject 化 / Inspector 編集 / JSON・CSV
- 複数攻撃、弱/中/強、技コマンド、コンボ、Cancel、Counter Hit
- 自然な歩行素材の再制作（現状 Walk 8 コマは動作確認済み）
- **ClashRecoil専用Sprite／演出の追加**（状態・専用色・通常HitCount非加算は実装済み。`debugMirrorP1InputToP2` のScene保存値はOFF）
- Punch 3 枚以上への素材改善
- Character Data ScriptableObject（ジャンプ設定の正式データ化含む）
- Jump 数値調整、Development Build Profiler
- 本番P2 AI（現在は鏡写しDebug入力ソースのみ。差し替え入口は用意）
- 練習用将来候補: ダミー回復、自動回復、ガード設定、行動記録、判定表示、フレーム表示など

（Training Reset・release gate・ジャンプ基盤・Visual Sequence / Sprite Sheet は **§1.1・§1.2 で実装済み**。未実装候補からは外す。）

### 2.10 相打ち・キャラ差し替え

- Ground Clash: 双方候補かつ双方地上攻撃なら成立（技種不問）。同技は旧検証フラグでEditor実測済み。異技 **P1 GroundKick → P2 JPunch** はSwap＋Delay5でPlay実測済み
- 発生差により片側Normal Hitになる場合あり（Delay4/6で実測）。Counter Hit／Attack Priorityなし
- Airを含む双方候補: 結果未適用（正式Air Clashではない）。双方AirKick同士はPlay実測済み。異種Air双方は未確認
- P2: 既定はNeutral。基本鏡写し・攻撃変換／地上Delay・AirKick Assistはpush済みの検証専用補助。Assist予約安全確認の追加Playは未コミットDocs
- キャラ差し替え・複数 Hurt/Hit Box: 未実装

---

## 3. Facing / Push（維持）

Facing 分離・壁際 Push 再配分は実装済み。Push は HP / KO / KB 速度に触れない。攻撃データも持たない。

---

## 4. 判定箱方針（維持）

Push / Hurt / Hit 可視化（11A）と Hit×Hurt 重なり判定（11B）は完了。
段階15: local Hit Box 定義は攻撃データ、world 変換・Facing 反転は Participant、重なりは PunchHitResolver。

---

## 5. 推奨工程順（見直し後）

2026-07-31時点でSprite Sheet正式採用は完了。次の独立した保留タスクとして、**Hurt／Push BoxのFacing対応と前後非対称化**を追加する。現状はP1/P2ともCenterX `0`、HalfWidth `0.75`（全幅1.50）の左右対称暫定値である。Hurt/PushのCenterX反転だけを先行させず、`DebugFighterPushResolver`がWorld Push Box中心を使うよう揃え、表示と押し合い判定を一致させてから前方／背面幅を調整する。

| 段階 | 内容 | 区分 |
|---|---|---|
| **10A〜13B-1** | Facing〜壁際 Push 再配分 | **完了** |
| **14A** | Participant 共通 HP・Damage 基盤 | **完了** |
| **14B** | Participant 共通 KO 状態・KO 遷移 | **完了** |
| **14**（全体） | HP、Damage、KO（工程表どおり） | **完了**（14A+14Bで充足） |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | **完了**（コード内不変データ。SO 化は見送り） |

その後の候補（順不同・未着手。**新工程番号は作らない**。正式な次 Stage も未定義）:

- ClashRecoil専用Sprite／演出の追加、Punch 3 枚以上への素材改善
- Animator + Animation Clip
- Air Hit / Air Knockback（**空中被弾**。空中攻撃ではない）
- Guard
- 将来の空中攻撃（仕様未定・空中パンチは対象外）
- Character Data ScriptableObject 化（ジャンプ設定含む）
- Jump 数値調整、Development Build Profiler
- 対戦モード用 Scene / Controller / HUD（Round / 勝敗 / タイマー / リザルト等。FightDebugScene とは分離）
- Down、HP バー
- ノックバック壁到達時の速度停止、壁バウンド、壁やられ、Corner
- しゃがみ、複数攻撃、入力バッファ、キャンセル
- 攻撃データの ScriptableObject 化（必要になったとき）
- 2P 入力 / CPU（KO 中移動・攻撃禁止の実操作確認、P1被Hit・左方向KB を含む）
- 複数 Hurt / Hit Box、キャラ固有データ化
- 練習モード将来候補: ダミー回復、自動回復、ガード設定、行動記録、判定／フレーム表示など

Training Reset・ジャンプ基盤・Visual Sequence / Sprite Sheet・release gate は完了（§1.1・§1.2）。モード責務の方針も同節。Round/勝敗は**対戦モード固有**であり、練習 Scene の次必須工程としては未確定。

---

## 5.1 GC 学習・計測の補助改善（正式 Stage ではない）

Round / Guard / 複数攻撃などの**機能 Stage とは別枠**。番号「GC-1」「GC-2」は便宜名。

### GC-1 Fixed Help Text Allocation Reduction

| 項目 | 内容 |
|---|---|
| **実装** | `DebugHudView`: Update から固定 Help の毎 Frame 再構築・再代入を削除。設定は Awake → EnsureSplitHudLayout で1回 |
| **非対象** | Status HUD（`BuildStatusHudText`）は GC-1 では未変更（毎 Frame） |
| **実測条件** | Unity 6.3 LTS / Editor Play Mode / FightDebugScene / 通常待機 |
| **変更前** | フレーム全体 約18.0 KB・69 alloc。`DebugHudView.Update` 約17.8 KB。GC.Collect 0.000 ms |
| **変更後** | `DebugHudView.Update` 約17.2 KB（約0.6 KB・約3.4%削減）。全体 KB / alloc 回数 / GC.Collect は**未記録** |
| **回帰** | Help 表示・Status HUD 表示は維持。Compile Error なし。既知 CS0618（enableWordWrapping）1件 |
| **未計測** | Development Build 測定、変更後のフレーム全体値 |

詳細: `docs/component_and_scene_guide.md` §16.13。

### GC-2 Status HUD StringBuilder Reuse

| 項目 | 内容 |
|---|---|
| **実装** | 再利用 `StringBuilder(2048)` + `Clear` + `Append`。`hudText.SetText(statusTextBuilder)`。毎 Frame 更新は維持 |
| **非対象** | Help（GC-1 のまま）、戦闘処理、更新頻度の低下、小数 `ToString` の完全除去、`DebugBox2D` 型変更 |
| **実測条件** | Unity 6.3 LTS / Editor Play Mode / FightDebugScene / 通常待機 / `DebugHudView.Update` |
| **変更前（GC-1後）** | 約17.2 KB / frame |
| **変更後** | 約3.2 KB / frame（別通常フレームでも約3.2 KB を確認） |
| **削減** | GC-2 単体 約14.0 KB・約81.4%。初期状態（約17.8 KB）比 約14.6 KB・約82.0% |
| **回帰** | 表示・Miss/Hit/KO/Push/Reset まで確認。Help・Status 毎 Frame 更新維持。Error 0。既知 CS0618 1件 |
| **残存候補** | 小数 `ToString`、`DebugBox2D`（class）の HUD 用 Box 生成、TMP 内部。内訳・Development Build は**未計測** |
| **未記録** | 変更後のフレーム全体 KB・alloc 回数・GC.Collect（推測しない） |

詳細: `docs/component_and_scene_guide.md` §16.14。

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

- 段階1〜**15**まで到達。工程表の段階14（HP/Damage/KO）と段階15（攻撃データ化）は完了
- Visual Sequence + Sprite Sheet 移行・ジャンプ基盤・計測ログ・Training Reset release gate は実装・Editor 確認済み。正式 Stage 番号なし
- `FightDebugScene` は**練習・検証モード**。戦闘コア共通、KO 後処理はモード側（§1.1）
- Visual: Sequence再生＋`Fighter_SpriteSheet` **41 sub-sprite**（PPU 39、Idle/Walk/Jump/Punch/Ground Kick/Air Kick接続済み）。Animator未使用
- 飛び越し後 Facing 反転・左向き Walk / Jump / J Punch は Editor 確認済み。J Punch は地上専用（ジャンプ中開始不可）。Air Hit / Air Knockback・ClashRecoil専用Sprite／演出・Dev Build Profiler は未実装／未確認
- 正式な次 Stage 番号は未定義。候補は順不同（ClashRecoil専用Sprite／演出、Punch / Ground Kick / Air Kick素材改善、Animator、Air Hit/KB、Guard、Character Data SO、対戦モード分離など。Air Kick最小検証版は実装済みだが正式仕様は未確定）

## 6. Ground Kick + 共通 Hit 解決基盤（2026-07-30）

正式 Stage 番号なし。コミット `bc80ddb`、`origin/unity` へ push 済み。

### Ground Kick 設定と入力

| 項目 | 現在値／挙動 |
|---|---|
| 入力 | P1 `K` |
| 開始条件 | Grounded、行動可能、KO/HitStun/他攻撃中でない |
| J+K | 同Tick相当では J Punch 優先 |
| 空中入力 | Ground Kick開始なし。K保持で着地しても予約発生せず、離して押し直すと開始 |
| キャンセル | Punch→Kick / Kick→Punch ともなし。入力予約なし |
| S/A/R | 8 / 3 / 4（Total 15） |
| Damage / HitStop / HitStun | 14 / 7 / 14 |
| Horizontal KB | 0.24 |
| local Hit Box | center `(0.95, 0.55)` / half `(0.60, 0.25)` |
| Visual | Kick 5枚、3CF/枚、Loop OFF、Hold Last Frame ON |

### Editor確認

- 近距離で `actual=14`、`P2HitCount=1`、追加Hitなし
- HitStop開始／終了、右向き・左向き双方のHit、相手を離れる方向のノックバック
- 空中で開始しない、空中K保持から着地しても出ない
- Punch中Kick、Kick中Punchで途中切替しない
- 通常速度で近いJ/K入力はPunch優先を確認

### 共通 Hit 解決基盤

`DebugAttackId`、`DebugAttackPhase`、`DebugPendingHit`、`DebugHitResolutionType`、`DebugClashTuning` を追加。各方向のHit候補を同一 CombatFrame で収集後に解決する構造へ移行した。正本メソッドは `CollectAndResolveHitsForCombatFrame`。旧即時経路 `TryResolveJPunchHit` は削除済み。

旧検証フラグ `debugForceP2AttackWithP1ForClashTest` を一時的にONとして、JPunch同士／Ground Kick同士のGround ClashをEditor実測済み（Damage 0、HitStop、双方反動、攻撃終了、Idle復帰）。コミット`78c4e94`で通常Hitから専用`ClashRecoil`状態へ分離し、黄色系表示と通常HitCount非加算を確認。検証後のScene保存値はOFF。フラグは後に `debugMirrorP1InputToP2` へ置換。

### Ground Clash専用実測（2026-07-30）

- JPunch同士: CombatFrame同時成立、Damage 0、HitStop、双方反動、攻撃終了、Idle復帰
- Ground Kick同士: 同内容を確認
- 通常Hitではなく`AttackResult=Clash`になる
- `ClashRecoil`専用状態と黄色系専用色を表示し、HUD状態名も`ClashRecoil`となる
- Clashでは通常HitCountを増やさず、JPunch同士／Ground Kick同士とも`P2HitCount=0`を確認
- 専用Sprite Sequence未設定時はIdleへfallback。Scene差分なし、コード側初期値で同動作を再確認
- 当時の検証フラグは通常OFF。現行は `debugMirrorP1InputToP2`（既定OFF）

### Clash判定整理＋基本P2鏡写し（push済み `5bd3627` / Docs `6bc5f03`）

- 正本フロー: (1) P1→P2 / P2→P1 候補収集 (2) 組み合わせ分類 (3) Ground Clash または Normal Hit 適用
- Ground Clash対象の地上攻撃: `JPunch` / `GroundKick`。技種一致は不要
- Air Kickを含む双方候補: 結果未適用、仮Air Clashなし、片側Normal Hitへ落とさない、警告はセッション中1回（双方AirKick同士はPlay実測済み。§下記Assist節）
- Counter Hit / Attack Priority / 技の固定重みは導入しない
- 同技Ground Clashは確認済み。基本の`debugMirrorP1InputToP2`（左右／Jump／地上攻撃鏡写し、SimulationInputState経由）もpush済み

### P2鏡写し攻撃変換・CombatFrame遅延・異技Clash実測（push済み `96f8148` / Docs `6be8bb4`）

- `DebugP2MirrorAttackMode`（コード既定 SameAsP1）: SameAsP1 / SwapPunchAndKick / NoAttack。双方接地時のみ攻撃変換。Start技の直接呼び出しなし
- `debugP2MirrorAttackDelayFrames`（0〜15・既定0）: Attack/Kick押下開始だけCombatFrame予約／発火。左右・Up・Downは遅延しない。検証専用（本番AI反応時間ではない）
- OFF／NoAttack／Training Reset等で遅延予約を破棄。**Mirror OFFおよびTraining Resetによる予約破棄はPlay確認済み**（Swap・Delay15。発火予定CF通過でfiredなし／P2 JPunch開始なし）

#### 異技Clash Play実測（SwapPunchAndKick・**P1 GroundKick → P2 JPunch**・近距離）

| Delay | P1開始 | P2開始 | 結果 |
|---:|---:|---:|---|
| 4 | GK CF991 | JP CF995 | P2 JPunch先勝ち（Normal Hit）。P1は同CFで候補成立前 |
| 5 | GK CF1677 | JP CF1682 | **Ground Clash CF1686**。双方Active／双方候補同一CF。**P1 GroundKick → P2 JPunch** / Damage0 |
| 6 | GK CF2253 | JP CF2259 | P1 GroundKick先勝ち（Normal Hit）。P2は同CFでActive前 |

同一CFに双方候補があるかで結果が決まること（処理順の有利ではないこと）を実測確認した。技性能・Hit Box・Clash条件は変更していない。

#### 検証用Debugログ（本番恒常ログではない）

Attack started（SimulationTick／CombatFrame／S/A/R／mirrorDelay／mode）、Attack active/recovery started、Pending hit candidate、Mirror attack delay reserved/fired。

**確認済み（地上・push済み）**: 攻撃変換モード、CombatFrame遅延、検証ログ、NoAttack時Normal Hit、異技Clash **P1 GroundKick → P2 JPunch**（Delay5）、Delay 4／5／6境界、Mirror OFFおよびTraining Resetによる地上Delay予約破棄。

**未確認（継続）**:

- **P1 JPunch → P2 GroundKick** で双方候補が同一CFになる条件でのClash実測
- 将来のP2 Movement／Stance／Guard Mode
- Hurt／Push BoxのFacing対応と前後非対称化

#### 将来のP2検証設定案（未実装）

- P2 Movement Mode: Neutral / Mirror P1
- P2 Stance／Guard Mode: Normal / Force Stand / Force Crouch / Stand Guard / Crouch Guard
- Down入力の扱いはしゃがみ実装時に再検討

### P2 AirKick検証アシスト・Air双方未適用実測（push済み `03bfd72` / Docs `988f36f`）

検証専用。正式P2操作・本番AI・対戦ルールではない。既定OFF。地上攻撃変換とは別責務。Air双方確認時はNoAttack併用推奨。

| 設定 | 既定 |
|---|---|
| `debugEnableP2AirKickAssist` | OFF |
| `debugP2AirKickDelayFrames` | 0（Range 0〜15） |

Scene既定: 鏡写しOFF、Mode=NoAttack、地上Delay0、Assist OFF、Assist Delay0。Play中Inspector変更はScene既定値変更ではない。

経路: P1がAirKick開始可能なKickエッジ → 予約 → 指定CFでP2 `SimulationInputState`へKick 1tick → 通常SampleAttack／TryStartKick／CanStartAirKick。`StartAirKick`直接呼び出し・PendingHit注入なし。発火時にP2開始不能ならKick非載荷（接地中のGroundKick化防止）。予約はAwake／Training Reset／Mirror OFF／Assist OFF／発火成功／発火不能破棄でクリア。

#### Play実測（Air双方未適用・push済み）

Mirror ON・NoAttack・Assist ON・Delay0・近距離Neutral Jump・空中P1 K。
CF1352 reserved+fired → CF1353双方AirKick開始 → CF1358双方Active・双方Pending・Unsupported mutual hit警告。
Ground Clash／Normal Hitなし。HP等不変。自然終了。最終HUD AttackResultは既存Miss表示。同一Playで警告1回（CF387→CF881）。

#### Assist予約安全確認（2026-08-03・未コミット・Play実測）

地上Delay（`debugP2MirrorAttackDelayFrames`）の破棄Playとは別系統。Delay15・NoAttack・初期距離3.0・Neutral Jump。

| 確認 | 実測要約 |
|---|---|
| Mirror OFF | CF670予約→fire685。Pause中Mirror OFF。CF900まで進行しても`fired`なし。P2 AirKick／GroundKickなし。`reason=MirrorOff`明示ログは未確認 |
| Assist OFF | CF938予約→fire953。Mirror ONのままAssistのみOFF。CF1140まで`fired`なし。P2攻撃開始なし。`reason=AssistOff`明示ログは未確認 |
| Training Reset | CF681予約→fire696。Pause中R。CF1020まで`fired`なし。位置0.00/3.00・HitCount0・Idle・Jump解除・HitStop0・HP/KO初期化・Gameplay input再有効化。`reason=Reset`明示ログは未確認 |
| 接地時破棄 | CF1352→1367／CF1841→1856で`cancelled ... reason=P2CannotStartAirKick`（実ログ）。GroundKick化なし |

複合条件: 被弾・CombatReactionを含む条件下でも誤発火しないことを確認（単独専用テストではない）。

**確認済み（Assist・Play）**: SimulationInputState経路、Air双方未適用＋警告1回、Mirror OFF／Assist OFF／Training Resetによる予約無効化、OFF/Reset後の発火予定CF通過でも古い予約が発火しない、接地時P2CannotStartAirKick、着地後GroundKick化防止、Reset後の主要状態初期化と入力再有効化。

**未確認（Assist）**: KO／HitStun／ClashRecoil等CombatReaction／ActionPlaying／AirKickUsedThisJumpの各単独破棄、Assist Delay 1〜14境界、Play再開始後の警告再出力、異種Air双方、正式Air Clash／正式P2操作・AI。

## 7. Air Kick最小検証版・通常技暫定調整（push済み・暫定）

### 2026-07-31 Sprite Sheet正式採用後の状態

- 正式Assetは`Game/Assets/Art/Characters/Fighter_SpriteSheet.png`。GUID `42345be00e0994144ba94bc5f1362757`、41 Sprite、PPU 39、Point、Compression None、Mip Map Off
- 192×192固定セル＋Center Pivotは透明余白中心が基準となって表示が上へずれたため不採用。各セル内の実画素Trim Rect＋Center Pivotを正式採用
- 再構築版GUIDとinternalIDを維持してAsset名のみ正式化。Sub-Asset名`FighterRebuilt_...`も参照破損回避のため維持
- 旧GUID `dcb7851d129f2305be49fac973bf47b4`のGame/Assets参照0件を確認後に削除。旧PNG/metaはAssets外へバックアップ済み
- P1/P2の初期Sprite、Idle／Walk／Jump／Punch／Ground Kick／Air Kickを接続済み。正式GUID参照76件、不明internalID 0件
- `DebugFighterVisual.airKickSequence`へ`FighterRebuilt_AirKick_00`〜`05`を接続（1CF/枚、Loop OFF、Hold Last Frame ON）。Startup／Activeで使用し、RecoveryはJumpFall
- 確認済み: Import、C#コンパイル、Missing Sprite/参照例外なし、P1/P2 Idle、Air Kick専用Flying Kick表示
- 未確認: 移動、Jump、J Punch、Ground Kick、通常Hit、Ground Clash、HitStun、Training Resetを通した最終総合回帰

### 区分

- **実装済み・Play確認済み**: Air Kick開始、1ジャンプ1回、着地即終了、既存Hit判定への接続、Visual分岐
- **暫定**: 全フレーム値、Recovery Visual境界、Ground Kick画像流用、空中被弾時の既存HitStun＋横KB流用
- **未実装**: Air Hit、Air Knockback、縦KB、Air Clash、正式Trade
- **表示**: Air Kick専用`airKickSequence`は `5bcc3fa` で接続済み（push済み）

### 変更C#ファイル（履歴。現在HEADの未コミット範囲ではない）

- `Combat/DebugAttackId.cs`: `AirKick`を既存値を変えず末尾追加
- `Combat/DebugAttackData.cs`: Air Kick専用データ、J Punch / Ground Kick暫定フレーム値
- `Fighter/DebugFighterAttackState.cs`: Air Kick状態と1ジャンプ1回制限
- `Fighter/FighterVisualState.cs`: `AirKick`を末尾追加
- `Fighter/DebugFighterVisual.cs`: 専用`airKickSequence`（`5bcc3fa`）
- `Simulation/SimulationSession.cs`: K入力分岐、着地終了、Phase別Visual、HUD / Console識別

### 現行暫定値

| 攻撃 | Startup | Active | Recovery | Total | Damage | HitStop | HitStun | 横KB |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| J Punch | 4 | 3 | 8 | 15 | 10 | 6 | 12 | 0.18 |
| Ground Kick | 9 | 4 | 13 | 26 | 14 | 7 | 14 | 0.24 |
| Air Kick | 5 | 5 | 10 | 20 | 14 | 7 | 14 | 0.24 |

KB減速度とlocal Hit Box値は従来値を維持。Hit Boxは全技ともActive中だけ有効。

### 入力・状態

- CombatFrame開始時点で接地中のKはGround Kick、すでに空中ならAir Kick
- 地上Up+KはGround Kick。J+KはJ Punch優先。空中Jは攻撃なし
- Air Kickは1ジャンプ1回。上昇・頂点・下降で開始可能、入力予約なし、着地で途中終了
- 地上／空中相手とも幾何学的なHit Box対Hurt Box重なりで命中可能
- Recovery中は`IsActionPlaying`を維持し、新しい攻撃・ジャンプを禁止

### Visualと内部Recovery

| 攻撃 | 攻撃Visual | Recovery後半Visual | 内部状態 |
|---|---|---|---|
| J Punch | AF1〜10 Attack（AF8〜10は振り切り） | AF11〜15 Idle | AF15終了までRecovery |
| Ground Kick | AF1〜19 Kick（AF14〜19は振り切り） | AF20〜26 Idle | AF26終了までRecovery |
| Air Kick | Startup / ActiveはAirKick | RecoveryはJumpFall | 着地またはTotalまでRecovery |

Visualが攻撃姿勢でもRecovery中のHit Boxは無効。VisualがIdle / JumpFallへ戻っても内部Recoveryと行動制限は継続する。

### Play確認と評価

- J Punch: 以前より見やすく、軽い技として暫定採用候補
- Ground Kick: フレーム調整後も見た目改善は限定的。コード不具合と断定せず、Kick Spriteの脚の伸び・シルエット不足の可能性を記録
- Air Kick: `5/5/10`へ調整後、以前より立ち相手へ当てやすい
- Ground Kickはさらに数値だけ遅くせず、脚を伸ばした専用Spriteへ再制作後に再調整する
- Air Kick専用モーション完成後、S/A/R、Hit Box、表示Sprite範囲を再調整する
- Scene / Prefab変更なし。現在値は正式確定値ではない
