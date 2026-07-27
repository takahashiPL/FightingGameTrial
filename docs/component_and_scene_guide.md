# Unity FightingGameTrial Component and Scene Guide

最終更新: 2026-07-27
対象ブランチ: `unity`
対象コミット（資料作成時点）: **`2341e4c`**（Add component and scene learning guide）
対象 Scene: `Game/Assets/Scenes/FightDebugScene.unity`
実装到達点: **Stage 15（J Punch 攻撃データ化）完了後**

---

## 0. この資料の目的

### 何を理解できる資料か

Unity の **Scene / GameObject / Component / MonoBehaviour / Inspector 参照** と、
本プロジェクト（FightingGameTrial）の **コード責務・実行経路** を結びつけて説明します。
加えて §16 では、**マネージヒープ / GC / GC.Alloc / Profiler 実習** を現在コードと結びつけて扱います。
題材は現在の `FightDebugScene` です。

読了後に次が追えることを目指します。

- Hierarchy 上のどの GameObject に、どの Component が付いているか
- Inspector の参照が、どの C# フィールドと対応するか
- Play 後に「誰の `Update` が何を始め、誰が SimulationTick を進めるか」
- J キーを押してから Hit / HitStop / HUD 更新までの担当クラス
- 高頻度経路での割り当て候補を Profiler でどう確認するか（実測は別実習）

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
| [docs/component_and_scene_guide.md](component_and_scene_guide.md) | **Scene/Component / GC 学習教材（本ファイル）** | Unity 構造・実行経路・GC.Alloc 確認 |
| [docs/unity_implementation_status.md](unity_implementation_status.md) | 段階到達点・次工程 | 実装状況の正本 |
| [docs/rules.md](rules.md) | ゲーム仕様 | 仕様の正本 |
| [docs/debug_screen_spec.md](debug_screen_spec.md) | デバッグ画面の項目方針 | HUD 項目の意図 |
| [CHANGELOG.md](../CHANGELOG.md) | 版履歴 | いつ何が変わったか |

**読み分けの目安**

- 「Unity 上で誰が何を持っているか」→ **この資料 §1〜§15**
- 「GC.Alloc をどう見るか」→ **この資料 §16**
- 「Stage 何まで終わったか」→ `unity_implementation_status.md`
- 「HitStop とは何か」→ `rules.md`

---

## 16. Unity C# のメモリ確保と GC

この章は **Unity一般** の基礎と、**本プロジェクト** の現在コードを分けて書きます。
「コード上割り当てが起きそう」と「Profiler で実測した」は別です。実測していないものは **未計測** と明記します。
Editor と Development Build / Player では数値が変わり得ます。

### 16.1 GC とは何か

#### Unity一般

| 用語 | 意味 |
|---|---|
| **マネージヒープ** | C# の参照型オブジェクトが置かれるメモリ領域（Unity / .NET ランタイムが管理） |
| **参照型** | `class`・配列・`string`・`List<T>` など。変数は参照を持ち、実体はヒープ上にあることが多い |
| **値型** | `struct`・`int`・`float`・`bool`・`Vector2`/`Vector3`/`Rect`/`Color` など。スタックや包含先に直接載ることが多い |
| **GC（Garbage Collection）** | 参照されなくなったマネージオブジェクトを、ランタイムが後からまとめて回収する仕組み |

要点:

- ゲーム側が「今すぐこのオブジェクトを回収せよ」と完全には制御できない
- 回収が走るとフレーム時間が乱れることがある（スパイク）
- **`new` と書いたら必ず GC 対象になるわけではない**（値型の `new Vector3(...)` など）
- 主なヒープ確保候補: `class` / 配列 / `string` / `List` など
- 値型でも **boxing**（値型を `object` やジェネリック制約のない参照として扱う）で割り当てが起きることがある
- Unity の **`Destroy`（GameObject/Component 破棄）と GC は同じ処理ではない**。前者はエンジンオブジェクトの破棄、後者はマネージヒープの回収

#### 本プロジェクトとの対応

戦闘進行の正本は 60Hz の SimulationTick / CombatFrame です（`docs/rules.md`）。
GC の話は「仕様の正本」ではなく、**実行時コストの学習**です。

### 16.2 なぜゲームでは問題になるのか

#### Unity一般

1回きりの初期化より、次のような **高頻度の小さな確保** が積み上がりやすいです。

- `Update` / `FixedUpdate`
- 毎フレームの UI 文字列更新
- 毎フレームの探索・ログ・一時配列
- 弾・エフェクトの `Instantiate` / `Destroy` の繰り返し

#### 本プロジェクト（高頻度経路の候補）

