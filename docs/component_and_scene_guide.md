# Unity FightingGameTrial Component and Scene Guide

最終更新: 2026-07-27
対象ブランチ: `unity`
対象コミット（資料作成時点）: **`de85e52`**（Document J Punch attack data completion）
対象 Scene: `Game/Assets/Scenes/FightDebugScene.unity`
実装到達点: **Stage 15（J Punch 攻撃データ化）完了後**

---

## 0. この資料の目的

### 何を理解できる資料か

Unity の **Scene / GameObject / Component / MonoBehaviour / Inspector 参照** と、
本プロジェクト（FightingGameTrial）の **コード責務・実行経路** を結びつけて説明します。
題材は現在の `FightDebugScene` です。

読了後に次が追えることを目指します。

- Hierarchy 上のどの GameObject に、どの Component が付いているか
- Inspector の参照が、どの C# フィールドと対応するか
- Play 後に「誰の `Update` が何を始め、誰が SimulationTick を進めるか」
- J キーを押してから Hit / HitStop / HUD 更新までの担当クラス

### 実装仕様書との違い

| 資料 | 役割 |
|---|---|
| `docs/unity_implementation_status.md` | 段階ごとの実装済み／未実装／検証結果 |
| `docs/rules.md` | ゲーム仕様の正本（60Hz、HitStop など） |
| `CHANGELOG.md` | 版ごとの変更履歴 |
| **この資料** | Unity 再学習用の Scene・Component 教材 |

到達点の一覧や「次に何を実装するか」は、この資料の主目的ではありません。

### 読む前提

- Unity 5.4 / 5.5 頃の経験がある（現在の Unity を再学習中）
- C# は読める
- このプロジェクトを初めて読む第三者でもよい

### Stage 15 時点の資料であること

数値・責務・色優先・攻撃データの正本は **Stage 15 完了後の現在実装** を正とします。
過去段階の暫定（距離判定、Session 内 const、KO > HitStun 色優先など）は、現在仕様として書かないか、「履歴」と明記します。

### 正本の区分

| 区分 | 正本の置き場 |
|---|---|
| ゲーム仕様（時間・判定の意味） | `docs/rules.md` |
| Unity 実装の到達点 | `docs/unity_implementation_status.md` |
| J Punch 攻撃の設定値 | `DebugAttackData.JPunch`（コード） |
| 攻撃の進行状態 | 各 `DebugFighterParticipant.AttackState` |
| 被 Hit / HitStun / KB 速度 | 各 `DebugFighterParticipant.HitState` |
| HP / KO | 各 `DebugFighterParticipant` |
| 共有時間（Tick / Combat / Pause / HitStop） | `SimulationSession` 内の `SimulationTimeState` |
| Scene 上の接続 | `FightDebugScene.unity` の Inspector 参照 |

この資料で Scene YAML やコードから確認できなかったことは **「未確認」** と書きます。推測で埋めません。

---

## 1. Unity の基本構造と本プロジェクトの対応

各用語を **Unity一般** と **本プロジェクト（FightingGameTrial）** で並べます。

### Project

| | |
|---|---|
| **Unity一般** | 1つのゲーム／アプリ単位。Assets・Packages・ProjectSettings を含む。 |
| **本プロジェクト** | リポジトリ直下の `Game/` が Unity プロジェクト。Git 管理は主に `Game/Assets`・`Packages`・`ProjectSettings`。 |

### Asset

| | |
|---|---|
| **Unity一般** | Project 内のファイル（Scene、Script、Sprite、Font など）。 |
| **本プロジェクト** | 例: `Game/Assets/Scenes/FightDebugScene.unity`、`Game/Assets/Scripts/...`、デバッグ用透過 PNG。 |

### Scene

| | |
|---|---|
| **Unity一般** | 実行時に読み込む世界の保存単位。GameObject の Hierarchy を持つ。 |
| **本プロジェクト** | 学習・検証の主 Scene は **`FightDebugScene`**。他に `SampleScene`・URP テンプレート Scene もあるが、本資料の題材ではない。 |

### GameObject

| | |
|---|---|
| **Unity一般** | **入れ物**。名前と Transform を持ち、Component を載せる。 |
| **本プロジェクト** | 例: `DebugPlayer`（P1）、`DebugDummy`（P2）、`SimulationSession`、`DebugCanvas`。名前が「P1」「P2」ではない点に注意。 |

### Component

