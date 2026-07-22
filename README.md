# 2D格闘ゲーム Trial 実装テンプレート v3

このパッケージは、フレーム単位の2D格闘ゲーム判定を試作・学習するための実装用ひな形です。

## ブランチ構成

| ブランチ | 内容 |
|---|---|
| `main` | 共通資料（docs / data / schemas / art の仕様・CSV・参考画像） |
| `unity` | 共通資料 + Unity プロジェクト（`Game/`） |

現在の Unity 初期コミットは `1f294cc`（Add initial Unity 2D project）です。  
Universal 2D テンプレートで作成済みです。

### Git 管理について

`Game/` 配下のうち、次は Git 管理対象です。

- `Game/Assets`
- `Game/Packages`
- `Game/ProjectSettings`

次は Git 管理外です（編集・コミットしない）。

- `Game/Library`
- `Game/Temp`
- `Game/Logs`
- `Game/UserSettings`
- `Game/obj` など自動生成物

## 今回Unityを選定した理由

本プロジェクトでUnityを使用するのは、特定のエンジンだけを継続して学習するためではありません。今回の中心課題である2D座標、固定ゲームフレーム、入力バッファ、状態遷移、判定箱、CSV駆動を、小規模かつ明示的に実装・可視化しやすいためです。

UEでもPaper 2D、DataTable、Enhanced Inputなどで実装できます。将来はUnity版の仕様とデータ構造を基準に、UE版との比較検証を行う余地を残します。

## 正本の優先順位

1. `docs/rules.md`
2. `data/*.csv`
3. `schemas/data_dictionary.md`
4. `docs/production_spritesheet_spec.md`
5. `docs/debug_screen_spec.md`
6. 各種PNG参考画像

### スプライト配置の正本

セル配置の正本は **`data/sprite_frame_requirements.csv`** です。

- `art/spritesheet_layout.csv` は正本の展開表（同期する）
- `data/frames.csv` の `sheet_x` / `sheet_y` も正本と一致させる
- 参考PNGは配置正本ではない

## データの完成状態

| データ | 状態 |
|---|---|
| `frames.csv` / `boxes.csv` | **初期検証用サンプル**（Idle + StandPunch のみ） |
| `sprite_frame_requirements.csv` | 配置計画の正本。`initial` / `planned` を区別 |
| `moves.csv` | 技設計あり。数値はすべて `provisional`（仮値） |
| 本番スプライトPNG | **未完成**。`asset_status=required` |

詳細は `schemas/data_dictionary.md` を参照。

## 最初の実装対象

次の最小単位から始める。

1. 60Hz固定進行（Pause / 1フレーム送り）
2. Idle
3. StandPunch（Startup / Active / Recovery）
4. Pushbox / Hurtbox / Hitbox の表示
5. デバッグHUDの初期必須項目（`debug_ui_fields.csv` の `implementation_phase=initial`）

他モーション（キック、ジャンプ、ガード、Clash など）は `planned` / `not_implemented` とし、存在しない数値を推測で埋めない。

必殺技は初期版では未実装・非表示。

## スプライト画像について

- `art/fighter_motion_reference_sheet.png`
  - キャラクターデザインとモーション方向性の参考画像
  - `docs/reference_infographic.png` の白い道着・赤い鉢巻の格闘家を基準にしている
  - **Unityへそのまま取り込む完成スプライトシートではない**
- `art/spritesheet_grid_template.png`
  - 128×128セル、8列×8行の本番配置台紙
- `art/spritesheet_layout.csv`
  - `sprite_frame_requirements.csv` に同期したセル配置表
- `data/sprite_frame_requirements.csv`
  - 必須フレーム一覧・配置正本・制作状況
- `docs/production_spritesheet_spec.md`
  - 本番スプライトの透明背景、固定セル、ピボットなどの制作条件

v2に入っていた簡易図形の `fighter_spritesheet_v1.png` と `fighter_spritesheet_v1_boxes_preview.png` は削除しました。

## デバッグ画面資料

- `docs/debug_screen_wireframe.png`
- `docs/debug_screen_mockup.png`
- `docs/debug_screen_spec.md`
- `data/debug_ui_fields.csv`

モックアップは将来項目を含みうる。初期実装は CSV の `implementation_phase=initial` を満たせばよい。

## ディレクトリ構成

```text
Unity_FightingGameTrial
├─ README.md
├─ CHANGELOG.md
├─ art
│  ├─ fighter_motion_reference_sheet.png
│  ├─ spritesheet_grid_template.png
│  └─ spritesheet_layout.csv
├─ data
│  ├─ boxes.csv
│  ├─ debug_ui_fields.csv
│  ├─ frames.csv
│  ├─ hit_resolution.csv
│  ├─ moves.csv
│  ├─ sprite_frame_requirements.csv
│  └─ state_transitions.csv
├─ docs
│  ├─ debug_screen_mockup.png
│  ├─ debug_screen_spec.md
│  ├─ debug_screen_wireframe.png
│  ├─ production_spritesheet_spec.md
│  ├─ reference_infographic.png
│  ├─ rules.md
│  └─ sprite_art_status.md
├─ schemas
│  ├─ data_dictionary.md
│  └─ schema_summary.json
└─ Game
   ├─ Assets          ← Git管理
   ├─ Packages        ← Git管理
   ├─ ProjectSettings ← Git管理
   └─ Library 等      ← Git管理外
```

`Game/` には Universal 2D の Unity プロジェクトを作成済みです（unity ブランチ、コミット `1f294cc`）。

## 仮値・未確定

次はすべて初期検証用の仮値。

- JustGuardWindow = 3
- Damage / HitStun / BlockStun / Pushback
- Jump 移動量（現状は frames 未収録）
- 判定箱サイズ

**空中パンチのガード可否は未確定。** `moves.csv` の該当フラグは暫定値であり、仕様確定ではない。

## 固定ゲームフレーム

- 内部シミュレーション: 60Hz固定
- 1ゲームフレーム: 1/60秒
- 描画FPSとは分離
- ジャストガードと入力バッファはゲームフレーム番号で判定
- 一時停止中は1フレーム送りに対応

## 主要仕様

- A: パンチ
- B: キック
- X: 垂直ジャンプ
- X + 前: 前ジャンプ
- X + 後ろ: 後ろジャンプ
- 後ろ: 立ちガード
- 下 + 後ろ: しゃがみガード
- ジャンプ中はガード不可
- 空中攻撃は1ジャンプにつき1回
- 立ちパンチはしゃがみに当たらない
- 立ちキックは立ち・しゃがみ双方に当たる
- しゃがみキックはClash向け
- Hitbox同士の接触はClash
- Clashでは双方の攻撃を無効化し、双方を少し弾く
- 空中Pushboxは無効
- ダウン追撃なし
- WakeUp終了まで無敵
- 必殺技は初期版では未実装・非表示