| 経路 | 誰が回すか | 備考 |
|---|---|---|
| 描画 Frame（`Update`） | `DebugGameplayInput` / `DebugPlaybackInput` / `SimulationClockDriver` / `DebugHudView` | 実時間ベース |
| SimulationTick | `SimulationClockDriver` → `SimulationSession.ProcessOneSimulationTick` | 約 60Hz（Pause 外） |
| CombatFrame | Session 内（HitStop 中は進まない） | 戦闘進行 |
| J Punch 処理 | Session + Participant + Resolver | Active 中の Box 評価など |
| HUD 文字列更新 | `DebugHudView.Update` | 毎描画 Frame |
| Console ログ | Session の `Debug.Log` 群 | 定期／イベント時 |

これらを「GC.Alloc を最初に疑う場所」として扱います。割り当てが必ず大きい、とは断定しません。

### 16.3 GC を減らす基本手法

| 手法 | 何を防ぐか | 使用例 | 注意点 |
|---|---|---|---|
| List / Dictionary / 配列の再利用 | 毎回のコレクション生成 | 一時結果バッファをフィールドに保持 | 使い回し忘れで古い要素が残る |
| 初期容量指定 | 内部配列の再確保 | `new List<T>(n)` | 過大確保はメモリを食う |
| `Clear` して再利用 | 新しい List の確保 | 毎 Frame `Clear` → 再充填 | 内部配列はすぐ解放されないことが多い |
| 戻り値で毎回配列を作らない | 呼び出し側への一時配列 | out 引数や共有バッファ | API 設計が変わる |
| `static readonly` で固定データ1回生成 | 毎 Hit / 毎 Frame の設定オブジェクト | `DebugAttackData.JPunch` | 可変状態を static に載せない |
| Object Pool | Instantiate/Destroy の繰り返し | 弾・ヒットエフェクト | 管理コスト・解放漏れ |
| NonAlloc API | 物理クエリなどの一時配列 | `Overlap*NonAlloc` | バッファサイズ設計が必要 |
| 文字列更新を値変更時だけ | 毎 Frame の `string` 生成 | 差分があったときだけ HUD 更新 | 差分検知の実装が必要 |
| `TMP_Text.SetText` 等 | 割り当てを抑えた数値表示 | 可能なら補間より専用 API | すべての文字列が消えるわけではない |
| `StringBuilder` 再利用 | 連結ごとの中間 `string` | フィールドに保持して `Clear` | 最後の `ToString` で `string` は生成される |
| 高頻度で LINQ を避ける | イテレータや一時コレクション | 戦闘 tick 内 | Editor ツールや初期化では可読性優先も可 |
| クロージャ・ラムダの捕捉に注意 | 隠れてのヒープ確保 | イベント購読 | ローカル関数でも捕捉すると確保し得る |
| boxing を避ける | 値型の箱詰め | 非ジェネリックな API | 見た目は単純でもコストが出る |
| Component 参照を保持 | 毎回の検索 | Inspector / Awake でキャッシュ | 本プロジェクト方針と一致 |
| Instantiate/Destroy の繰り返しを避ける | ネイティブ＋マネージ双方の負荷 | Pool | 少量・低頻度なら可読性優先も可 |
| Coroutine と `WaitForSeconds` | 毎起動の確保 | キャッシュした `WaitForSeconds` | 本プロジェクトは攻撃進行に Coroutine 未使用 |
| イベント購読の解除 | 購読者の生存による解放阻害 | `-=` / 購読寿命の管理 | 漏れはメモリ増の原因 |

**方針**: すべてを禁止ルールにしない。初期化・低頻度・Editor ツールは可読性優先を許容する。
**Profiler で問題になった箇所から直す**（`docs/learning_and_readability.md` の「まず読みやすい基準実装」と両立させる）。

### 16.4 FightingGameTrial の現在構成で既に良い点

コードで確認できたものだけ書きます。

| 良い点 | 根拠（コード） |
|---|---|
| J Punch 設定を `static readonly` で1回生成 | `DebugAttackData.JPunch`。コメントでも毎 Frame / 毎 Hit `new` しないと明記 |
| Session は攻撃データの正本を持たず参照する | `SimulationSession` の `JPunchData` |
| Inspector 参照で Participant / Motor / Visual / Input を保持 | Find 系 API を Scripts 配下で未使用（検索結果なし） |
| 攻撃進行を Coroutine ではなく整数 Frame 状態で管理 | `DebugFighterAttackState.ActionFrame` 等 |
| 1攻撃1Hit 用の状態を保持 | `HasCurrentJPunchHit` / `MarkHit` |
| P2 Neutral 入力を毎 tick `new` しない | Session が `p2NeutralInput` を Awake で1つ保持 |
| 論理入力は既存インスタンスへ上書き | `SampleCurrentInputFromGameplay` → `CopyFromPhysicalAndCommit`（null 時のみ `new`） |
| Resolver は static 純関数で一時 List を作らない | `DebugPunchHitResolver` / `DebugFighterPushResolver` |
| BoxView の `AddComponent` は Awake 時 | `DebugFighterParticipant.EnsureBoxView` |
| LINQ / Coroutine / `FindObjectOfType` を高頻度経路で使っていない | Scripts 配下に該当 API なし |