| | |
|---|---|
| **Unity一般** | GameObject に機能を追加する部品。1つの GO に複数付けられる。 |
| **本プロジェクト** | 例: 同じ `DebugPlayer` に `SpriteRenderer` + `DebugFighterMotor` + `DebugFighterVisual` + `DebugFighterParticipant`。 |

### Transform

| | |
|---|---|
| **Unity一般** | **すべての GameObject に必ずある**。位置・回転・スケール・親子関係。 |
| **本プロジェクト** | 論理位置の正本は Motor の `LogicalX`（Session 経由）。Transform は表示と初期配置にも使う。UI は `RectTransform`。 |

### MonoBehaviour

| | |
|---|---|
| **Unity一般** | `MonoBehaviour` を継承した C# クラスは、GameObject に Add Component できる。Unity が `Awake` / `Update` などを呼ぶ。 |
| **本プロジェクト** | `SimulationClockDriver`、`SimulationSession`、`DebugFighterParticipant` などが該当。**`DebugAttackData` は MonoBehaviour ではない**（GameObject に付けない）。 |

### Inspector

| | |
|---|---|
| **Unity一般** | 選択中 GameObject の Component とフィールドを編集するウィンドウ。 |
| **本プロジェクト** | Session・Participant・HUD などの参照をここで接続する。**自動検索（Find）はしない**方針。 |

### SerializeField

| | |
|---|---|
| **Unity一般** | `private` でも Inspector に出す属性。`public` にしなくてもよい。 |
| **本プロジェクト** | ほぼすべての Inspector 接続が `[SerializeField]`。Tooltip / Header 付き。 |

### 参照のドラッグ＆ドロップ

| | |
|---|---|
| **Unity一般** | Inspector のオブジェクト欄へ、Hierarchy の GO や Component を落とす。実行時検索とは別。 |
| **本プロジェクト** | 例: `SimulationSession.participantP1` ← `DebugPlayer` の Participant。欠けていると `Awake` で `Debug.LogError`。 |

### Awake

| | |
|---|---|
| **Unity一般** | 有効化直後に Unity が呼ぶ。同 Scene 内の呼び出し順は保証されない（Script Execution Order で調整可）。 |
| **本プロジェクト** | 参照 null チェック、入力 Neutral の準備、Box 原点、HP 初期化などに使う。**戦闘の 60Hz 進行はしない**。 |

### Update

| | |
|---|---|
| **Unity一般** | 描画フレームごとに Unity が呼ぶ。実時間ベース。 |
| **本プロジェクト** | 入力サンプリング（物理キー）と `SimulationClockDriver` の時間蓄積に使う。**格闘ロジック本体を各 Fighter の Update に分散していない**。 |

### Unity が呼ぶ関数 / 通常の C# クラス

| | |
|---|---|
| **Unity一般** | `Awake`/`Update` 等は Unity が呼ぶ。普通のクラスは自分で `new` するか static で使う。 |
| **本プロジェクト** | `DebugAttackData`・`DebugPunchHitResolver`（static）・`DebugFighterPushResolver`（static）・`DebugFighterHitState`（Serializable 入れ物）は、Scene 上の「独立 Component」ではない。 |

**要点（本プロジェクト）**

- GameObject は入れ物、Component が機能
- C# でも `MonoBehaviour` なら Component になる
- `DebugAttackData` は付けない（データ型）
- Inspector 参照 ≠ `GetComponent` / `FindObjectOfType` の実行時検索
- Transform は全 GO にある
- Simulation 進行は **ClockDriver → Session** に集約（各 Component の Update に戦闘をバラさない）

---

## 2. FightDebugScene 全体像

Scene ファイル `FightDebugScene.unity` から確認した Hierarchy です（GameObject 名は YAML の `m_Name`）。

```text
FightDebugScene
├─ SimulationRoot
│  ├─ SimulationClockDriver
│  ├─ SimulationSession
│  ├─ DebugPlaybackInput
│  └─ DebugGameplayInput
├─ FightActors
│  ├─ DebugPlayer          ← P1（slotId = P1）
│  └─ DebugDummy           ← P2（slotId = P2）
├─ DebugCanvas
│  ├─ DebugHudBackground
│  └─ DebugHudText
├─ EventSystem
└─ Main Camera
```

**注意**

- Hierarchy 上に `P1` / `P2` という名前の GameObject はない
- 操作説明用の `DebugHudHelpText` は **Scene に存在しない**（Play 時に `DebugHudView` が生成）
- `SampleScene` は本資料の対象外

### 主要 GameObject 一覧

