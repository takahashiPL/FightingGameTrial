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
2. **`docs/component_and_scene_guide.md`** … FightDebugScene を題材にした Scene / Component / Inspector / 実行経路、および Managed Heap / GC / GC.Alloc / Profiler 実習の教材（§16.13 に **GC-1 実測記録**あり。GC-1 は正式 Stage ではない）
3. **`docs/rules.md`** … ゲーム仕様（SimulationTick / HitStop などの意味）
4. **`docs/unity_implementation_status.md`** … 段階ごとの実装済み・未実装・次工程（状況確認用）。補助改善 GC-1 は §5.1
5. コード … 教材 §13 の推奨順（`DebugAttackData` → 入力 → ClockDriver → Session → …）

「今どこまで実装されたか」を知りたいときは 4。
「Hierarchy と Inspector とコードを結びたい」ときは 2。
「GC.Alloc を Profiler でどう見るか／GC-1 の実測」も 2（§16）。実装状況の正本と混同しない。GC-2 は未実装。

## Unity 実装の到達点（要約）

ブランチ `unity` 上で、**段階1〜15まで完了**しています。
最新コミット済み HEAD: **`e157b7d`**（Add Unity GC learning guide）
段階14全体（14A+14B）・段階15（J Punch 攻撃データ化）: **完了・push 済み**（SO 化は見送り）。
GC-1（固定 Help 毎 Frame 停止）は補助改善。コード差分がある場合は未コミットのことがある。

| 区分 | 内容 |
|---|---|
| **実装済み** | 60Hz SimulationTick、Pause/Step、HUD（左上状態／左下操作・JPunch Data）、HitStop、入力、左右移動、Facing分離、2体共通 Participant / AttackState / HitState、Push Box、壁際 Push 再配分、Box 可視化、Hit×Hurt 重なり判定、Jパンチ、1攻撃1Hit、Hit時6F HitStop、HitStun 12CF・被Hit表示、横ノックバック、HP/Damage（max100・J Punch10）、**KO状態・遷移（Life表示）**、**`DebugAttackData.JPunch` による攻撃設定正本**、Rで戦闘デバッグ初期化（HP全回復・KO解除含む） |
| **暫定** | Push 等分＋壁際再配分。攻撃数値はコード内不変データ（SO 未使用）。KO 視覚は色変更のみ（優先: HitStun赤 > KO暗色 > 通常Tint）。Motor minX/maxX（±7）。相打ちは両方向判定の土台のみ（P2 は Neutral） |
| **未実装（正式方針）** | Round/勝敗、HPバー、Guard、壁バウンド等、複数 Hurt/Hit Box、キャラ固有データ化、攻撃データ SO 化（後続で要否判断）、複数攻撃・コンボ・Cancel |

詳細・次工程は **`docs/unity_implementation_status.md`** を正とする。

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
| 本番スプライトシート | **未完成** |
| デバッグ用透過PNG（Idle/Punch） | FightDebugScene 用の試験素材（本番清書ではない） |

## 次の実装候補（要約）

1. 残課題（工程番号なし・順不同）: Round/勝敗、KB壁停止、壁バウンド、壁やられ、Corner、HPバー、Guard
2. 複数攻撃・入力バッファ・キャンセル、攻撃データの SO 化（必要時）
3. 2P入力/AI時: KO中の移動・攻撃禁止の実操作確認、HitStun行動制限・P1被Hit・左方向KB・Facing Left 攻撃

詳細は `docs/unity_implementation_status.md`。

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
- `Game/Assets/Art/Characters/fighter_idle_00_transparent.png` 等 … デバッグ表示用の透過試験素材（本番シートではない）
- `docs/production_spritesheet_spec.md` … 制作工程とセル仕様の正本
- `docs/sprite_art_status.md` … 現在の素材は未完成であること

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
未確定: 空中パンチのガード可否、多段技、必殺技、キャラ差、高度な壁際補正。
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