補足: `DebugBox2D` は **class**（struct ではない）。「struct だからヒープに載らない」とは言えない。
`SpriteRenderer.color` の変更自体は、通常はマネージ確保の主因にはならない。

### 16.5 現在の GC.Alloc 候補

以下は **コード上の候補** です。実際に毎 Frame 何バイト確保しているかは **未計測** です。

| 箇所 | コード上の候補 | 実行頻度 | 推定される割り当て種類 | 実測状態 |
|---|---|---|---|---|
| `DebugHudView.Update` → `BuildStatusHudText` | 多数の `string` 連結（`text = text + ...`）と `ToString("0.00")` 等 | **毎描画 Frame** | `string`（連結・数値整形） | 未計測 |
| `DebugHudView.Update` → `BuildHelpHudText` | 操作説明文字列の再構築（内容はほぼ固定） | **毎描画 Frame** | `string` | 未計測 |
| `DebugHudView` → `hudText.text` / `helpText.text` 代入 | TMP への文字列設定 | 毎描画 Frame | TMP 側の内部処理の可能性（コード外） | 未計測 |
| `BuildStatusHudText` 内の `EvaluateWorld*Box` | 各評価で `new DebugBox2D()`（class） | HUD 更新ごと（Push/Hurt/Hit） | `DebugBox2D` インスタンス | 未計測 |
| `DebugFighterParticipant.EvaluateWorldHitBox` 等 | Session の Hit 判定経路でも `new DebugBox2D()` | Active 中の Combat など | `DebugBox2D` | 未計測 |
| `SimulationSession` の Attack started/ended / Punch hit / KO | `Debug.Log` + 文字列連結・`ToString` | イベント時 | `string`（ログ用） | 未計測 |
| `WriteConsoleLogIfNeeded` | 長い状態ログの連結 | 既定で **60 SimulationTick ごと** | `string` | 未計測 |
| Push / wall 再配分ログ | 補正立ち上がり時の `Debug.Log` | 低頻度（立ち上がり時） | `string` | 未計測 |
| `DebugHudView` で `CurrentInput == null` 時 | `new SimulationInputState()` | 通常は稀（防御コード） | class インスタンス | 未計測 |
| `DebugFighterBoxView` | 子 GO / `LineRenderer` / `Material` | 主に Awake 周辺 | Unity オブジェクト＋ Material | 未計測 |
| Input（Gameplay/Playback） | フィールドへの bool 上書きが中心。配列/List 生成なし | 毎描画 Frame | （この範囲では主候補なし） | 未計測 |
| `SimulationInputState` | **class**。ただし正規経路は再利用 | tick ごと（再利用） | null 時のみ確保 | 未計測 |

**見ていない／断定しないこと**

- Editor の Inspector / Console / Profiler 自身の割り当て
- TMP パッケージ内部の確保量
- 「候補がある＝今のフレーム予算を超えている」

### 16.6 DebugHudView を最初に調べる理由

1. **毎描画 Frame** で動く（SimulationTick より高頻度になり得る）
2. 状態文字列が長く、数値整形と連結が多い
3. Debug 用途のため、戦闘ロジックと分離して改善しやすい
4. Hit / KO の仕様を変えずに観察できる

ただし **この資料追加時点ではコード変更をしない**。
先に Profiler で GC.Alloc を確認し、Editor 由来のノイズと区別します。

### 16.7 Unity Profiler で GC.Alloc を確認する実習

**前提（重要）**: ここで得られる数値は **Unity Editor 上の計測** です。製品性能の最終判断には使いません（§16.8）。

#### 手順

1. `FightDebugScene` を開く
2. **Window → Analysis → Profiler** を開く
3. **CPU Usage** モジュールを選ぶ
4. Play Mode を開始する
5. 数秒待つ（安定させる）
6. Editor を Pause する（または Profiler の録画を止める）
7. Hierarchy 表示で **GC.Alloc** 列を見る
8. `DebugHudView.Update` を探す
9. `SimulationClockDriver.Update` を探す
10. `SimulationSession.ProcessOneSimulationTick`（およびその子）を探す
11. Console ログが出た Frame を別途見る
12. 必要なら **Call Stacks** を有効にして割り当て元を辿る