| GameObject | 主な役割 | 主な Component | 他 Object との接続（Scene 確認済み） |
|---|---|---|---|
| **SimulationRoot** | Simulation 系の親 | Transform | 子に Driver / Session / 入力 |
| **SimulationClockDriver** | 実時間→60Hz tick 進行の入口 | Transform, `SimulationClockDriver` | → Session, → PlaybackInput |
| **SimulationSession** | 1 SimulationTick の戦闘処理 | Transform, `SimulationSession` | → GameplayInput, → DebugPlayer の Participant, → DebugDummy の Participant |
| **DebugPlaybackInput** | Space / . / H / A / R の押下エッジ | Transform, `DebugPlaybackInput` | SerializeField なし。ClockDriver が読む |
| **DebugGameplayInput** | 矢印 / J の物理キー保持 | Transform, `DebugGameplayInput` | SerializeField なし。Session が読む |
| **FightActors** | 戦闘アクターの親 | Transform | 子に DebugPlayer / DebugDummy |
| **DebugPlayer** | P1 参加枠の見た目とロジック入れ物 | Transform, SpriteRenderer, Motor, Visual, Participant | Participant.opponent → DebugDummy の Participant |
| **DebugDummy** | P2 参加枠（棒立ち） | 同上 | Participant.opponent → DebugPlayer の Participant |
| **DebugCanvas** | デバッグ HUD の Canvas | RectTransform, Canvas, CanvasScaler, GraphicRaycaster, `DebugHudView` | HudView → Session, → DebugHudText |
| **DebugHudBackground** | HUD 背景パネル | RectTransform, CanvasRenderer, Image | Canvas の子 |
| **DebugHudText** | 状態表示テキスト | RectTransform, CanvasRenderer, TextMeshProUGUI | HudView.hudText に接続 |
| **EventSystem** | UI 入力システム | Transform, EventSystem, InputSystemUIInputModule | （戦闘ロジックとは非接続） |
| **Main Camera** | 表示カメラ | Transform, Camera, AudioListener, URP AdditionalCameraData | 位置 (0,0,-10)、Orthographic |

### Hierarchy の親子関係と C# 参照は別物

| | Hierarchy（親子） | C# / Inspector 参照 |
|---|---|---|
| **何か** | Transform の親子。ローカル座標や描画階層に影響 | フィールドが指すオブジェクト。処理の呼び出し先 |
| **例** | `DebugPlayer` は `FightActors` の子 | `SimulationSession.participantP1` が `DebugPlayer` の Participant を指す |
| **誤解しやすい点** | 同じ親の下にいても、参照は自動では繋がらない | 親子でなくても、Inspector で落とせば繋がる |

---

## 3. Scene 上の主要 Component 一覧

Scene に実在するものと、コードのみのものを分けます。

### 3.1 Scene に付いている主要 Component

| Component | 付いている GameObject | MonoBehaviour か | 主責務 | Inspector 参照 | 毎 Frame 処理 |
|---|---|---|---|---|---|
| `SimulationClockDriver` | SimulationClockDriver | はい | Pause/Step 消化、時間蓄積、Session に tick 依頼 | Session, PlaybackInput | `Update` あり |
| `SimulationSession` | SimulationSession | はい | 1 SimulationTick の戦闘処理 | GameplayInput, P1/P2 Participant | `Update` なし（呼ばれて進む） |
| `DebugPlaybackInput` | DebugPlaybackInput | はい | デバッグキーのエッジ検出 | なし | `Update` あり（ExecutionOrder -100） |
| `DebugGameplayInput` | DebugGameplayInput | はい | 矢印/J の物理状態保持 | なし | `Update` あり（ExecutionOrder -90） |
| `DebugHudView` | DebugCanvas | はい | HUD 文字列の組み立て・表示 | Session, hudText | `Update` あり |
| `DebugFighterParticipant` | DebugPlayer / DebugDummy | はい | 参加枠・HP/KO・Box 変換・状態所有 | Motor, Visual, SpriteRenderer, opponent 等 | `Update` なし |
| `DebugFighterMotor` | DebugPlayer / DebugDummy | はい | LogicalX・Facing・移動・Clamp | SpriteRenderer 等 | `Update` なし |
| `DebugFighterVisual` | DebugPlayer / DebugDummy | はい | Idle/Attack Sprite 切替 | SpriteRenderer, Sprites | `Update` なし |
| `SpriteRenderer` | DebugPlayer / DebugDummy | ビルトイン | スプライト描画・色 | （Unity） | Unity が描画 |
| `Camera` | Main Camera | ビルトイン | 画面描画 | （Unity） | Unity が描画 |
| `Canvas` / `CanvasScaler` / `GraphicRaycaster` | DebugCanvas | ビルトイン/UI | HUD Canvas | （Unity） | UI 更新 |
| `TextMeshProUGUI` | DebugHudText | TMP | 状態テキスト表示 | Font Asset 等 | HudView が text を書換 |
| `Image` | DebugHudBackground | UI | 背景 | 色等 | — |
| `EventSystem` / `InputSystemUIInputModule` | EventSystem | UI/Input | UI イベント | — | UI 用 |

