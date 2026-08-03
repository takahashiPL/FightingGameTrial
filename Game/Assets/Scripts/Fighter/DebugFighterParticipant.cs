using FightingGameTrial.Combat;
using FightingGameTrial.DebugTools;
using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// デバッグ用の参加枠（P1 / P2）です。キャラクター種類ではありません。
    ///
    /// 責務:
    /// - SlotId（P1/P2）を識別する
    /// - Motor / Visual / SpriteRenderer への明示参照を持つ
    /// - 表示 Tint（同キャラ色違い）を初期化時に適用する
    /// - ゲームプレイ入力を使うか（使わないなら Session が Neutral を渡す）
    /// - 相手 Participant への明示参照を持つ
    /// - 自分専用の DebugFighterAttackState を1つ所有する
    /// - 自分専用の DebugFighterHitState（被 Hit / HitStun / ノックバック速度）を所有する（段階12A / 13A）
    /// - Participant 共通の最大HP・現在HPを所有する（段階14A）
    /// - Participant 共通の KO 状態を所有する（段階14B。HitState には持たせない）
    /// - 横方向 Push Box 半幅を持つ（段階10B-3。重なり解消の計算に使う）
    /// - Push / Hurt Box のローカル定義を持ち、World Box を計算する（段階11A）
    /// - J Punch Hit Box の World 変換・Facing 反転を行う（local 正本は攻撃データ・段階15）
    ///
    /// やらないこと:
    /// - 攻撃データの正本を持たない（DebugAttackData.JPunch。段階15）
    /// - Update で移動しない
    /// - Keyboard.current を直接読まない
    /// - SimulationTick を回さない
    /// - 相手を検索しない（Inspector で opponent を接続する）
    /// - 攻撃開始・ActionFrame進行・Hit判定を自分で回さない（状態の所有のみ。進行は Session）
    /// - attacker / defender の選択や Hit 成立判定をしない（Session の責務）
    /// - Push 重なり解消を自分で回さない（Session が DebugFighterPushResolver を呼ぶ）
    /// - Transform へノックバックを直接書かない（Motor.SetLogicalX 経由。Session が呼ぶ）
    /// - Round 終了 / 勝敗判定 / WIN表示をしない（段階14B は KO 状態のみ）
    /// - Box 枠の描画をしない（DebugFighterBoxView の責務）
    /// - CharacterDefinition を持たない（後段）
    ///
    /// なぜ Slot とキャラ種類を分けるか:
    /// 同じキャラを P1/P2 が選んでも、参加枠側の Tint だけで色違いにできる。
    /// 別キャラにするときは将来の CharacterDefinition 参照を差し替える想定で、
    /// ここでは Sprite や速度を SlotId で分岐しない。
    ///
    /// なぜ P2 が棒立ちか:
    /// Dummy 専用ロジックだからではなく、Session が Neutral 入力（全 false）を渡すから。
    /// 同じ Motor 処理を通るが、Left/Right が無いので移動しない。
    ///
    /// 攻撃と被弾（段階10B-2 / 12A / 13A / 14A / 14B）:
    /// Participant は自分の AttackState・HitState・HP・KO を所有します。
    /// SimulationSession が attacker / defender を選び Hit を解決し、
    /// 成立時に defender.ReceiveHit / ApplyDamage / TryEnterKnockout を呼びます。
    ///
    /// HP / KO（段階14A / 14B）:
    /// HP と KO は別概念。HP==0 で一度だけ KO へ遷移し、Reset まで維持する。
    /// HitStun 終了で KO を Idle へ戻さない。
    ///
    /// Box 可視化（段階11A）:
    /// ローカル定義はここが所有し、World 変換もここで行う。
    ///
    /// Box 判定（段階11B）:
    /// EvaluateWorldHitBox / EvaluateWorldHurtBox の結果を Session が重なり判定に使う。
    /// 可視化と同じ経路なので、見た目の枠と実判定がズレない。
    /// </summary>
    public class DebugFighterParticipant : MonoBehaviour
    {
        [Header("参加枠（キャラ種類ではない）")]
        [Tooltip("デバッグ用の参加枠 ID です。キャラクター種類の選択ではありません。")]
        [SerializeField]
        private DebugFighterSlotId slotId = DebugFighterSlotId.P1;

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("この参加者の移動・Facing を持つ DebugFighterMotor です。")]
        [SerializeField]
        private DebugFighterMotor motor;

        [Tooltip("この参加者の Visual State → Sprite 切替を持つ DebugFighterVisual です。")]
        [SerializeField]
        private DebugFighterVisual visual;

        [Tooltip("表示色（Tint）を適用する SpriteRenderer です。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "相手の DebugFighterParticipant です。"
            + " Facing 更新で相手 X を読むために使います。検索はしません。"
        )]
        [SerializeField]
        private DebugFighterParticipant opponent;

        [Header("表示バリエーション（参加枠側）")]
        [Tooltip(
            "同キャラ色違い用の表示色です。"
            + " Visual は sprite だけを切り替え、通常時の color はここが正本です。"
            + " 将来の Material / Palette 差し替え余地として、まずは Color のみ使います。"
        )]
        [SerializeField]
        private Color displayTint = Color.white;

        [Tooltip(
            "HitStop / HitStun 中の被 Hit 表示色です（段階12A）。"
            + " LineRenderer の Box 色は変更しません。"
        )]
        [SerializeField]
        private Color hitStunDisplayColor = new Color(1f, 0.35f, 0.35f, 1f);

        [Tooltip(
            "Ground Clash 反動中の専用表示色です。"
            + " 通常被弾の赤色と区別し、Damage 0 の相打ちであることを見分けます。"
        )]
        [SerializeField]
        private Color clashRecoilDisplayColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip(
            "立ちガード硬直中の専用表示色です。"
            + " 通常被弾の赤色・Clash の黄と区別します（検証用）。"
        )]
        [SerializeField]
        private Color guardStunDisplayColor = new Color(0.45f, 0.7f, 0.95f, 1f);

        [Tooltip(
            "KO 中の表示色です（段階14B）。"
            + " 被 Hit 表示（HitStun 中）が終わったあとだけ適用します。"
            + " Scene / Prefab 変更なしで既定の暗いグレーを使います。"
        )]
        [SerializeField]
        private Color knockoutDisplayColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Header("入力")]
        [Tooltip(
            "true なら Session が DebugGameplayInput 由来の入力を渡します。"
            + " false なら Session が Neutral 入力（全 false）を渡します（棒立ち）。"
        )]
        [SerializeField]
        private bool usesGameplayInput = true;

        [Header("HitStun / KB（段階15: J Punch は攻撃データが正本）")]
        [Tooltip(
            "旧暫定フィールド（段階12A）。J Punch 適用値は DebugAttackData.JPunch.HitStunFrames。"
            + " Session は攻撃データを参照する。Inspector 互換のため残す。"
        )]
        [SerializeField]
        private int hitStunFrames = 12;

        [Tooltip(
            "旧暫定フィールド（段階13A）。J Punch 初速は DebugAttackData.JPunch。"
            + " Session は攻撃データを参照する。Inspector 互換のため残す。"
        )]
        [SerializeField]
        private float knockbackInitialSpeed = 0.18f;

        [Tooltip(
            "旧暫定フィールド（段階13A）。J Punch 減速は DebugAttackData.JPunch。"
            + " Session が攻撃データの減速を渡す。Inspector 互換のため残す。"
        )]
        [SerializeField]
        private float knockbackDeceleration = 0.015f;

        [Header("HP / KO（段階14A / 14B）")]
        [Tooltip(
            "最大 Hit Points です。攻撃データ化・キャラ固有化前の暫定値。"
            + " 現在HPは実行時状態で、Inspector からは編集しません。"
            + " HP が 0 になると段階14B で KO 状態へ遷移します。"
        )]
        [SerializeField]
        private int maxHitPoints = 100;

        /// <summary>
        /// 現在 HP（実行時状態の正本）。Awake / Reset で最大へ戻す。
        /// Inspector 編集対象にしない（SerializeField にしない）。
        /// </summary>
        private int currentHitPoints;

        /// <summary>
        /// KO 状態の正本（段階14B）。HP 0 到達時に一度だけ true。
        /// Reset まで解除しない。HitState には持たせない。
        /// </summary>
        private bool isKnockedOut;

        [Header("Push Box（段階10B-3・判定用半幅）")]
        [Tooltip(
            "横方向 Push Box の半幅（ワールド単位）です。"
            + " Push Resolver が使う正本。2人の中心間に必要な最小距離 = 双方の半幅の合計。"
            + " 可視化用ローカル定義の HalfWidth もこの値に合わせます。"
        )]
        [SerializeField]
        private float pushBoxHalfWidth = 0.5f;

        [Header("Box 座標原点（段階11A）")]
        [Tooltip(
            "Transform.position.y から論理的な接地位置（足元）へのローカル Y オフセットです。"
            + " World の Box 原点 Y = Transform.position.y + この値。"
            + " 現在のデバッグ Sprite は pivot が中央のため、Awake で"
            + " -(sprite.pivot.y / pixelsPerUnit) を一度だけ入れます。"
            + " SpriteRenderer.bounds を毎フレームは使いません。"
        )]
        [SerializeField]
        private float boxOriginLocalY = 0f;

        [Tooltip(
            "true なら Awake 時に SpriteRenderer.sprite の pivot から"
            + " boxOriginLocalY を自動設定します（Scene 再保存不要）。"
        )]
        [SerializeField]
        private bool deriveBoxOriginLocalYFromSprite = true;

        [Header("Box ローカル定義（段階11A・可視化用。判定未使用）")]
        [Tooltip(
            "Push Box のローカル定義です。"
            + " HalfWidth は pushBoxHalfWidth が正本（Resolver と一致させる）。"
            + " Center は論理接地位置（足元）からの相対です。"
        )]
        [SerializeField]
        private DebugBox2D pushBoxLocal = new DebugBox2D(0f, 1f, 0.5f, 1f, true);

        [Tooltip(
            "Hurt Box のローカル定義です。常時存在（可視化は常時）。"
            + " 現段階では被弾判定には使いません。"
        )]
        [SerializeField]
        private DebugBox2D hurtBoxLocal = new DebugBox2D(0f, 1f, 0.45f, 1f, true);

        [Tooltip(
            "Jパンチ Hit Box のローカル定義ミラーです（Inspector 確認用）。"
            + " 判定・可視化の正本は DebugAttackData.JPunch の local 値（段階15）。"
            + " Facing 反転と World 変換は EvaluateWorldHitBox の責務。"
        )]
        [SerializeField]
        private DebugBox2D jPunchHitBoxLocal = new DebugBox2D(0.75f, 1.25f, 0.55f, 0.35f, false);

        [Header("攻撃状態")]
        [Tooltip(
            "この参加者自身の攻撃進行状態です。"
            + " P1/P2で同じ型を持ち、Sessionから共通処理されます。"
        )]
        [SerializeField]
        private DebugFighterAttackState attackState =
            new DebugFighterAttackState();

        [Header("被 Hit 状態（段階12A / 13A）")]
        [Tooltip(
            "この参加者自身の被 Hit / HitStun / ノックバック速度です。"
            + " P1/P2で同じ型を持ち、Sessionから共通処理されます。"
        )]
        [SerializeField]
        private DebugFighterHitState hitState = new DebugFighterHitState();

        // ------------------------------------------------------------
        // 表示色の復元用（Awake で displayTint を控える）
        // ------------------------------------------------------------

        private Color baseDisplayTint = Color.white;

        public DebugFighterSlotId SlotId
        {
            get { return slotId; }
        }

        public DebugFighterMotor Motor
        {
            get { return motor; }
        }

        public DebugFighterVisual Visual
        {
            get { return visual; }
        }

        public SpriteRenderer SpriteRenderer
        {
            get { return spriteRenderer; }
        }

        public DebugFighterParticipant Opponent
        {
            get { return opponent; }
        }

        public Color DisplayTint
        {
            get { return displayTint; }
        }

        public bool UsesGameplayInput
        {
            get { return usesGameplayInput; }
        }

        /// <summary>
        /// 横方向 Push Box 半幅です。負数や 0 以下は安全側で 0 として扱います。
        /// </summary>
        public float PushBoxHalfWidth
        {
            get
            {
                if (pushBoxHalfWidth < 0f)
                {
                    return 0f;
                }

                return pushBoxHalfWidth;
            }
        }

        public DebugFighterAttackState AttackState
        {
            get { return attackState; }
        }

        public DebugFighterHitState HitState
        {
            get { return hitState; }
        }

        /// <summary>
        /// 被 Hit 累計です。正本は HitState.TotalHitCount（HUD の P2HitCount 互換）。
        /// </summary>
        public int HitCount
        {
            get
            {
                if (hitState == null)
                {
                    return 0;
                }

                return hitState.TotalHitCount;
            }
        }

        public bool WasHitThisCombatFrame
        {
            get
            {
                if (hitState == null)
                {
                    return false;
                }

                return hitState.WasHitThisCombatFrame;
            }
        }

        public int LastHitCombatFrame
        {
            get
            {
                if (hitState == null)
                {
                    return -1;
                }

                return hitState.LastHitCombatFrame;
            }
        }

        /// <summary>
        /// HitStun 中（残り > 0）か。HitStop 中も残りは維持されるため true になり得る。
        /// </summary>
        public bool IsInHitStun
        {
            get
            {
                if (hitState == null)
                {
                    return false;
                }

                return hitState.IsInHitStun;
            }
        }

        /// <summary>Ground Clash 専用反動中か。</summary>
        public bool IsInClashRecoil
        {
            get
            {
                if (hitState == null)
                {
                    return false;
                }

                return hitState.IsInClashRecoil;
            }
        }

        /// <summary>立ちガード硬直中か。</summary>
        public bool IsInGuardStun
        {
            get
            {
                if (hitState == null)
                {
                    return false;
                }

                return hitState.IsInGuardStun;
            }
        }

        /// <summary>
        /// 入力・通常移動を止める戦闘リアクション中か。
        /// 通常 HitStun / Ground Clash 反動 / GuardStun の共通ゲートに使います。
        /// </summary>
        public bool IsInCombatReaction
        {
            get
            {
                if (hitState == null)
                {
                    return false;
                }

                return hitState.IsInCombatReaction;
            }
        }

        /// <summary>
        /// 被 Hit 時に適用する HitStun 長（Combat Frame）。負数は 0 扱い。
        /// </summary>
        public int HitStunFrames
        {
            get
            {
                if (hitStunFrames < 0)
                {
                    return 0;
                }

                return hitStunFrames;
            }
        }

        /// <summary>
        /// ノックバック初速の絶対値（Combat Frame 単位）。負数は 0 扱い。
        /// </summary>
        public float KnockbackInitialSpeed
        {
            get
            {
                if (knockbackInitialSpeed < 0f)
                {
                    return 0f;
                }

                return knockbackInitialSpeed;
            }
        }

        /// <summary>
        /// 1 Combat Frame あたりのノックバック減速量。負数は 0 扱い。
        /// </summary>
        public float KnockbackDeceleration
        {
            get
            {
                if (knockbackDeceleration < 0f)
                {
                    return 0f;
                }

                return knockbackDeceleration;
            }
        }

        /// <summary>
        /// 現在のノックバック横速度（正本は HitState）。
        /// </summary>
        public float KnockbackVelocityX
        {
            get
            {
                if (hitState == null)
                {
                    return 0f;
                }

                return hitState.KnockbackVelocityX;
            }
        }

        /// <summary>
        /// ノックバック適用中か（速度が 0 でない）。
        /// </summary>
        public bool IsBeingKnockedBack
        {
            get
            {
                if (hitState == null)
                {
                    return false;
                }

                return hitState.IsBeingKnockedBack;
            }
        }

        /// <summary>
        /// 最大 HP。1 未満は安全側で 1 として扱う。
        /// </summary>
        public int MaxHitPoints
        {
            get
            {
                if (maxHitPoints < 1)
                {
                    return 1;
                }

                return maxHitPoints;
            }
        }

        /// <summary>
        /// 現在 HP。0 以上・最大以下。
        /// </summary>
        public int CurrentHitPoints
        {
            get { return currentHitPoints; }
        }

        /// <summary>
        /// 現在 HP が 0 以下か（数値上の枯渇）。
        /// KO 遷移済みかどうかは IsKnockedOut を見る（段階14B）。
        /// </summary>
        public bool IsHitPointsDepleted
        {
            get { return currentHitPoints <= 0; }
        }

        /// <summary>
        /// KO 状態か（段階14B）。Reset まで維持。
        /// HP==0 でも、遷移前の一瞬や未遷移時は false のままになり得る。
        /// </summary>
        public bool IsKnockedOut
        {
            get { return isKnockedOut; }
        }

        /// <summary>
        /// Push Box ローカル定義（可視化・Inspector 確認用）。
        /// </summary>
        public DebugBox2D PushBoxLocal
        {
            get { return pushBoxLocal; }
        }

        /// <summary>
        /// Hurt Box ローカル定義（可視化・Inspector 確認用）。
        /// </summary>
        public DebugBox2D HurtBoxLocal
        {
            get { return hurtBoxLocal; }
        }

        /// <summary>
        /// Jパンチ Hit Box ローカル定義（可視化・Inspector 確認用）。
        /// </summary>
        public DebugBox2D JPunchHitBoxLocal
        {
            get { return jPunchHitBoxLocal; }
        }

        /// <summary>
        /// Transform.position.y から論理接地（足元）へのローカル Y オフセットです。
        /// </summary>
        public float BoxOriginLocalY
        {
            get { return boxOriginLocalY; }
        }

        /// <summary>
        /// 論理接地位置の World Y です（Box 共通の Y 原点）。
        /// </summary>
        public float LogicalGroundY
        {
            get { return transform.position.y + boxOriginLocalY; }
        }

        private void Awake()
        {
            if (motor == null)
            {
                Debug.LogError("DebugFighterParticipant: Motor が未設定です。");
            }

            if (visual == null)
            {
                Debug.LogError("DebugFighterParticipant: Visual が未設定です。");
            }

            if (spriteRenderer == null)
            {
                Debug.LogError("DebugFighterParticipant: SpriteRenderer が未設定です。");
            }

            if (opponent == null)
            {
                Debug.LogError("DebugFighterParticipant: Opponent が未設定です。");
            }

            EnsureLocalBoxDefaults();
            RefreshBoxOriginLocalYIfNeeded();

            // Push Resolver 用半幅を可視化定義へ同期（二重の真実を作らない）。
            if (pushBoxLocal != null)
            {
                pushBoxLocal.HalfWidth = PushBoxHalfWidth;
            }

            // Serializable な通常クラスのため AddComponent は使わない。
            // 参加者ごとに別インスタンスを持ち、P1/P2 で共有しない。
            if (attackState == null)
            {
                attackState = new DebugFighterAttackState();
            }

            attackState.Reset();

            if (hitState == null)
            {
                hitState = new DebugFighterHitState();
            }

            hitState.Reset();

            // 段階14A / 14B: HP 全回復、KO 解除。
            RestoreHitPointsToMaximum();
            ClearKnockoutForReset();

            baseDisplayTint = displayTint;
            ApplyDisplayColor();
            EnsureBoxView();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureLocalBoxDefaults();
            if (pushBoxLocal != null)
            {
                // Inspector で pushBoxHalfWidth を変えたとき、可視化定義も追従させる。
                pushBoxLocal.HalfWidth = pushBoxHalfWidth < 0f ? 0f : pushBoxHalfWidth;
            }
        }
