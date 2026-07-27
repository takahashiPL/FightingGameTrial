# CHANGELOG

## v3.14（Unity段階14B完了・Participant共通KO状態・KO遷移）

- 段階14B完了を明記。KO正本は Participant.isKnockedOut。API: TryEnterKnockout / ClearKnockoutForReset / BuildLifeLabel
- 処理順 ReceiveHit→ApplyDamage→TryEnterKnockout→MarkHit→HitStop。最後の一撃の HitStop/Stun/KB は維持
- KO中は入力移動・新規攻撃禁止、KO済み防御者への追加Hit拒否（DefenderKO）。視覚優先 KO>HitStun>通常
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