### 3.2 Scene に Component として存在しないもの（重要）

| 型 | 実体 | 備考 |
|---|---|---|
| `DebugAttackData` | 通常の sealed クラス | `static readonly JPunch`。Add Component 不可 |
| `DebugPunchHitResolver` | static クラス | Session から静的呼び出し |
| `DebugFighterPushResolver` | static クラス | Session から静的呼び出し |
| `DebugFighterHitState` | `[Serializable]` 入れ物 | Participant のフィールド。独立 Component ではない |
| `DebugFighterAttackState` | `[Serializable]` 入れ物 | 同上 |
| `DebugFighterBoxView` | MonoBehaviour | Scene 未配置。Participant.Awake で `AddComponent` |
| `SimulationTimeState` | `[Serializable]` | Session の SerializeField |
| `SimulationInputState` | 通常クラス | Session が保持・生成 |

---

## 4. Inspector 接続ガイド

コードの `[SerializeField]` と、Scene YAML で確認できた値を併記します。
Scene にフィールドが出ていない項目は、**スクリプト既定値が実行時に使われる**（Scene 再保存前）と注記します。

### SimulationClockDriver

| Inspector 項目 | 型 | 接続先または値（Scene） | 必須性 | 何に使うか |
|---|---|---|---|---|
| Simulation Session | `SimulationSession` | GO `SimulationSession` | 必須（null で Awake Error） | tick 進行の依頼先 |
| Debug Playback Input | `DebugPlaybackInput` | GO `DebugPlaybackInput` | 必須（null で Awake Error） | Pause/Step/H/A/R |
| Seconds Per Simulation Tick | float | `0.016666668`（≈1/60） | 推奨 | 60Hz 蓄積 |
| Max Catch Up Ticks Per Update | int | `5` | 推奨 | 追いつき上限 |
| Test Hit Stop Frames | int | `6` | 任意 | H キー用テスト HitStop |

### SimulationSession

| Inspector 項目 | 型 | 接続先または値（Scene） | 必須性 | 何に使うか |
|---|---|---|---|---|
| Debug Gameplay Input | `DebugGameplayInput` | GO `DebugGameplayInput` | 必須（Awake Error） | P1 物理入力の取得元 |
| Participant P1 | `DebugFighterParticipant` | `DebugPlayer` | 必須（Awake Error） | P1 |
| Participant P2 | `DebugFighterParticipant` | `DebugDummy` | 必須（Awake Error） | P2 |
| Time State | `SimulationTimeState` | ネスト（初期値） | Session 内 | Tick/Combat/Pause/HitStop |
| Console Log Interval Ticks | int | `60` | 任意 | 間欠ログ |

**注（Scene YAML）**: ファイル上に古い `attackRange: 1.35` が残っている。
**現在の `SimulationSession.cs` に同名フィールドはなく、距離判定も使わない**（Stage 11B 以降）。Inspector／YAML の残骸であり、攻撃判定の正本ではない。

### DebugHudView（DebugCanvas 上）

| Inspector 項目 | 型 | 接続先または値（Scene） | 必須性 | 何に使うか |
|---|---|---|---|---|
| Simulation Session | `SimulationSession` | GO `SimulationSession` | 必須（Awake Error） | 状態の読み取り |
| Hud Text | `TextMeshProUGUI` | `DebugHudText` | 必須（Awake Error） | 状態 HUD 本文 |

Help 用 TMP は Inspector 項目なし（Awake で生成）。

### DebugFighterParticipant（P1 = DebugPlayer / P2 = DebugDummy）

| Inspector 項目 | 型 | P1（DebugPlayer） | P2（DebugDummy） | 必須性 | 何に使うか |
|---|---|---|---|---|---|
| Slot Id | enum | **P1 (0)** | **P2 (1)** | 必須 | 参加枠 ID（キャラ種類ではない） |
| Motor | Motor | 自分の Motor | 自分の Motor | 必須（Awake Error） | 移動・Facing |
| Visual | Visual | 自分の Visual | 自分の Visual | 必須（Awake Error） | Sprite 切替 |
| Sprite Renderer | SpriteRenderer | 自分 | 自分 | 必須（Awake Error） | 色・描画 |
| Opponent | Participant | **DebugDummy** | **DebugPlayer** | 必須（Awake Error） | Facing 用の相手 |
| Display Tint | Color | 白 | 水色系 (0.65,0.78,0.95) | 推奨 | 通常時の色 |
| Uses Gameplay Input | bool | **true** | **false** | 重要 | true=Gameplay / false=Neutral |