#endif

        /// <summary>
        /// Box 可視化コンポーネントを同じ GameObject へ自動用意します。
        /// Scene への手動配線を増やさないための段階11A 方針です。
        /// </summary>
        private void EnsureBoxView()
        {
            DebugFighterBoxView boxView = GetComponent<DebugFighterBoxView>();
            if (boxView == null)
            {
                boxView = gameObject.AddComponent<DebugFighterBoxView>();
            }

            boxView.Bind(this);
        }

        private void EnsureLocalBoxDefaults()
        {
            if (pushBoxLocal == null)
            {
                pushBoxLocal = new DebugBox2D(0f, 1f, 0.5f, 1f, true);
            }

            if (hurtBoxLocal == null)
            {
                hurtBoxLocal = new DebugBox2D(0f, 1f, 0.45f, 1f, true);
            }

            // Inspector ミラー用。判定の正本は DebugAttackData.JPunch。
            DebugAttackData jPunch = DebugAttackData.JPunch;
            if (jPunchHitBoxLocal == null)
            {
                jPunchHitBoxLocal = jPunch.CreateLocalHitBoxDefinition(false);
            }
            else
            {
                jPunchHitBoxLocal.Set(
                    jPunch.HitBoxLocalCenterX,
                    jPunch.HitBoxLocalCenterY,
                    jPunch.HitBoxHalfWidth,
                    jPunch.HitBoxHalfHeight,
                    jPunchHitBoxLocal.IsActive
                );
            }
        }

        /// <summary>
        /// Push Box の World 座標を返します（段階11A）。
        ///
        /// 変換の流れ:
        /// 1. 原点 X = Motor.LogicalX（位置の正本）
        /// 2. 原点 Y = 論理接地 Y（Transform.position.y + boxOriginLocalY）
        /// 3. WorldCenter = 原点 + LocalCenter
        /// 4. HalfWidth は pushBoxHalfWidth（Resolver と同じ値）
        ///
        /// Facing では左右反転しない（体幹の押し合い箱のため）。
        /// 現段階では可視化専用。Push Resolver は従来どおり半幅だけを使う。
        /// </summary>
        public DebugBox2D EvaluateWorldPushBox()
        {
            EnsureLocalBoxDefaults();

            DebugBox2D world = new DebugBox2D();
            float originX;
            float originY;
            GetBoxOrigin(out originX, out originY);

            float halfWidth = PushBoxHalfWidth;
            float halfHeight = pushBoxLocal.HalfHeight;
            if (halfHeight < 0f)
            {
                halfHeight = 0f;
            }

            world.Set(
                originX + pushBoxLocal.CenterX,
                originY + pushBoxLocal.CenterY,
                halfWidth,
                halfHeight,
                true
            );
            return world;
        }

        /// <summary>
        /// Hurt Box の World 座標を返します（段階11A）。
        /// 常時有効。Facing 反転なし。被弾判定にはまだ使わない。
        /// </summary>
        public DebugBox2D EvaluateWorldHurtBox()
        {
            EnsureLocalBoxDefaults();

            DebugBox2D world = new DebugBox2D();
            float originX;
            float originY;
            GetBoxOrigin(out originX, out originY);

            float halfWidth = hurtBoxLocal.HalfWidth;
            float halfHeight = hurtBoxLocal.HalfHeight;
            if (halfWidth < 0f)
            {
                halfWidth = 0f;
            }

            if (halfHeight < 0f)
            {
                halfHeight = 0f;
            }

            world.Set(
                originX + hurtBoxLocal.CenterX,
                originY + hurtBoxLocal.CenterY,
                halfWidth,
                halfHeight,
                true
            );
            return world;
        }

        /// <summary>
        /// 現在再生中の攻撃の Hit Box（World）を返します。
        ///
        /// local 定義の正本: AttackState.CurrentAttackData（Facing Right 基準）。
        /// 攻撃中でなければ JPunch 定義を枠の形だけ返す（IsActive=false）。
        /// Active 連動は攻撃データの Active 区間＋AttackState。
        /// </summary>
        public DebugBox2D EvaluateWorldHitBox()
        {
            EnsureLocalBoxDefaults();

            DebugAttackData attackData = DebugAttackData.JPunch;
            if (attackState != null
                && attackState.IsActionPlaying
                && attackState.CurrentAttackData != null)
            {
                attackData = attackState.CurrentAttackData;
            }

            DebugBox2D world = new DebugBox2D();
            float originX;
            float originY;
            GetBoxOrigin(out originX, out originY);

            bool facingRight = true;
            if (motor != null)
            {
                facingRight = motor.FacingRight;
            }

            float localCenterX = attackData.HitBoxLocalCenterX;
            if (facingRight == false)
            {
                localCenterX = -localCenterX;
            }

            float halfWidth = attackData.HitBoxHalfWidth;
            float halfHeight = attackData.HitBoxHalfHeight;
            if (halfWidth < 0f)
            {
                halfWidth = 0f;
            }

            if (halfHeight < 0f)
            {
                halfHeight = 0f;
            }

            bool isActive = IsCurrentAttackHitBoxActiveNow(attackData);

            world.Set(
                originX + localCenterX,
                originY + attackData.HitBoxLocalCenterY,
                halfWidth,
                halfHeight,
                isActive
            );
            return world;
        }

        /// <summary>
        /// Sprite 資産の pivot から、Transform → 足元（スプライト矩形下端）への
        /// ローカル Y オフセットを一度だけ求めます。
        ///
        /// 根拠（FightDebugScene / デバッグ PNG meta）:
        /// - 階層に Visual 子オフセットはない（Participant と同 GO）
        /// - spritePivot は中央 (0.5, 0.5)、PPU=100、テクスチャ 256 → pivot は中心
        /// - Transform.position.y は足元ではなくスプライト中心
        /// - LocalCenterY=1 / HalfHeight=1 は「原点=足元」前提のため、中心を原点にすると枠が上へずれる
        ///
        /// bounds は使わない。Sprite.pivot と pixelsPerUnit のみ（資産の静的値）。
        /// </summary>
        private void RefreshBoxOriginLocalYIfNeeded()
        {
            if (deriveBoxOriginLocalYFromSprite == false)
            {
                return;
            }

            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Sprite sprite = spriteRenderer.sprite;
            float pixelsPerUnit = sprite.pixelsPerUnit;
            if (pixelsPerUnit <= 0f)
            {
                return;
            }

            // Unity の Sprite.pivot は矩形左下からのピクセル位置。
            // Transform は pivot 位置にあるので、矩形下端（足元側）は
            // transform.y - (pivot.y / PPU) になる。
            boxOriginLocalY = -(sprite.pivot.y / pixelsPerUnit);
        }

        /// <summary>
        /// いま攻撃 Hit Box を有効にすべきか。
        /// 進行は AttackState、Active 境界は渡された攻撃データ。
        /// </summary>
        public bool IsCurrentAttackHitBoxActiveNow(DebugAttackData attackData)
        {
            if (attackState == null || attackData == null)
            {
                return false;
            }

            if (attackState.IsActionPlaying == false)
            {
                return false;
            }

            if (attackState.CurrentAttackData != attackData)
            {
                return false;
            }

            return attackData.IsActiveFrame(attackState.ActionFrame);
        }

        /// <summary>互換: J Punch Active か。</summary>
        public bool IsJPunchHitBoxActiveNow()
        {
            return IsCurrentAttackHitBoxActiveNow(DebugAttackData.JPunch);
        }

        /// <summary>
        /// Box 共通の論理原点を返します（P1/P2 同じ経路）。
        /// X = Motor.LogicalX（無ければ Transform.x）
        /// Y = 論理接地 = Transform.position.y + boxOriginLocalY
        /// （Transform.y を無条件に足元とはみなさない）
        /// </summary>
        private void GetBoxOrigin(out float originX, out float originY)
        {
            originX = transform.position.x;
            if (motor != null)
            {
                originX = motor.LogicalX;
            }

            originY = transform.position.y + boxOriginLocalY;
        }

        /// <summary>
        /// 新しい CombatFrame の開始時に、このフレームの Hit 旗を下ろします。
        /// HitStun 残りは減らしません（Session が Combat 末尾で Tick）。
        /// </summary>
        public void BeginCombatFrame()
        {
            if (hitState == null)
            {
                return;
            }

            hitState.BeginCombatFrame();
        }

        /// <summary>
        /// パンチ Hit を受け取り、被弾記録・HitStun・ノックバック初速を開始します（段階12A / 13A）。
        ///
        /// 何をするか:
        /// - HitState.BeginHitStun（累計加算・Stun 残り・ノックバック初速）
        /// - 実行中の自分の攻撃があれば InterruptByHit（攻撃側の攻撃は触らない）
        /// - 被 Hit 表示色へ切替
        ///
        /// なぜ必要か: Session が defender.ReceiveHit を呼ぶ共通口にするため。
        /// やらないこと: Hit 判定、HitStop 設定、位置移動、Damage（Session が ApplyDamage）、ログ。
        /// このフレームではノックバック移動しない（Session の移動は Hit より前）。
        /// </summary>
        public void ReceiveHit(int combatFrame)
        {
            ReceiveHit(combatFrame, HitStunFrames, 0f);
        }

        /// <summary>
        /// hitStunFrames とノックバック速度を明示して被 Hit を受け取ります。
        /// knockbackVelocityX の符号は Session が LogicalX 比較で決めます。
        /// </summary>
        public void ReceiveHit(int combatFrame, int hitStunFrameCount, float knockbackVelocityX)
        {
            if (hitState == null)
            {
                hitState = new DebugFighterHitState();
            }

            hitState.BeginHitStun(hitStunFrameCount, combatFrame, knockbackVelocityX);

            // 被弾側が攻撃中なら即時中断（Hit Box も IsActive で消える）。
            // 実操作未検証でも、P1/P2 共通基盤として持つ。
            if (attackState != null && attackState.IsActionPlaying)
            {
                attackState.InterruptByHit();
            }

            ApplyDisplayColor();
        }

        /// <summary>
        /// Ground Clash 成立後の専用反動を開始します。
        ///
        /// 通常 ReceiveHit と分ける理由:
        /// - Damage 0 の Clash を被弾回数へ加算しない
        /// - 赤い HitStun 表示ではなく Clash 専用色を使う
        /// - HUD / Visual State で ClashRecoil と判別できるようにする
        ///
        /// 攻撃終了は Session が EndAttackAsClash を先に呼びます。
        /// HitStop は共有時間なので Session が設定します。
        /// </summary>
        public void ReceiveGroundClashRecoil(
            int recoilFrameCount,
            float knockbackVelocityX)
        {
            if (hitState == null)
            {
                hitState = new DebugFighterHitState();
            }

            hitState.BeginClashRecoil(recoilFrameCount, knockbackVelocityX);
            ApplyDisplayColor();
        }

        /// <summary>
        /// 立ちガード成立後の専用硬直を開始します。
        ///
        /// 通常 ReceiveHit と分ける理由:
        /// - HitCount を増やさない
        /// - Damage を入れない
        /// - 赤い HitStun 表示ではなく Guard 専用色を使う
        ///
        /// HitStop は共有時間なので Session が設定します。
        /// </summary>
        public void ReceiveGuardStun(
            int guardStunFrameCount,
            float knockbackVelocityX)
        {
            if (hitState == null)
            {
                hitState = new DebugFighterHitState();
            }

            hitState.BeginGuardStun(guardStunFrameCount, knockbackVelocityX);
            ApplyDisplayColor();
        }

        /// <summary>
        /// Damage を適用し、実際に減った HP 量を返します（段階14A）。
        ///
        /// 何をするか:
        /// - damage &lt;= 0 なら何もしない（戻り値 0）
        /// - currentHP = max(0, currentHP - damage)
        /// - 実際に減った量を返す
        ///
        /// やらないこと: KO 遷移（Session が ApplyDamage 後に TryEnterKnockout）。
        /// HitState には HP を持たせない。
        /// </summary>
        public int ApplyDamage(int damage)
        {
            if (damage <= 0)
            {
                return 0;
            }

            // 万一未初期化でも最大以下に収める。
            if (currentHitPoints < 0)
            {
                currentHitPoints = 0;
            }

            if (currentHitPoints > MaxHitPoints)
            {
                currentHitPoints = MaxHitPoints;
            }

            int before = currentHitPoints;
            int after = before - damage;
            if (after < 0)
            {
                after = 0;
            }

            currentHitPoints = after;
            return before - after;
        }

        /// <summary>
        /// 現在 HP を最大へ戻します（Awake / R Reset 用）。
        /// KO 状態はここでは触らない（ClearKnockoutForReset とセットで呼ぶ）。
        /// </summary>
        public void RestoreHitPointsToMaximum()
        {
            currentHitPoints = MaxHitPoints;
        }

        /// <summary>
        /// HP 0 到達後に KO 状態へ一度だけ遷移します（段階14B）。
        ///
        /// 戻り値: 今回初めて KO へ入ったら true。既に KO なら false。
        ///
        /// 何をするか:
        /// - isKnockedOut を true にする（Reset まで維持）
        /// - 自分が攻撃中なら InterruptByHit で中断（新規攻撃禁止の補完）
        /// - KO 表示色を適用
        ///
        /// なぜ HP と別に持つか:
        /// HP==0 は数値、KO は生命状態。HitStun 終了で Idle 色へ戻しても KO は残す。
        ///
        /// 呼び出し側（Session）は、最後の一撃の ReceiveHit / ApplyDamage / HitStop のあとで呼ぶ。
        /// これにより最後の一撃の HitStun / Knockback / HitStop は失わない。
        /// </summary>
        public bool TryEnterKnockout()
        {
            if (isKnockedOut)
            {
                return false;
            }

            // 安全側: HP が残っているのに KO しない。
            if (currentHitPoints > 0)
            {
                return false;
            }

            isKnockedOut = true;

            // KO 時点で自分が攻撃中なら中断（P1/P2 共通。大規模キャンセル機構は持たない）。
            if (attackState != null && attackState.IsActionPlaying)
            {
                attackState.InterruptByHit();
            }

            ApplyDisplayColor();
            return true;
        }

        /// <summary>
        /// R Reset 用に KO 旗だけ下ろします。HP 回復は RestoreHitPointsToMaximum。
        /// </summary>
        public void ClearKnockoutForReset()
        {
            isKnockedOut = false;
        }

        /// <summary>
        /// HUD 用: Alive / KO。
        /// </summary>
        public string BuildLifeLabel()
        {
            if (isKnockedOut)
            {
                return "KO";
            }

            return "Alive";
        }

        /// <summary>
        /// 1 Combat Frame 分の HitStun 消費を Participant に依頼します。
        /// Session が HitStop 外の Combat 末尾で1回だけ呼びます。
        /// HitStun が 0 になったとき残ノックバック速度も捨てます（段階13A）。
        /// KO 状態はここでは解除しない（段階14B）。
        /// </summary>
        public void TickHitStunForCombatFrame()
        {
            if (hitState == null)
            {
                return;
            }

            bool wasInCombatReaction = hitState.IsInCombatReaction;
            hitState.TickCombatFrame();

            if (wasInCombatReaction && hitState.IsInCombatReaction == false)
            {
                // 通常 HitStun / ClashRecoil / GuardStun の終了時に残速度を 0 へ。
                // KO 中でもリアクション終了はするが、isKnockedOut は維持する。
                hitState.ClearKnockback();
                ApplyDisplayColor();
            }
        }

        /// <summary>
        /// ノックバック移動直後に、1 Combat Frame 分だけ速度を減速します。
        /// Session が HitStop 外・移動適用後に1回だけ呼びます。
        /// 減速量は呼び出し側（Session）が攻撃データから渡す（段階15）。
        /// </summary>
        public void TickKnockbackVelocityForCombatFrame(float decelerationPerCombatFrame)
        {
            if (hitState == null)
            {
                return;
            }

            float deceleration = decelerationPerCombatFrame;
            if (deceleration < 0f)
            {
                deceleration = 0f;
            }

            hitState.TickKnockbackVelocity(deceleration);
        }

        /// <summary>
        /// HUD 用: Idle / Attack / HitStop / HitStun / ClashRecoil / KO の短い状態名。
        /// KO 中でも最後の一撃の HitStop/HitStun 表示は優先（併存確認用）。
        /// HitStun 終了後は KO を返す。
        /// </summary>
        public string BuildDebugStateLabel(int sharedHitStopRemaining)
        {
            if (hitState != null && hitState.IsInClashRecoil)
            {
                return "ClashRecoil";
            }

            if (hitState != null && hitState.IsInGuardStun)
            {
                if (sharedHitStopRemaining > 0)
                {
                    return "HitStop";
                }

                return "GuardStun";
            }

            if (hitState != null && hitState.IsInHitStun)
            {
                if (sharedHitStopRemaining > 0)
                {
                    return "HitStop";
                }

                return "HitStun";
            }

            if (isKnockedOut)
            {
                return "KO";
            }

            if (attackState != null && attackState.IsActionPlaying)
            {
                return "Attack";
            }

            return "Idle";
        }

        /// <summary>
        /// HitState / AttackState / 表示色 / ノックバック / HP / KO を初期化します（R キー Reset 用）。
        /// 位置と Facing は変えません（現行仕様）。
        /// </summary>
        public void ResetCombatDebugState()
        {
            if (attackState == null)
            {
                attackState = new DebugFighterAttackState();
            }

            attackState.Reset();

            if (hitState == null)
            {
                hitState = new DebugFighterHitState();
            }

            hitState.Reset();
            RestoreHitPointsToMaximum();
            ClearKnockoutForReset();
            ApplyDisplayColor();
        }

        /// <summary>
        /// 参加枠の表示 Tint を SpriteRenderer.color へ1回適用します。
        /// Visual の毎フレーム更新では color を触らない前提です。
        /// </summary>
        public void ApplyDisplayTint()
        {
            ApplyDisplayColor();
        }

        /// <summary>
        /// 表示色の正本を反映します（段階12A / 14B / 15 / Ground Clash）。
        /// 優先: **Clash 専用色 &gt; 被 Hit 表示 &gt; KO 暗色 &gt; 通常 Tint**。
        ///
        /// KO 状態の正本（isKnockedOut）や TryEnterKnockout の呼び出し順は変えない。
        /// 最後の一撃でも HitStun 中は通常 Hit と同じ赤表示にし、
        /// HitStun 終了後の ApplyDisplayColor で KO 暗色へ移る（視覚だけ遅延）。
        /// Attack Sprite 切替は Visual 側。色はここが担当。
        /// </summary>
        public void ApplyDisplayColor()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            // Ground Clash は Damage 0 なので、通常被弾の赤色と分ける。
            // HitStop 中も ClashRecoil 残りは維持されるため専用色のまま止まる。
            if (hitState != null && hitState.IsInClashRecoil)
            {
                spriteRenderer.color = clashRecoilDisplayColor;
                return;
            }

            // 立ちガード硬直は被弾赤と区別する。
            if (hitState != null && hitState.IsInGuardStun)
            {
                spriteRenderer.color = guardStunDisplayColor;
                return;
            }

            // 通常 Hit は従来どおり赤表示。
            if (hitState != null && hitState.IsInHitStun)
            {
                spriteRenderer.color = hitStunDisplayColor;
                return;
            }

            if (isKnockedOut)
            {
                spriteRenderer.color = knockoutDisplayColor;
                return;
            }

            spriteRenderer.color = baseDisplayTint;
        }
    }
}