**Deep Profile** は最初から使わない（計測自体が重く、ノイズが増えやすい）。

#### 確認ケース（数値は空欄テンプレート）

| ケース | GC.Alloc / Frame | 主な発生元 | 備考 |
|---|---|---|---|
| A. 通常待機 |  |  | Editor 計測 |
| B. 移動（矢印） |  |  | |
| C. J Punch Miss |  |  | |
| D. J Punch Hit |  |  | |
| E. HitStop 中 |  |  | Combat は止まるが Update は動く |
| F. KO |  |  | |
| G. Reset（R） |  |  | |
| H. Console 閉 / 開の比較 |  |  | Editor ノイズ比較用 |

測定値はまだ記入しない。記入は「次の実習」（§16.12）で行う。

### 16.8 Editor と Development Build の違い

| 環境 | 混ざりやすいもの |
|---|---|
| **Editor** | Editor UI、Inspector、Console 描画、Profiler 自体、Domain 関連、TMP の Editor 支援 |
| **Development Build** | Autoconnect Profiler で Player 側に近い計測が可能 |
| **Release / 実機** | ログ抑制や最適化の差が出る |

**結論**

- Editor 測定 → **候補発見用**
- 最終判断 → **Development Build**（必要なら実機）で行う

### 16.9 改善の優先順位（FightingGameTrial 向け）

1. Profiler で待機時 GC.Alloc を確認する
2. `DebugHudView` の文字列生成を確認する
3. Console ログ（定期・Hit）の生成頻度を確認する
4. SimulationTick 内の配列・List・LINQ（現状ほぼ無し）を確認する
5. 将来の HitEffect / Projectile で Object Pool を検討する
6. 複数攻撃追加時のデータ生成（毎 Hit `new` しない）を確認する
7. 必要になったときだけ NativeArray / Jobs / Burst を検討する

**重要**

- NativeArray / Jobs / Burst を最初の対策にしない
- 現在規模では通常 C# の割り当て整理を優先する
- **GC.Alloc 0 を目的化しない**
- フレーム時間と可読性の両方で判断する

### 16.10 よくある誤解

1. `new Vector3` は必ず GC ではない
2. `Clear()` で必ず内部配列が解放されるわけではない
3. Incremental GC は「割り当てそのもの」を消さない
4. `GC.Collect()` を頻繁に呼べばよいわけではない
5. Object Pool は何にでも使えばよいわけではない
6. static にすれば GC 問題が解決するわけではない
7. キャッシュしすぎると解放されない（寿命が延びる）
8. LINQ は常に禁止ではない（高頻度経路で測って判断）
9. Coroutine は常に悪いわけではない（本プロジェクトの攻撃進行には未使用）
10. `StringBuilder` でも `ToString` 時に `string` が生成される
11. Profiler で GC.Alloc が見えないから永久に割り当てゼロとは限らない
12. Editor 計測だけで製品性能を断定しない

### 16.11 本プロジェクト向け暫定ルール（学習用）

- 高頻度経路では不要な参照型生成を避ける
- 固定データは初期化時に1回だけ作る（例: `DebugAttackData.JPunch`）
- List / 配列は必要に応じて再利用する
- 毎 Frame の文字列生成は Profiler 確認対象にする
- ログは開発用として Release で抑制可能にする余地を残す
- Instantiate / Destroy を大量に繰り返す機能では Pool を検討する
- 物理検索を高頻度で行う場合は NonAlloc 版を検討する
- LINQ / closure / boxing は測定して判断する
- 可読性を壊す最適化は、効果を測定してから行う
- 最適化前後で GC.Alloc と Frame Time を比較する

これらは学習用方針です。実装状況の正本（何が Stage 完了か）は `docs/unity_implementation_status.md` です。

### 16.12 次の実習候補: FightingGameTrial の GC.Alloc 基準測定

コード変更前の **Baseline 測定** を次作業候補とします。

1. Editor で §16.7 のケース A〜H を記録する（表の空欄を埋める）
2. 待機時に `DebugHudView.Update` が主因かを Call Stacks で確認する
3. Development Build でも同ケースを取り、Editor との差を見る
4. 結果を Docs（本資料または測定メモ）へ記録する
5. **実測後にのみ** 改善案（HUD 差分更新、ログ抑制、Box 再利用など）を決める

この段階では、まだ最適化パッチを入れない。