**Scene YAML に出ていないがコードにある SerializeField（スクリプト既定値が使われる）**

| 項目 | コード既定 | Stage 15 での扱い |
|---|---|---|
| Hit Stun Frames | 12 | **互換用。J Punch 適用値の正本は `DebugAttackData.JPunch`** |
| Knockback Initial Speed | 0.18 | 同上（互換用） |
| Knockback Deceleration | 0.015 | 同上（互換用） |
| Max Hit Points | 100 | HP 最大の暫定正本（攻撃データではない） |
| Push Box Half Width | 0.5 | Push 用 |
| Box Origin / Boxes / AttackState / HitState 等 | コード既定 | AttackState/HitState は実行時状態の入れ物 |

HitStun / KO 表示色もコード側 SerializeField。Scene YAML には未保存（未確認: Editor で開いたときにデフォルト表示されるか）。

### DebugFighterMotor（両体とも Scene 値は同一）

| Inspector 項目 | 型 | Scene 値 | 何に使うか |
|---|---|---|---|
| Move Units Per Combat Frame | float | `0.05` | 1 CombatFrame の移動量 |
| Min X / Max X | float | `-7` / `7` | ステージ端 Clamp |
| Sprite Renderer | SpriteRenderer | 自分 | flipX 等 |
| Faces Right By Default | bool | true | 初期 Facing |

### DebugFighterVisual（両体とも Scene 値は同一）

| Inspector 項目 | 型 | Scene 値 | 何に使うか |
|---|---|---|---|
| Sprite Renderer | SpriteRenderer | 自分 | 表示先 |
| Idle Sprite | Sprite | fighter_idle_00_transparent | Idle |
| Attack Sprite | Sprite | fighter_attack_punch_transparent | 攻撃ポーズ |
| Attack Pose End Frame | int | `6` | 攻撃 Sprite の終了 AF |
| Action End Frame | int | `12` | 見た目用の最終 AF（攻撃終了の正本は Stage 15 で攻撃データの TotalFrames） |

### DebugFighterHitState について

**独立した Scene Component ではない。**
Participant の `[SerializeField] hitState` として埋め込まれる Serializable クラス。
「HitState を Hierarchy から選ぶ」操作はない。

---

## 5. MonoBehaviour と通常 C# クラスの違い

| 観点 | MonoBehaviour（例: Participant） | 通常 C# クラス（例: `DebugAttackData`） |
|---|---|---|
| GameObject へ Add Component | できる | **できない** |
| Inspector に単体で並ぶ | する | **しない**（フィールドの型としては出る場合あり） |
| Unity が Awake/Update を呼ぶ | 呼ぶ | **呼ばない** |
| `new` | 通常は Add Component / Scene 配置 | `CreateJPunch()` や `static readonly` |
| Scene 保存対象 | Component として保存 | Scene に載らない |
| データ正本として使う理由 | 実行時の「誰の状態か」を持つ | 技の**設定値**を1か所に固定する |
| `JPunch` が static readonly な理由 | — | 毎 Frame/毎 Hit で `new` せず、正本を1つにするため |
| ScriptableObject にしていない理由 | — | Stage 15 は参照経路の整理が目的。Inspector/Asset 依存を増やさない |

static な `DebugPunchHitResolver` / `DebugFighterPushResolver` も「計算関数の置き場」であり、Scene に置かない。

---

## 6. 実行開始時の流れ

現在コードに基づく順序です。Awake 同士の厳密な順序は Unity が保証しないため、**本プロジェクトは Inspector 参照が揃っている前提**で動きます（欠けていれば各 Awake が Error）。

1. **Scene ロード** … `FightDebugScene` の GameObject / Component が生成される
2. **各 MonoBehaviour の Awake**（順不同になり得る）
   - ClockDriver / Session / HudView / Participant / Motor / Visual などが参照チェック
   - Participant: AttackState/HitState 準備、HP 全快、BoxView を `AddComponent`、色適用
   - `DebugGameplayInput` / `DebugPlaybackInput`: ExecutionOrder で早め（-90 / -100）
