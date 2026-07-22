# 2D格闘ゲーム Trial 実装テンプレート v3

このパッケージは、フレーム単位の2D格闘ゲーム判定を**試作・学習**するための実装用ひな形です。

## 学習・可読性方針（重要）

このプロジェクトでは、処理効率やコードの短さより、**人間が読んで目的と処理順を追えること**を優先します。  
日本語しか分からない人でも、クラスの責務と「いつ何が起きるか」が分かることを目標にします。

詳細は **`docs/learning_and_readability.md`** を読んでください。要約:

- 多少冗長でも処理順が見える構成を選ぶ
- C# では日本語コメントを十分に入れる（実装開始後）
- 英語の名前だけで理解できる前提にしない
- Unity の `Awake` / `Update` / `FixedUpdate` 等も説明するが、**ゲーム仕様の正本は 60Hz の SimulationTick / CombatFrame**（`docs/rules.md` §10）
- 過度な LINQ・ワンライナー・難解な省略を避ける
- Inspector には Tooltip
- まず読みやすい基準実装を残し、最適化は後
- 中間状態をデバッグ表示する

**現時点では実装（C# / Scene）を開始していません。** 仕様とデータの正本整備が先行します。

## ブランチ構成

| ブランチ | 内容 |
|---|---|
| `main` | 共通資料（docs / data / schemas / art） |
| `unity` | 共通資料 + Unity プロジェクト（`Game/`） |

`1f294cc`（Add initial Unity 2D project）は、**Unity プロジェクトを追加したときの基準コミット**です（最新コミット番号ではありません）。  
Universal 2D テンプレートで作成済みです。  
現在の仕様・資料更新は**未コミットの作業ツリー上**にあります。存在しない「最新コミット番号」を推測して書かないでください。

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
5. `docs/production_spritesheet_spec.md`
6. `docs/debug_screen_spec.md`
7. 各種PNG参考画像

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
| 本番スプライト | **未完成** |

## 最初の実装対象（実装開始後）

1. SimulationTick / CombatFrame 進行（Pause / 1フレーム送り）
2. Idle
3. StandPunch（ActionFrame と duration の正しい進め方）
4. 判定箱表示
5. デバッグHUD初期必須項目（SimulationTick 等の日本語説明付き）

処理順の正本は `docs/rules.md` §10 です。

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
│  ├─ debug_screen_spec.md
│  └─ ...
├─ schemas/
└─ Game/          ← unityブランチで Universal 2D 作成済み
   ├─ Assets / Packages / ProjectSettings  ← Git管理
   └─ Library 等                           ← Git管理外
```

## 仮値・未確定（要約）

暫定値: JustGuardWindow=3、Damage、Stun、Pushback、Jump移動量、LandingRecoveryフレーム数 など。  
未確定: 空中パンチのガード可否、多段技、必殺技、キャラ差、高度な壁際補正。  
詳細は `docs/rules.md` §0。

## 主要仕様（要約）

- 後ろ単独＝後退。Down+Back＝その場しゃがみ（水平移動なし）
- ガードは接触時入力。CombatState=BlockStun、GuardPosture / DisplayAnimation は別
- JustGuard は Back の新規押下エッジ（攻撃弾き型）
- Clash は一次分類。Trade は双方向とも Hit のときの複合集約
- SimulationTick（入力）と CombatFrame / ActionFrame（戦闘）を分離
- 空中 Pushbox 無効。着地で復活＋等分分離
- `attack_category` はラベルのみ
- 必殺技は未実装・非表示
