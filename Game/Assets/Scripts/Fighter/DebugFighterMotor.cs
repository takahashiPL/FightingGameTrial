using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// Fighter の論理位置・向き・ジャンプ軌道を担当します。
    ///
    /// 責務:
    /// - LogicalX / LogicalY を位置の正本として保持する
    /// - 地上の左右移動（CombatFrame 単位）
    /// - ジャンプ軌道（上昇／頂点／落下／着地）を CombatFrame 単位で進める
    /// - Facing を SpriteRenderer.flipX へ反映する
    /// - Transform へ X/Y を写す（Session から Transform を直書きしない）
    ///
    /// やらないこと:
    /// - 入力 Left/Right から Facing を決めない
    /// - JumpType / Visual State の優先順位を決めない（Session）
    /// - Push 重なり計算をしない
    /// - Rigidbody / Time.deltaTime 物理を使わない
    /// - Sprite 差し替えをしない（Visual の責務）
    ///
    /// なぜ Rigidbody を使わないか:
    /// 攻撃フレーム・HitStop・将来のリプレイと同期する、決定的な 60Hz 進行が正本だからです。
    ///
    /// なぜ LogicalY を正本にするか:
    /// Transform.position.y を各所から直接いじると、接地・Push 飛び越え・見た目がずれやすいためです。
    /// GroundLogicalY は Scene 開始時の足元高さを保存し、着地と Reset の基準にします。
    ///
    /// なぜ Min/Max と保持率か:
    /// キャラごとに短押し／長押しの差を付けつつ、極端な可変ジャンプにしないためです。
    /// 位置を毎フレーム Lerp で追従させず、速度へ反映して自然な上昇／落下にします。
    /// </summary>
    public class DebugFighterMotor : MonoBehaviour
    {
        [Header("移動量（CombatFrame単位・地上）")]
        [Tooltip("CombatFrame が1回進むごとの左右移動量（ワールド単位）です。地上のみ。")]
        [SerializeField]
        private float moveUnitsPerCombatFrame = 0.05f;

        [Tooltip("論理位置 X の左端です。これより左へは行きません。")]
        [SerializeField]
        private float minX = -7f;

        [Tooltip("論理位置 X の右端です。これより右へは行きません。")]
        [SerializeField]
        private float maxX = 7f;

        [Header("ジャンプ設定（キャラごと）")]
        [Tooltip(
            "この Motor（キャラ）のジャンプ性能です。"
            + " P1/P2 で別値にできます。未設定でもコード側で安全な既定値を埋めます。"
        )]
        [SerializeField]
        private FighterJumpSettings jumpSettings = new FighterJumpSettings();

        [Header("見た目")]
        [Tooltip("向き変更に使う SpriteRenderer です。Transform.scale ではなく flipX を使います。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "元画像が右向きなら true、左向きなら false です。"
            + " この値で素材の初期向きを吸収し、SetFacingRight の見た目へ合わせます。"
        )]
        [SerializeField]
        private bool facesRightByDefault = true;

        private float logicalX;
        private float initialLogicalX;

        private float logicalY;
        private float groundLogicalY;
        private float initialLogicalY;

        private float verticalVelocity;
        private float horizontalJumpVelocity;

        private bool isGrounded = true;
        private FighterJumpType currentJumpType = FighterJumpType.None;

        private int jumpElapsedFrames;
        private int jumpHeldFrames;
        private int directionHeldFrames;
        private int landingFramesRemaining;

        /// <summary>
        /// ジャンプ開始時に確定した水平符号（+1=右、-1=左）。着地まで種類を反転しません。
        /// </summary>
        private float jumpHorizontalSign;

        private bool facingRight = true;

        // ------------------------------------------------------------
        // ジャンプ計測（短押し／長押し比較用。毎 CombatFrame ログは出さない）
        // Session が slot 付きで Debug.Log するため、Motor は数値とイベント旗のみ保持する。
        // ------------------------------------------------------------
        private float jumpStartLogicalX;
        private float jumpStartLogicalY;
        private float jumpMaximumLogicalY;
        private bool jumpApexLogged;

        /// <summary>
        /// Apex 通過後の CombatFrame 数（通過フレームは 0）。通過前は -1。
        /// 見た目の JumpApex 表示に使うだけで、軌道計算には使いません。
        /// </summary>
        private int framesSinceJumpApex = -1;

        private bool pendingJumpStartedEvent;
        private bool pendingJumpApexEvent;
        private bool pendingJumpLandedEvent;

        private FighterJumpType debugStartedJumpType;
        private float debugStartedX;
        private float debugStartedY;
        private bool debugStartedFacingRight;

        private FighterJumpType debugApexJumpType;
        private int debugApexElapsedFrames;
        private int debugApexHeldFrames;
        private int debugApexDirectionHeldFrames;
        private float debugApexHeight;
        private float debugApexXDistance;

        private FighterJumpType debugLandedJumpType;
        private int debugLandedTotalFrames;
        private int debugLandedHeldFrames;
        private int debugLandedDirectionHeldFrames;
        private float debugLandedMaxHeight;
        private float debugLandedHorizontalDistance;
        private float debugLandedStartX;
        private float debugLandedEndX;
        private bool debugLandedFacingRight;

        public float LogicalX
        {
            get { return logicalX; }
        }

        public float LogicalY
        {
            get { return logicalY; }
        }

        public float GroundLogicalY
        {
            get { return groundLogicalY; }
        }

        public float VerticalVelocity
        {
            get { return verticalVelocity; }
        }

        public float HorizontalJumpVelocity
        {
            get { return horizontalJumpVelocity; }
        }

        public bool IsGrounded
        {
            get { return isGrounded; }
        }

        public FighterJumpType CurrentJumpType
        {
            get { return currentJumpType; }
        }

        public bool IsRising
        {
            get { return isGrounded == false && verticalVelocity > 0f; }
        }

        public bool IsFalling
        {
            get { return isGrounded == false && verticalVelocity <= 0f; }
        }

        public bool IsLanding
        {
            get { return isGrounded && landingFramesRemaining > 0; }
        }

        public int JumpElapsedFrames
        {
            get { return jumpElapsedFrames; }
        }

        public int JumpHeldFrames
        {
            get { return jumpHeldFrames; }
        }

        public int DirectionHeldFrames
        {
            get { return directionHeldFrames; }
        }

        public int LandingFramesRemaining
        {
            get { return landingFramesRemaining; }
        }

        /// <summary>
        /// このジャンプで頂点（上昇→非上昇）を一度でも通過したか。軌道計算は変えません。
        /// </summary>
        public bool HasPassedJumpApex
        {
            get { return jumpApexLogged; }
        }

        /// <summary>
        /// Apex 通過からの CombatFrame 数（通過フレームは 0）。未通過は -1。
        /// JumpApex Visual 用。軌道計算には使いません。
        /// </summary>
        public int FramesSinceJumpApex
        {
            get { return framesSinceJumpApex; }
        }

        public bool FacingRight
        {
            get { return facingRight; }
        }

        /// <summary>
        /// この Motor のジャンプ設定（読み取り）。P1/P2 個別調整の入口です。
        /// </summary>
        public FighterJumpSettings JumpSettings
        {
            get
            {
                if (jumpSettings == null)
                {
                    jumpSettings = new FighterJumpSettings();
                }

                jumpSettings.EnsureValidOrFillDefaults();
                return jumpSettings;
            }
        }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                Debug.LogError("DebugFighterMotor: SpriteRenderer が未設定です。");
            }

            if (jumpSettings == null)
            {
                jumpSettings = new FighterJumpSettings();
            }

            jumpSettings.EnsureValidOrFillDefaults();

            // Scene 配置を論理位置の初期値・接地基準にする
            logicalX = transform.position.x;
            initialLogicalX = logicalX;
            logicalY = transform.position.y;
            groundLogicalY = logicalY;
            initialLogicalY = logicalY;

            isGrounded = true;
            currentJumpType = FighterJumpType.None;
            verticalVelocity = 0f;
            horizontalJumpVelocity = 0f;
            jumpElapsedFrames = 0;
            jumpHeldFrames = 0;
            directionHeldFrames = 0;
            landingFramesRemaining = 0;
            jumpHorizontalSign = 0f;

            facingRight = true;
            ApplyFacingToSprite();
            ApplyLogicalPositionToTransform();
        }

        /// <summary>
        /// Training Reset: 論理 X/Y とジャンプ状態を Scene 開始時へ戻します。
        /// Facing は Session が位置復帰後に再適用します。
        /// </summary>
        public void ResetLogicalPositionAndJumpToInitial()
        {
            ClearJumpRuntimeState();
            SetLogicalX(initialLogicalX);
            logicalY = initialLogicalY;
            groundLogicalY = initialLogicalY;
            isGrounded = true;
            ApplyLogicalPositionToTransform();
        }

        /// <summary>
        /// 互換: 論理 X のみ初期値へ（Y/ジャンプは触らない）。通常は ResetLogicalPositionAndJumpToInitial を使う。
        /// </summary>
        public void ResetLogicalXToInitial()
        {
            SetLogicalX(initialLogicalX);
        }

        /// <summary>
        /// 地上の 1 CombatFrame 左右移動。空中では呼びません（Session 側で分岐）。
        /// Landing 中も左右移動は許可します（再ジャンプは Session が Landing 終了後のみ）。
        /// </summary>
        public void ProcessOneCombatFrame(SimulationInputState input)
        {
            if (isGrounded == false)
            {
                ApplyLogicalPositionToTransform();
                return;
            }

            if (input == null)
            {
                TickLandingFramesOnGround();
                ApplyLogicalPositionToTransform();
                return;
            }

            bool left = input.Left;
            bool right = input.Right;

            if (left && right)
            {
                // 移動なし
            }
            else if (right)
            {
                logicalX = logicalX + moveUnitsPerCombatFrame;
            }
            else if (left)
            {
                logicalX = logicalX - moveUnitsPerCombatFrame;
            }

            ClampLogicalX();
            TickLandingFramesOnGround();
            ApplyLogicalPositionToTransform();
        }

        /// <summary>
        /// ジャンプ開始を試みます。地上・非 Landing・JumpType 有効時のみ成功。
        /// </summary>
        public bool TryStartJump(FighterJumpType jumpType)
        {
            if (jumpType == FighterJumpType.None)
            {
                return false;
            }

            if (isGrounded == false)
            {
                return false;
            }

            if (landingFramesRemaining > 0)
            {
                return false;
            }

            FighterJumpSettings settings = JumpSettings;
            JumpArcSettings arc = settings.GetArc(jumpType);
            arc.Sanitize();

            currentJumpType = jumpType;
            isGrounded = false;
            landingFramesRemaining = 0;
            jumpElapsedFrames = 0;
            jumpHeldFrames = 0;
            directionHeldFrames = 0;
            logicalY = groundLogicalY;

            // 開始は Min 弧の速度。保持に応じて後から Max 寄りへ伸ばす。
            float duration = arc.MinDurationFrames;
            float height = arc.MinHeight;
            verticalVelocity = ComputeInitialVerticalVelocity(height, duration);

            jumpHorizontalSign = 0f;
            horizontalJumpVelocity = 0f;
            if (jumpType == FighterJumpType.Forward)
            {
                jumpHorizontalSign = facingRight ? 1f : -1f;
                horizontalJumpVelocity =
                    jumpHorizontalSign * (arc.MinHorizontalDistance / duration);
            }
            else if (jumpType == FighterJumpType.Backward)
            {
                jumpHorizontalSign = facingRight ? -1f : 1f;
                horizontalJumpVelocity =
                    jumpHorizontalSign * (arc.MinHorizontalDistance / duration);
            }

            // 計測開始位置（短押し／長押しの高さ・距離比較用）
            jumpStartLogicalX = logicalX;
            jumpStartLogicalY = logicalY;
            jumpMaximumLogicalY = logicalY;
            jumpApexLogged = false;
            framesSinceJumpApex = -1;

            // Session が slot 付きログを出すための開始イベント（文字列は作らない）
            debugStartedJumpType = jumpType;
            debugStartedX = logicalX;
            debugStartedY = logicalY;
            debugStartedFacingRight = facingRight;
            pendingJumpStartedEvent = true;
            pendingJumpApexEvent = false;
            pendingJumpLandedEvent = false;

            ApplyLogicalPositionToTransform();
            return true;
        }

        /// <summary>
        /// 空中の 1 CombatFrame 分の軌道を進めます。
        ///
        /// 保持入力は速度へ反映し、LogicalY を直接 Lerp しません。
        /// JumpType は着地まで変更しません（逆入力は弱い空中制御のみ）。
        /// </summary>
        public void ProcessOneAirborneCombatFrame(SimulationInputState input)
        {
            if (isGrounded)
            {
                ApplyLogicalPositionToTransform();
                return;
            }

            FighterJumpSettings settings = JumpSettings;
            JumpArcSettings arc = settings.GetArc(currentJumpType);
            arc.Sanitize();

            bool upHeld = false;
            bool left = false;
            bool right = false;
            if (input != null)
            {
                upHeld = input.Up;
                left = input.Left;
                right = input.Right;
            }

            jumpElapsedFrames = jumpElapsedFrames + 1;

            // 上昇中のみ上保持をカウント（頂点後の再押下では高くしない）
            if (verticalVelocity > 0f && upHeld)
            {
                if (jumpHeldFrames < settings.JumpHoldFramesToMax)
                {
                    jumpHeldFrames = jumpHeldFrames + 1;
                }
            }

            UpdateDirectionHeldFrames(settings, left, right);

            float jumpHoldRate = 0f;
            if (settings.JumpHoldFramesToMax > 0)
            {
                jumpHoldRate = Mathf.Clamp01(
                    (float)jumpHeldFrames / (float)settings.JumpHoldFramesToMax
                );
            }

            float directionHoldRate = 0f;
            if (settings.DirectionHoldFramesToMax > 0)
            {
                directionHoldRate = Mathf.Clamp01(
                    (float)directionHeldFrames / (float)settings.DirectionHoldFramesToMax
                );
            }

            float targetHeight = Mathf.Lerp(arc.MinHeight, arc.MaxHeight, jumpHoldRate);
            float targetDuration = Mathf.Lerp(
                arc.MinDurationFrames,
                arc.MaxDurationFrames,
                jumpHoldRate
            );
            float targetDistance = Mathf.Lerp(
                arc.MinHorizontalDistance,
                arc.MaxHorizontalDistance,
                directionHoldRate
            );

            if (targetDuration < 4f)
            {
                targetDuration = 4f;
            }

            float gravity = ComputeGravity(targetHeight, targetDuration);

            // 上昇中に上を保持している間は重力を弱め、Min→Max へ滑らかに伸ばす
            if (verticalVelocity > 0f && upHeld && jumpHeldFrames < settings.JumpHoldFramesToMax)
            {
                gravity = gravity * 0.55f;
            }

            // 頂点検出: 速度更新前が上昇、更新後が非上昇になった CombatFrame のみ1回
            // （毎フレームログにせず、短押し／長押しの頂点高度を比較するため）
            bool wasRising = verticalVelocity > 0f;
            bool apexAlreadyLogged = jumpApexLogged;
            verticalVelocity = verticalVelocity - gravity;
            logicalY = logicalY + verticalVelocity;

            if (logicalY > jumpMaximumLogicalY)
            {
                jumpMaximumLogicalY = logicalY;
            }

            if (wasRising && verticalVelocity <= 0f && jumpApexLogged == false)
            {
                jumpApexLogged = true;
                framesSinceJumpApex = 0;
                debugApexJumpType = currentJumpType;
                debugApexElapsedFrames = jumpElapsedFrames;
                debugApexHeldFrames = jumpHeldFrames;
                debugApexDirectionHeldFrames = directionHeldFrames;
                debugApexHeight = jumpMaximumLogicalY - jumpStartLogicalY;
                debugApexXDistance = Mathf.Abs(logicalX - jumpStartLogicalX);
                pendingJumpApexEvent = true;
            }
            else if (apexAlreadyLogged)
            {
                // 見た目用: Apex 通過後の経過だけ数える（軌道式は変更しない）
                if (framesSinceJumpApex < int.MaxValue)
                {
                    framesSinceJumpApex = framesSinceJumpApex + 1;
                }
            }

            UpdateHorizontalJumpVelocity(
                settings,
                arc,
                targetDistance,
                targetDuration,
                left,
                right
            );
            logicalX = logicalX + horizontalJumpVelocity;
            ClampLogicalX();

            if (logicalY <= groundLogicalY)
            {
                FinishLanding(settings);
            }

            ApplyLogicalPositionToTransform();
        }

        public void SetFacingRight(bool faceRight)
        {
            facingRight = faceRight;
            ApplyFacingToSprite();
        }

        public void SetLogicalX(float newX)
        {
            logicalX = newX;
            ClampLogicalX();
            ApplyLogicalPositionToTransform();
        }

        public float TryMoveLogicalXBy(float deltaX)
        {
            float beforeX = logicalX;
            SetLogicalX(logicalX + deltaX);
            return logicalX - beforeX;
        }

        /// <summary>
        /// 飛び越え判定用: 地上からの高さ。
        /// </summary>
        public float HeightAboveGround
        {
            get { return logicalY - groundLogicalY; }
        }

        private void UpdateDirectionHeldFrames(
            FighterJumpSettings settings,
            bool left,
            bool right)
        {
            if (currentJumpType == FighterJumpType.None
                || currentJumpType == FighterJumpType.Neutral)
            {
                return;
            }

            if (left && right)
            {
                return;
            }

            bool sameDirectionHeld = false;
            if (jumpHorizontalSign > 0f)
            {
                sameDirectionHeld = right && left == false;
            }
            else if (jumpHorizontalSign < 0f)
            {
                sameDirectionHeld = left && right == false;
            }

            if (sameDirectionHeld)
            {
                if (directionHeldFrames < settings.DirectionHoldFramesToMax)
                {
                    directionHeldFrames = directionHeldFrames + 1;
                }
            }
        }

        private void UpdateHorizontalJumpVelocity(
            FighterJumpSettings settings,
            JumpArcSettings arc,
            float targetDistance,
            float targetDuration,
            bool left,
            bool right)
        {
            if (currentJumpType == FighterJumpType.Neutral)
            {
                // Neutral は大きな前後移動なし。逆入力でもごく弱い減衰のみ。
                if (left && right == false)
                {
                    horizontalJumpVelocity =
                        horizontalJumpVelocity - settings.ReverseAirControlPerFrame;
                }
                else if (right && left == false)
                {
                    horizontalJumpVelocity =
                        horizontalJumpVelocity + settings.ReverseAirControlPerFrame;
                }

                // 微調整の上限（意図的な大移動にしない）
                float microCap = 0.04f;
                if (horizontalJumpVelocity > microCap)
                {
                    horizontalJumpVelocity = microCap;
                }

                if (horizontalJumpVelocity < -microCap)
                {
                    horizontalJumpVelocity = -microCap;
                }

                return;
            }

            float desiredSpeed = 0f;
            if (targetDuration > 0f)
            {
                desiredSpeed = targetDistance / targetDuration;
            }

            float desiredVelocity = jumpHorizontalSign * desiredSpeed;

            // 目標速度へ近づける（位置の直接補正はしない）
            float blend = 0.35f;
            horizontalJumpVelocity =
                horizontalJumpVelocity
                + (desiredVelocity - horizontalJumpVelocity) * blend;

            // 逆方向は種類を反転せず、弱い空中制御のみ
            bool reverseHeld = false;
            if (jumpHorizontalSign > 0f)
            {
                reverseHeld = left && right == false;
            }
            else if (jumpHorizontalSign < 0f)
            {
                reverseHeld = right && left == false;
            }

            if (reverseHeld)
            {
                horizontalJumpVelocity =
                    horizontalJumpVelocity
                    - Mathf.Sign(jumpHorizontalSign) * settings.ReverseAirControlPerFrame;
            }

            // Max 距離相当を大きく超えないよう速度を抑える
            float maxSpeed = 0f;
            if (targetDuration > 0f)
            {
                maxSpeed = arc.MaxHorizontalDistance / targetDuration;
            }

            if (maxSpeed < 0f)
            {
                maxSpeed = 0f;
            }

            if (horizontalJumpVelocity > maxSpeed)
            {
                horizontalJumpVelocity = maxSpeed;
            }

            if (horizontalJumpVelocity < -maxSpeed)
            {
                horizontalJumpVelocity = -maxSpeed;
            }
        }

        private void FinishLanding(FighterJumpSettings settings)
        {
            // CurrentJumpType を None にする前に完了値を保存（着地ログ用）
            debugLandedJumpType = currentJumpType;
            debugLandedTotalFrames = jumpElapsedFrames;
            debugLandedHeldFrames = jumpHeldFrames;
            debugLandedDirectionHeldFrames = directionHeldFrames;
            debugLandedMaxHeight = jumpMaximumLogicalY - jumpStartLogicalY;
            debugLandedHorizontalDistance = Mathf.Abs(logicalX - jumpStartLogicalX);
            debugLandedStartX = jumpStartLogicalX;
            debugLandedEndX = logicalX;
            debugLandedFacingRight = facingRight;
            pendingJumpLandedEvent = true;

            logicalY = groundLogicalY;
            verticalVelocity = 0f;
            horizontalJumpVelocity = 0f;
            isGrounded = true;
            currentJumpType = FighterJumpType.None;
            jumpElapsedFrames = 0;
            jumpHeldFrames = 0;
            directionHeldFrames = 0;
            jumpHorizontalSign = 0f;
            jumpApexLogged = false;
            framesSinceJumpApex = -1;
            landingFramesRemaining = settings.LandingFrames;
            if (landingFramesRemaining < 1)
            {
                landingFramesRemaining = 1;
            }
        }

        private void TickLandingFramesOnGround()
        {
            if (landingFramesRemaining > 0)
            {
                landingFramesRemaining = landingFramesRemaining - 1;
                if (landingFramesRemaining < 0)
                {
                    landingFramesRemaining = 0;
                }
            }
        }

        private void ClearJumpRuntimeState()
        {
            verticalVelocity = 0f;
            horizontalJumpVelocity = 0f;
            isGrounded = true;
            currentJumpType = FighterJumpType.None;
            jumpElapsedFrames = 0;
            jumpHeldFrames = 0;
            directionHeldFrames = 0;
            landingFramesRemaining = 0;
            jumpHorizontalSign = 0f;
            ClearJumpDebugMeasurementState();
        }

        /// <summary>
        /// Training Reset / ジャンプ終了時に計測と未消費イベントを破棄します。
        /// Reset 直後に古い apex / landed ログが出ないようにするためです。
        /// </summary>
        private void ClearJumpDebugMeasurementState()
        {
            jumpStartLogicalX = 0f;
            jumpStartLogicalY = 0f;
            jumpMaximumLogicalY = 0f;
            jumpApexLogged = false;
            framesSinceJumpApex = -1;
            pendingJumpStartedEvent = false;
            pendingJumpApexEvent = false;
            pendingJumpLandedEvent = false;
            debugStartedJumpType = FighterJumpType.None;
            debugApexJumpType = FighterJumpType.None;
            debugLandedJumpType = FighterJumpType.None;
        }

        /// <summary>
        /// ジャンプ開始イベントを1回だけ取り出します（Session の slot 付きログ用）。
        /// </summary>
        public bool TryConsumeJumpStartedDebug(
            out FighterJumpType jumpType,
            out float startX,
            out float startY,
            out bool facingRightAtStart)
        {
            jumpType = FighterJumpType.None;
            startX = 0f;
            startY = 0f;
            facingRightAtStart = false;

            if (pendingJumpStartedEvent == false)
            {
                return false;
            }

            pendingJumpStartedEvent = false;
            jumpType = debugStartedJumpType;
            startX = debugStartedX;
            startY = debugStartedY;
            facingRightAtStart = debugStartedFacingRight;
            return true;
        }

        /// <summary>
        /// 頂点到達イベントを1回だけ取り出します。
        /// </summary>
        public bool TryConsumeJumpApexDebug(
            out FighterJumpType jumpType,
            out int elapsedFrames,
            out int heldFrames,
            out int directionHeld,
            out float height,
            out float xDistance)
        {
            jumpType = FighterJumpType.None;
            elapsedFrames = 0;
            heldFrames = 0;
            directionHeld = 0;
            height = 0f;
            xDistance = 0f;

            if (pendingJumpApexEvent == false)
            {
                return false;
            }

            pendingJumpApexEvent = false;
            jumpType = debugApexJumpType;
            elapsedFrames = debugApexElapsedFrames;
            heldFrames = debugApexHeldFrames;
            directionHeld = debugApexDirectionHeldFrames;
            height = debugApexHeight;
            xDistance = debugApexXDistance;
            return true;
        }

        /// <summary>
        /// 着地完了イベントを1回だけ取り出します。
        /// </summary>
        public bool TryConsumeJumpLandedDebug(
            out FighterJumpType jumpType,
            out int totalFrames,
            out int heldFrames,
            out int directionHeld,
            out float maxHeight,
            out float horizontalDistance,
            out float startX,
            out float endX,
            out bool facingRightAtLand)
        {
            jumpType = FighterJumpType.None;
            totalFrames = 0;
            heldFrames = 0;
            directionHeld = 0;
            maxHeight = 0f;
            horizontalDistance = 0f;
            startX = 0f;
            endX = 0f;
            facingRightAtLand = false;

            if (pendingJumpLandedEvent == false)
            {
                return false;
            }

            pendingJumpLandedEvent = false;
            jumpType = debugLandedJumpType;
            totalFrames = debugLandedTotalFrames;
            heldFrames = debugLandedHeldFrames;
            directionHeld = debugLandedDirectionHeldFrames;
            maxHeight = debugLandedMaxHeight;
            horizontalDistance = debugLandedHorizontalDistance;
            startX = debugLandedStartX;
            endX = debugLandedEndX;
            facingRightAtLand = debugLandedFacingRight;
            return true;
        }

        private void ClampLogicalX()
        {
            if (logicalX < minX)
            {
                logicalX = minX;
            }

            if (logicalX > maxX)
            {
                logicalX = maxX;
            }
        }

        /// <summary>
        /// 対称放物線近似: 頂点高さ H・総時間 D から初速を求める（CombatFrame 単位）。
        /// v0 = 4H/D、g = 8H/D^2。
        /// </summary>
        private static float ComputeInitialVerticalVelocity(float height, float durationFrames)
        {
            if (durationFrames < 4f)
            {
                durationFrames = 4f;
            }

            if (height < 0.1f)
            {
                height = 0.1f;
            }

            return (4f * height) / durationFrames;
        }

        private static float ComputeGravity(float height, float durationFrames)
        {
            if (durationFrames < 4f)
            {
                durationFrames = 4f;
            }

            if (height < 0.1f)
            {
                height = 0.1f;
            }

            return (8f * height) / (durationFrames * durationFrames);
        }

        private void ApplyFacingToSprite()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            bool shouldFlipX;
            if (facesRightByDefault)
            {
                shouldFlipX = (facingRight == false);
            }
            else
            {
                shouldFlipX = facingRight;
            }

            spriteRenderer.flipX = shouldFlipX;
        }

        /// <summary>
        /// 論理 X/Y を Transform へ写します。Z は変更しません。
        /// </summary>
        private void ApplyLogicalPositionToTransform()
        {
            Vector3 position = transform.position;
            position.x = logicalX;
            position.y = logicalY;
            transform.position = position;
        }
    }
}