3. **Start（Session）** … 初期 Push（ログ控えめ）→ 初期 Facing → Visual 更新
4. **毎描画フレームの Update**
   - PlaybackInput / GameplayInput: キー状態更新
   - ClockDriver: Pause/Step/H/A/R 消化 → 時間蓄積 → 必要なら `ProcessOneSimulationTick`
   - HudView: 状態文字列を TMP へ書く
5. **SimulationTick**（Session）… 入力サンプル →（HitStop でなければ）Combat 処理
6. **Motor / Visual** … Session から呼ばれて位置・Sprite を更新（自身の Update ではない）
7. **HUD** … Session の公開状態を読んで表示（戦闘の正本ではない）

**HitStop 中でも** Unity の `Update` 自体は止まりません。止まるのは Session 内の Combat 進行です。

---

## 7. 1回の J Punch が通る経路

### 7.1 呼び出しの矢印図（概略）

```text
Keyboard (J)
  → DebugGameplayInput.Update（物理 Held）
  → SimulationClockDriver.Update（時間蓄積）
  → SimulationSession.ProcessOneSimulationTick
       → SampleCurrentInput / SampleAttackInput（押した瞬間）
       → TryStartJPunch（AttackState 開始）
       → …移動 / Push / Facing…
       → AdvanceActionFrame（Startup→Active→Recovery）
       → EvaluateWorldHitBox（DebugAttackData.JPunch の local + Facing）
       → DebugPunchHitResolver.TryResolveHit（重なり・1攻撃1Hit）
       → ReceiveHit / ApplyDamage / TryEnterKnockout / MarkHit
       → HitStopRemaining 設定
       → Visual / TryEndJPunch / HitStun 消費
  → DebugHudView.Update / Console ログ
  → SpriteRenderer（色・Sprite）
```

### 7.2 段階と担当クラス

| # | 段階 | 主担当 |
|---|---|---|
| 1 | J 入力（物理） | `DebugGameplayInput` |
| 2 | 押した瞬間の検出 | `DebugFighterAttackState.SampleAttackInput`（Session 経由） |
| 3 | 攻撃開始 | `SimulationSession.TryStartJPunchForParticipant` → `AttackState.StartJPunch` |
| 4 | Startup（AF 1〜3） | `AttackState.ActionFrame` + `DebugAttackData` 境界 |
| 5 | Active（AF 4〜6） | 同上。Hit Box `IsActive` |
| 6 | local Hit Box | **正本** `DebugAttackData.JPunch` |
| 7 | Facing 反転と world 変換 | `DebugFighterParticipant.EvaluateWorldHitBox` |
| 8 | Hurt Box との重なり | `DebugPunchHitResolver` + defender `EvaluateWorldHurtBox` |
| 9 | 1攻撃1Hit ガード | `AttackState.HasCurrentJPunchHit` / `MarkHit` |
| 10 | Damage | Session → `ApplyDamage`（値は攻撃データ） |
| 11 | KO 判定 | `TryEnterKnockout`（処理順は ReceiveHit の後） |
| 12 | HitStop | `SimulationTimeState.HitStopRemaining` |
| 13 | HitStun | `HitState`（値は攻撃データから ReceiveHit） |
| 14 | Knockback | Session が移動・減速（減速値も攻撃データ） |
| 15 | Recovery（AF 7〜12） | ActionFrame 進行 |
| 16 | 攻撃終了 | `ActionFrame >= TotalFrames` → `EndJPunch` |
| 17 | HUD / Log | `DebugHudView` / `Debug.Log` |

処理順の詳細は `SimulationSession.ProcessOneSimulationTick` のコメント番号が正本です。

---

## 8. P1 と P2 が同じ構成で動く理由

| 事実 | 説明 |
|---|---|
| Participant は参加枠 | キャラクター種類の選択ではない（`slotId` が P1/P2） |
| 同一 Component 構成 | DebugPlayer / DebugDummy とも Motor + Visual + Participant + SpriteRenderer |
| 入力の差だけ | P1: `usesGameplayInput = true`、P2: `false` → Session が Neutral を渡す |
| Dummy 専用分岐ではない | P2 用の別戦闘ロジッククラスはない |
| opponent 相互参照 | Scene で交差接続 |
| Facing / Push | 両者に同じ Session 経路が適用される |

P2 が動かないのは「Dummy 専用コード」ではなく、**入力が常に false** だからです。

---

## 9. 表示と戦闘状態の違い

