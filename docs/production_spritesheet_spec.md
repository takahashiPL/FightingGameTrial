# 本番スプライトシート制作仕様

## 1. 目的

Unityからピクセル矩形またはUVで安全に切り出せる、固定セル方式の2D格闘キャラクタースプライトシートを作成する。

## 2. キャラクターデザイン

- 白い道着
- 黒帯
- 赤い鉢巻
- 赤いグローブ
- 黒髪
- `docs/reference_infographic.png` と `art/fighter_motion_reference_sheet.png` をデザイン参考にする
- 右向きを基準に描き、左向きはUnity側で反転する

## 3. 画像仕様

- PNG
- RGBA
- 透明背景
- 1セル: 128×128 px
- 全体: 1024×1024 px（8列×8行）
- 補間なし
- アンチエイリアスは抑える
- セル境界線やラベルは本番画像へ描き込まない

## 4. 原点

- キャラクター原点は両足の中央
- 各セルのピボットは原則 `(64, 112)`
- ジャンプ中も、地上基準位置を同じピボットへ合わせる
- 実際のキャラクターはピボットより上へ配置する

## 5. 必要フレーム

必要なフレームIDと配置先の**正本**は `data/sprite_frame_requirements.csv` とする。  
`art/spritesheet_layout.csv` は正本の展開表であり、正本と矛盾する場合は正本を優先して同期する。

`implementation_scope=initial`（Idle / StandPunch）でも、`asset_status=required` の間は本番素材未作成として扱う。  
`planned` のフレームは配置予約であり、実装済み・素材完成を意味しない。

最低限、次を用意する（制作計画。初期実装は Idle + StandPunch のプレースホルダで開始してよい）。

- 待機
- 前進・後退
- 立ちパンチ Startup / Active / Recovery
- 立ちキック Startup / Active / Recovery
- しゃがみ
- しゃがみパンチ
- しゃがみキック
- 垂直ジャンプ
- 前ジャンプ
- 後ろジャンプ
- 空中パンチ
- 空中キック
- 立ちガード
- しゃがみガード
- ジャストガード
- HitStun
- Knockdown
- Down
- WakeUp
- Stagger
- AirDeflected
- LandingStagger
- ClashRecoil

## 6. 判定箱

判定箱はスプライト画像へ焼き込まない。

- Pushbox / Hurtbox / Hitbox は `data/boxes.csv` から描画する
- 確認用プレビュー画像は別途生成する
- 本番スプライトと判定箱データを分離する

## 7. 検証条件

本番素材と判断する前に、次を確認する。

- すべてのセルが128×128
- 背景が完全に透明
- ピボットが統一されている
- 右向きで統一されている
- `frames.csv` の矩形と一致する
- 1フレーム送り時に足元が不自然に跳ねない
- 左右反転時にHitbox位置が正しく反転する
