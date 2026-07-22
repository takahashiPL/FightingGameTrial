# CHANGELOG

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