| 種類 | 正本 | 表示との関係 |
|---|---|---|
| HP / KO / HitStun 残り | Participant / HitState | HUD や色は**結果の見え方** |
| SpriteRenderer.color | `ApplyDisplayColor` | 戦闘判定の入力ではない |
| KO 正本 | `isKnockedOut` | 色が暗いから KO、ではない |

**現在の色優先（Stage 15 回帰後）**

1. HitStun 中（`hitState.IsInHitStun`）→ 赤系
2. KO → 暗色
3. それ以外 → 通常 Tint

最後の一撃では、KO 状態は Hit 成立時点で立つが、**赤表示が終わってから**暗色へ移る。
表示だけを変えても Damage / HitStop は変わらない（`ApplyDisplayColor` は色設定のみ）。

---

## 10. HUD の構成

| 要素 | 確認結果 |
|---|---|
| Canvas | Scene の `DebugCanvas`（Screen Space Overlay） |
| DebugHudView | `DebugCanvas` 上の MonoBehaviour |
| 状態テキスト | Scene の `DebugHudText`（TextMeshProUGUI） |
| 操作説明 | **ランタイム生成** `DebugHudHelpText`（Scene に無し） |
| JPunch Data 行 | `BuildStatusHudText` が `DebugAttackData.JPunch` から生成 |
| HelpBlockHeightPixels | コード定数 `92`（旧 120 から縮小） |
| overflowMode | 状態側 `Truncate` |

**Stage 15 で起きた表示切れ**

- 原因: 行数が増え、Truncate + 状態領域高さで末尾行が切れた
- 対応: `JPunch Data` を `P2 KB Vx/Act` 直後へ移動し、Help ブロック高さをコードで調整
- **Scene / Prefab は変更していない**（`EnsureSplitHudLayout` のランタイム設定）

HUD の数字は便利な鏡です。**戦闘データの正本は Session / Participant / AttackData 側**です。

---

## 11. よくある誤解

1. **MonoBehaviour ごとに戦闘用 Update を書く必要はない**（本プロジェクトは ClockDriver + Session 集約）
2. **Inspector に見える値が必ず正本とは限らない**（HitStun/KB の旧フィールド、Scene の残骸 `attackRange`）
3. **public にしなくても SerializeField で表示できる**
4. **Hierarchy の親子だけでは参照は繋がらない**
5. **GetComponent と Inspector 参照は同じではない**（本プロジェクトは後者を正とする）
6. **Sprite 色が KO 判定ではない**
7. **Active が 3 Frame でも 3 Hit しない**（1攻撃1Hit）
8. **HitStop 中も Unity の Update は止まらない**（Combat だけ止まる）
9. **SimulationTick / CombatFrame / ActionFrame は別物**
10. **`DebugAttackData` は Component ではない**
11. **`static readonly` は Inspector 編集用ではない**

---

## 12. Unity Editor で確認する実習

いずれも **確認作業**です。値を試す場合は **保存せず**、Play を止めて元に戻してください。

### 実習1: P1 の Component と参照

1. `Game/Assets/Scenes/FightDebugScene.unity` を開く
2. Hierarchy で `FightActors` → `DebugPlayer` を選択
3. Inspector で Motor / Visual / Participant / SpriteRenderer を確認
4. Participant の Opponent が `DebugDummy` の Participant を指すか確認
5. Slot Id が P1、Uses Gameplay Input がオンか確認

### 実習2: P2 との差

1. `DebugDummy` を選択
2. Uses Gameplay Input が **オフ**であることを確認
3. Slot Id が P2、Opponent が DebugPlayer 側であることを確認
4. Component 種類は P1 と同じ並びであることを確認

### 実習3: Simulation 系の接続

1. `SimulationClockDriver` を選択 → Session / PlaybackInput 参照
2. `SimulationSession` を選択 → GameplayInput / Participant P1=DebugPlayer / P2=DebugDummy
3. `DebugCanvas` の `DebugHudView` → Session と Hud Text

### 実習4: Play Mode で時間と攻撃

1. Play
2. HUD の Tick/Combat/AF、JPunch Data を見る
3. J を押す → Punch Phase が Startup → Active → Recovery
4. Hit 時、SimulationTick と CombatFrame の差で HitStop を観察
5. 終わったら Play を停止（Scene を保存しない）

### 実習5: コードと Inspector の対応

1. Project で `DebugFighterParticipant.cs` を開く
2. `[SerializeField] private bool usesGameplayInput` を探す
3. Inspector の同名項目と対応づける
4. `DebugAttackData.cs` を開き、Inspector に出てこないことを確認する

---

