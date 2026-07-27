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
    /// - ノックバック初速・減速量の暫定値を持つ（段階13A。攻撃データ化前）
    /// - Participant 共通の最大HP・現在HPを所有する（段階14A。KO 遷移はまだしない）
    /// - 横方向 Push Box 半幅を持つ（段階10B-3。重なり解消の計算に使う）
    /// - Push / Hurt / Hit Box のローカル定義を持ち、World Box を計算する（段階11A）
    ///
    /// やらないこと:
    /// - Update で移動しない
    /// - Keyboard.current を直接読まない
    /// - SimulationTick を回さない
    /// - 相手を検索しない（Inspector で opponent を接続する）
    /// - 攻撃開始・ActionFrame進行・Hit判定を自分で回さない（状態の所有のみ。進行は Session）
    /// - attacker / defender の選択や Hit 成立判定をしない（Session の責務）
    /// - Push 重なり解消を自分で回さない（Session が DebugFighterPushResolver を呼ぶ）
    /// - Transform へノックバックを直接書かない（Motor.SetLogicalX 経由。Session が呼ぶ）
    /// - KO / Round 終了 / 勝敗判定をしない（段階14A は HP 減算のみ）
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
    /// 攻撃と被弾（段階10B-2 / 12A / 13A / 14A）:
    /// Participant は自分の AttackState・HitState・HP を所有します。
    /// SimulationSession が attacker / defender を選び Hit を解決し、
    /// 成立時に defender.ReceiveHit と defender.ApplyDamage を呼びます。
    ///
    /// HP（段階14A）:
    /// 正本はここ（HitState には持たせない）。0HP でも今回は KO せず戦闘継続する暫定状態。
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
        /// <summary>
        /// Jパンチ Hit Box を表示する Active 区間です。
        /// SimulationSession の距離 Hit 用 Active（4〜6）と揃えます。
        /// 可視化側の独自攻撃状態は持たず、AttackState.ActionFrame だけを参照します。
        /// </summary>
        private const int JPunchHitBoxActiveStartFrame = 4;

        private const int JPunchHitBoxActiveEndFrame = 6;
        [Header("参加枠（キャラ種類ではない）")]
        [Tooltip("デバッグ用の参加枠 ID です。キャラクター種類の選択ではありません。")]
        [SerializeField]
        private DebugFighterSlotId slotId = DebugFighterSlotId.P1;

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("この参加者の移動・Facing を持つ DebugFighterMotor です。")]
        [SerializeField]
        private DebugFighterMotor motor;

        [Tooltip("この参加者の Idle/Attack Sprite 切替を持つ DebugFighterVisual です。")]
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

        [Header("入力")]
        [Tooltip(
            "true なら Session が DebugGameplayInput 由来の入力を渡します。"
            + " false なら Session が Neutral 入力（全 false）を渡します（棒立ち）。"
        )]
        [SerializeField]
        private bool usesGameplayInput = true;

        [Header("HitStun（段階12A・暫定）")]
        [Tooltip(
            "被 Hit 後、HitStop 終了から行動不能となる Combat Frame 数です。"
            + " 攻撃データ化前の暫定値。HP は扱いません。"
        )]
        [SerializeField]
        private int hitStunFrames = 12;

        [Header("ノックバック（段階13A・暫定）")]
        [Tooltip(
            "HitStop 終了後、最初の Combat Frame で適用する横移動量の絶対値です。"
            + " 符号は Session が LogicalX 比較で決めます。Time.deltaTime は使いません。"
            + " 攻撃データ化前の暫定値。Y 方向・壁処理はしません。"
        )]
        [SerializeField]
        private float knockbackInitialSpeed = 0.18f;

        [Tooltip(
            "1 Combat Frame ごとにノックバック速度を 0 へ近づける量です。"
            + " Combat Frame 単位。実時間（deltaTime）には掛けません。"
        )]
        [SerializeField]
        private float knockbackDeceleration = 0.015f;

        [Header("HP（段階14A・暫定）")]
        [Tooltip(
            "最大 Hit Points です。攻撃データ化・キャラ固有化前の暫定値。"
            + " 現在HPは実行時状態で、Inspector からは編集しません。"
            + " 0HP でも今回は KO 遷移せず戦闘を継続します（段階14A 限定の暫定）。"
        )]
        [SerializeField]
        private int maxHitPoints = 100;

        /// <summary>
        /// 現在 HP（実行時状態の正本）。Awake / Reset で最大へ戻す。
        /// Inspector 編集対象にしない（SerializeField にしない）。
        /// </summary>
        private int currentHitPoints;

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
            "Jパンチ Hit Box のローカル定義です。"
            + " Facing Right 基準の Local Center X。Left のときは X だけ符号反転します。"
            + " Active 中だけ IsActive。距離 Hit 判定にはまだ使いません。"
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
        /// 現在 HP が 0 以下か。
        /// 段階14A では KO 遷移には使わない（導出値の用意のみ）。
        /// </summary>
        public bool IsHitPointsDepleted
        {
            get { return currentHitPoints <= 0; }
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

            // 段階14A: HP 正本は Participant。開始時は最大へ。
            RestoreHitPointsToMaximum();

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

            if (jPunchHitBoxLocal == null)
            {
                jPunchHitBoxLocal = new DebugBox2D(0.75f, 1.25f, 0.55f, 0.35f, false);
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
        /// Jパンチ Hit Box の World 座標を返します（段階11A）。
        ///
        /// Facing 反転の考え方:
        /// - ローカル定義は Facing Right（相手が右）基準
        /// - Facing Left のときは Local Center X の符号だけ反転する
        /// - SpriteRenderer.flipX / Scale 反転には依存しない（二重反転を防ぐ）
        ///
        /// Active 連動:
        /// AttackState が Jパンチ再生中かつ ActionFrame が 4〜6 のときだけ IsActive。
        /// Startup / Recovery / Idle では非表示。距離 Hit 判定自体は変更しない。
        /// </summary>
        public DebugBox2D EvaluateWorldHitBox()
        {
            EnsureLocalBoxDefaults();

            DebugBox2D world = new DebugBox2D();
            float originX;
            float originY;
            GetBoxOrigin(out originX, out originY);

            bool facingRight = true;
            if (motor != null)
            {
                facingRight = motor.FacingRight;
            }

            // Facing Right: +LocalX / Facing Left: -LocalX（Y は反転しない）
            float localCenterX = jPunchHitBoxLocal.CenterX;
            if (facingRight == false)
            {
                localCenterX = -localCenterX;
            }

            float halfWidth = jPunchHitBoxLocal.HalfWidth;
            float halfHeight = jPunchHitBoxLocal.HalfHeight;
            if (halfWidth < 0f)
            {
                halfWidth = 0f;
            }

            if (halfHeight < 0f)
            {
                halfHeight = 0f;
            }

            bool isActive = IsJPunchHitBoxActiveNow();

            world.Set(
                originX + localCenterX,
                originY + jPunchHitBoxLocal.CenterY,
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
        /// いま Jパンチ Hit Box を有効表示すべきか。
        /// AttackState を正本とし、可視化独自の攻撃進行は持たない。
        /// </summary>
        public bool IsJPunchHitBoxActiveNow()
        {
            if (attackState == null)
            {
                return false;
            }

            if (attackState.IsJPunchAttack == false)
            {
                return false;
            }

            if (attackState.IsActionPlaying == false)
            {
                return false;
            }

            int frame = attackState.ActionFrame;
            if (frame < JPunchHitBoxActiveStartFrame)
            {
                return false;
            }

            if (frame > JPunchHitBoxActiveEndFrame)
            {
                return false;
            }

            return true;
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
        /// Damage を適用し、実際に減った HP 量を返します（段階14A）。
        ///
        /// 何をするか:
        /// - damage &lt;= 0 なら何もしない（戻り値 0）
        /// - currentHP = max(0, currentHP - damage)
        /// - 実際に減った量（要求と実減の小さい方）を返す
        ///
        /// なぜ必要か:
        /// 有効 Hit 成立時に Session が1回だけ呼ぶ共通口。HitState には HP を持たせない。
        ///
        /// 暫定仕様（段階14A）:
        /// HP が 0 になっても KO 状態へ遷移しない。移動・攻撃・HitStun は既存どおり継続する。
        /// 「0HP だが戦闘継続」は 14A 限定の学習用暫定状態。
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
        /// </summary>
        public void RestoreHitPointsToMaximum()
        {
            currentHitPoints = MaxHitPoints;
        }

        /// <summary>
        /// 1 Combat Frame 分の HitStun 消費を Participant に依頼します。
        /// Session が HitStop 外の Combat 末尾で1回だけ呼びます。
        /// HitStun が 0 になったとき残ノックバック速度も捨てます（段階13A）。
        /// </summary>
        public void TickHitStunForCombatFrame()
        {
            if (hitState == null)
            {
                return;
            }

            bool wasInHitStun = hitState.IsInHitStun;
            hitState.TickCombatFrame();

            if (wasInHitStun && hitState.IsInHitStun == false)
            {
                // HitStun 終了時に残速度を 0 へ（ノックバックは Stun 中だけ適用）。
                hitState.ClearKnockback();
                ApplyDisplayColor();
            }
        }

        /// <summary>
        /// ノックバック移動直後に、1 Combat Frame 分だけ速度を減速します。
        /// Session が HitStop 外・移動適用後に1回だけ呼びます。
        /// </summary>
        public void TickKnockbackVelocityForCombatFrame()
        {
            if (hitState == null)
            {
                return;
            }

            hitState.TickKnockbackVelocity(KnockbackDeceleration);
        }

        /// <summary>
        /// HUD 用: Idle / Attack / HitStop / HitStun の短い状態名。
        /// HitStop は共有残りが >0 かつ自分が HitStun 中のとき。
        /// </summary>
        public string BuildDebugStateLabel(int sharedHitStopRemaining)
        {
            if (hitState != null && hitState.IsInHitStun)
            {
                if (sharedHitStopRemaining > 0)
                {
                    return "HitStop";
                }

                return "HitStun";
            }

            if (attackState != null && attackState.IsActionPlaying)
            {
                return "Attack";
            }

            return "Idle";
        }

        /// <summary>
        /// HitState / AttackState / 表示色 / ノックバック速度 / HP を初期化します（R キー Reset 用）。
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
        /// 表示色の正本を反映します（段階12A）。
        /// 優先: HitStun/HitStop 見た目（IsInHitStun） > 通常 Tint。
        /// Attack Sprite 切替は Visual 側。色はここが担当。
        /// </summary>
        public void ApplyDisplayColor()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (hitState != null && hitState.IsInHitStun)
            {
                spriteRenderer.color = hitStunDisplayColor;
                return;
            }

            spriteRenderer.color = baseDisplayTint;
        }
    }
}
