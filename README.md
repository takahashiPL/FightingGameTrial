# 2D格闘ゲーム Trial 実装テンプレート v3

このパッケージは、フレーム単位の2D格闘ゲーム判定を試作・学習するための実装用ひな形です。

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

## スプライト画像について

- `art/fighter_motion_reference_sheet.png`
  - キャラクターデザインとモーション方向性の参考画像
  - `docs/reference_infographic.png` の白い道着・赤い鉢巻の格闘家を基準にしている
  - **Unityへそのまま取り込む完成スプライトシートではない**
- `art/spritesheet_grid_template.png`
  - 128×128セル、8列×8行の本番配置台紙
- `art/spritesheet_layout.csv`
  - セル配置案
- `data/sprite_frame_requirements.csv`
  - 必須フレーム一覧と制作状況
- `docs/production_spritesheet_spec.md`
  - 本番スプライトの透明背景、固定セル、ピボットなどの制作条件

v2に入っていた簡易図形の `fighter_spritesheet_v1.png` と `fighter_spritesheet_v1_boxes_preview.png` は削除しました。

## デバッグ画面資料

- `docs/debug_screen_wireframe.png`
- `docs/debug_screen_mockup.png`
- `docs/debug_screen_spec.md`
- `data/debug_ui_fields.csv`

以前の参考画像右下にあった画面予想図に相当する内容を、上記4ファイルで補っています。

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
```

`Game`は空フォルダです。Unity Hubから、この場所へUnityプロジェクト本体を作成してください。

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