## 13. コードを読む推奨順

パスは `Game/Assets/Scripts/` 以下です。

| 順 | ファイル | 先に見るもの | 主要 Method | 後回しでよい部分 |
|---|---|---|---|---|
| 1 | `Combat/DebugAttackData.cs` | `JPunch` のプロパティ一覧 | `CreateJPunch`, Frame 判定ヘルパ | validation 詳細 |
| 2 | `Input/DebugGameplayInput.cs` | 公開 Is* プロパティ | `Update` | — |
| 3 | `Debug/DebugPlaybackInput.cs` | Consume* | `Update` | — |
| 4 | `Simulation/SimulationTimeState.cs` | Tick/Combat/Pause/HitStop フィールド | Reset | — |
| 5 | `Simulation/SimulationClockDriver.cs` | SerializeField | `Update`, `AdvanceByAccumulatedTime` | テスト HitStop 細部 |
| 6 | `Simulation/SimulationSession.cs` | クラス先頭の処理順コメント | `ProcessOneSimulationTick`, `TryResolveJPunchHit` | ログ整形 |
| 7 | `Fighter/DebugFighterParticipant.cs` | 所有物（HP/KO/AttackState/HitState） | `EvaluateWorldHitBox`, `ReceiveHit`, `ApplyDisplayColor` | Box 原点計算の細部 |
| 8 | `Fighter/DebugFighterMotor.cs` | LogicalX, Facing | `SetLogicalX`, `TryMoveLogicalXBy` | — |
| 9 | `Fighter/DebugFighterHitState.cs` | HitStun / KB 速度 | `BeginHitStun`, Tick* | — |
| 10 | `Fighter/DebugFighterAttackState.cs` | ActionFrame, HasCurrentJPunchHit | Start/End/MarkHit/Sample | — |
| 11 | `Combat/DebugPunchHitResolver.cs` | TryResolveHit | 純関数本体 | — |
| 12 | `Fighter/DebugFighterPushResolver.cs` | 静的 Resolve | 再配分 | — |
| 13 | `Fighter/DebugFighterVisual.cs` | Apply | Sprite 切替 | — |
| 14 | `Debug/DebugHudView.cs` | BuildStatusHudText | `Update`, `EnsureSplitHudLayout` | レイアウト数値 |

---

## 14. 現在未確認・未実装

### 資料作成時に Scene / コードから確認しきれなかった項目

- Editor を開いた瞬間の Inspector 表示（YAML に無い Participant 追加フィールドがどう折りたたまれるか）のスクリーンショット相当の見た目
- URP `UniversalAdditionalCameraData` のパッケージ内 `.meta` パス（フィールド形状からの同定）
- GraphicRaycaster / EventSystem のパッケージ guid と `.meta` の対応ファイルパス
- Play Mode 中の `DebugHudHelpText` の Rect 最終値（コード定数からの計算結果は記載済み。実機ピクセルは環境依存）
- `SampleScene` / URP テンプレート Scene の詳細（本資料の対象外）

### ゲーム機能として未実装（Stage 15 時点・実装状況 Docs と一致）

- Round 終了・勝敗・HP バー・Guard
- 攻撃データの ScriptableObject 化 / Inspector 編集
- 複数攻撃・コンボ・Cancel・Counter Hit
- 2P 実操作（現在 P2 は Neutral）
- 壁バウンド・壁やられ など

詳細は `docs/unity_implementation_status.md`。

---

## 15. 関連資料

| 資料 | 役割 | この教材との違い |
|---|---|---|
| [README.md](../README.md) | 入口・要約・読み順 | 全体の扉 |
| [docs/learning_and_readability.md](learning_and_readability.md) | 可読性・学習方針 | 「どう書くか」 |
| [docs/component_and_scene_guide.md](component_and_scene_guide.md) | **Scene/Component 教材（本ファイル）** | Unity 構造と実行経路 |
| [docs/unity_implementation_status.md](unity_implementation_status.md) | 段階到達点・次工程 | 実装状況の正本 |
| [docs/rules.md](rules.md) | ゲーム仕様 | 仕様の正本 |
| [docs/debug_screen_spec.md](debug_screen_spec.md) | デバッグ画面の項目方針 | HUD 項目の意図 |
| [CHANGELOG.md](../CHANGELOG.md) | 版履歴 | いつ何が変わったか |

**読み分けの目安**

- 「Unity 上で誰が何を持っているか」→ **この資料**
- 「Stage 何まで終わったか」→ `unity_implementation_status.md`
- 「HitStop とは何か」→ `rules.md`
