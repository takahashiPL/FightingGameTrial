# Unity 実装状況・次工程（段階1〜9到達後）

最終更新: 2026-07-23  
対象ブランチ: `unity`  
最新実装コミット: **`0b84a81`**（Add minimal punch hit detection）

このファイルは、Unity 側の**実装済み / 暫定 / 未実装 / 次回候補 / 正式方針**を混同せずに追うための正本です。  
ゲーム仕様そのものの正本は引き続き `docs/rules.md` です。

---

## 1. 区分の読み方

| 区分 | 意味 |
|---|---|
| **実装済み・確認済み** | FightDebugScene で動作確認済み |
| **暫定実装** | 動くが、正式仕様へ置き換える前提 |
| **正式方針（未実装）** | 今後そうする、と決めた設計。コード未反映 |
| **未実装** | まだ作っていない |
| **次回候補** | 工程上の次ステップ |

---

## 2. 段階1〜9の到達点（実装済み・確認済み）

| 段階 | 内容 | 状態 |
|---|---|---|
| 1 | 60Hz SimulationTick、Catch-up上限 | 実装済み |
| 2 | Pause / 1 SimulationTick Step | 実装済み |
| 3 | Debug HUD | 実装済み |
| 4 | HitStop（テスト用 H キー含む） | 実装済み |
| 5 | ActionFrame（テスト用 A / R） | 実装済み |
| 6 | 入力サンプリング（矢印・J Held → CurrentInput） | 実装済み |
| 7 | DebugPlayer 左右移動、Sprite 表示 | 実装済み |
| 8 | Jパンチ（Startup/Active/Recovery 見た目、立ち上がり入力） | 実装済み |
| 9 | DebugDummy、距離＋向きの暫定 Hit、1攻撃1Hit、Hit時6F HitStop | 実装済み |

### 2.1 確認済みの挙動（段階9まで）

- SimulationTick は HitStop 中も進む。CombatFrame / ActionFrame / 移動は止まる
- Pause 中は自動進行しない。`.` で 1 SimulationTick だけ進む
- Jパンチ: Startup 1〜3 / Active 4〜6 / Recovery 7〜12
- 近距離・正面で Hit → HitCount 増、HitStop 6
- 遠距離 Miss、背中側 Miss
- 左向き攻撃でも範囲内なら Hit
- 1攻撃1Hit。次のパンチでは再び Hit 可能
- H キーのテスト HitStop、A / R のデバッグ Action は維持

### 2.2 段階9の Hit 判定は暫定

現在の Hit は **正式な Box 判定ではない**。

暫定条件:

- `IsJPunchAttack`
- ActionFrame が Active（4〜6）
- その攻撃で未 Hit（`HasCurrentJPunchHit`）
- Facing 方向側に Dummy がいる
- `|PlayerX - DummyX|` 相当の向き込み距離 ≤ `attackRange`（初期 1.35）

段階9のコードは**削除対象ではない**。  
SimulationTick / ActionFrame / HitStop / 1攻撃1Hit の検証実績として残し、のちに Box 重なり判定へ**置換**する。

---

## 3. 設計見直し：Facing と移動入力の分離（正式方針・未実装）

### 3.1 現状の暫定挙動（実装済み）

`DebugFighterMotor` は、**Left 入力 → 左向き / Right 入力 → 右向き**という暫定挙動である。

操作確認で判明した問題:

- 前後移動だけで自由に方向転換できる
- 格闘ゲームとして、相手との向き合い関係と一致しない

これは**確認用の暫定**であり、正式仕様ではない。

### 3.2 正式方針（未実装）

1. **移動方向と Facing を分離する**
2. Player は基本的に相手と向き合う
3. `PlayerX < DummyX` なら Player は右向き
4. `PlayerX > DummyX` なら Player は左向き
5. 入力の Left/Right は**ワールド座標の移動方向だけ**を決める（Facing は変えない）
6. **前進／後退は、入力キーの左右そのものではなく、相手との位置関係で決める**
7. 地上移動で相手をすり抜けない
8. 左右入れ替わりは、将来のジャンプ・飛び越し等の**明示的な状況**でのみ発生させる
9. **自動振り向きと Push Box はセットで設計する**

#### 前進・後退の定義（引継ぎ用・明確化）

| 用語 | 意味 |
|---|---|
| **前進** | 相手へ近づく方向への移動 |
| **後退** | 相手から離れる方向への移動 |

つまり「Left キー＝常に後退」「Right キー＝常に前進」ではない。  
同じキーでも、Player が Dummy のどちら側にいるかで前進／後退が入れ替わる。

具体例:

| 位置関係 | Facing（正式） | Right 入力 | Left 入力 |
|---|---|---|---|
| Player が Dummy の**左側**（`PlayerX < DummyX`） | 右向き | **前進**（相手へ近づく） | **後退**（相手から離れる） |
| Player が Dummy の**右側**（`PlayerX > DummyX`） | 左向き | **後退**（相手から離れる） | **前進**（相手へ近づく） |

補足（誤解防止）:

- 左側で Left を押しても左向きにはならない（Facing は相手向き合いのまま）
- 右側で Right を押しても右向きにはならない
- ワールド移動量はキーの Left/Right が決め、前進／後退という戦闘用語は位置関係から解釈する

対応する仕様記述は `docs/rules.md` §2.3（入力解釈用 Facing / 最終 Facing）および Pushbox 関連節。  
Unity デバッグ実装は、段階10でこの正式方針へ寄せる。

---

## 4. 判定箱方針（正式・ほぼ未実装）

| 箱 | 役割 | 現状 |
|---|---|---|
| **Push Box** | 重なり防止・地上すり抜け防止・向き合いとセット | **未実装** |
| **Hurt Box** | 被弾判定 | **未実装**（CSVサンプルのみ） |
| **Hit Box** | 攻撃判定（Active のみ有効） | **未実装**（CSVサンプルのみ） |

今後の置換方針:

- Facing による左右反転を箱に反映する
- Game ビュー上で Box を可視化する
- 段階9の距離判定を **Box 同士の重なり判定**へ置き換える

自動振り向き・Push Box・Box 可視化・Box 判定はいずれも**現時点では未実装**。

---

## 5. 推奨工程順（見直し後）

次工程は、単なる被 Hit 演出や HP 追加ではない。

| 段階 | 内容 | 区分 |
|---|---|---|
| **10** | Facing と移動入力の分離、自動向き合い、前進/後退の定義、Push Box 定義・可視化、地上すり抜け防止 | 次回候補 |
| **11** | Hurt Box / Punch Hit Box、Active のみ Hit Box 有効、Facing 反転、Box 可視化、距離判定→Box 重なりへ置換 | 次回以降 |
| **12** | 被 Hit 状態、HitStun、被 Hit 表示 | 未実装 |
| **13** | ノックバック、押し戻し、ステージ端 | 未実装 |
| **14** | HP、Damage、KO | 未実装 |
| **15** | 攻撃データ化（Startup/Active/Recovery、Hit Box、Damage、HitStop、HitStun、Knockback） | 未実装 |

その後の候補（順不同・未着手）:

- しゃがみ、ジャンプ、空中状態
- 複数攻撃、入力バッファ、キャンセル
- ガード、コンボ
- Animation 本接続
- 2P / CPU、ラウンド進行

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

- 段階1〜9まで到達。最新コミット `0b84a81`
- 段階9の Hit は**距離＋向きの暫定**。Box 判定ではない
- 入力で Facing を変える挙動は**暫定**。正式は相手向き合い＋移動入力分離
- 次は段階10（Facing分離・Push Box・すり抜け防止）を優先
