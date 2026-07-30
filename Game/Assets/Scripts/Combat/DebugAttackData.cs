using System;
using FightingGameTrial.Fighter;

namespace FightingGameTrial.Combat
{
    /// <summary>
    /// 1つの攻撃の「設定値」です（段階15）。
    ///
    /// 攻撃データと攻撃進行処理の違い:
    /// - 本クラス … Startup/Active/Recovery・Damage・HitStop・HitStun・KB・Hit Box など、技の固定設定
    /// - DebugFighterAttackState … 今この機体が何フレーム目か、Hit 済みか、という進行状態
    ///
    /// local Hit Box と world Hit Box:
    /// - ここは Facing Right 基準のローカル定義（Center / Half）だけを持つ
    /// - World 変換・Facing 反転・原点計算は Participant.EvaluateWorldHitBox の責務
    ///
    /// Frame 区間の考え方（ActionFrame）:
    /// Start 直後は AF=0。Combat ごとに +1。
    /// Startup = 1〜StartupFrames、Active = その直後の ActiveFrames 分、
    /// Recovery = Active 終了翌〜TotalFrames。AF &gt;= TotalFrames で攻撃終了。
    ///
    /// 将来の複数攻撃:
    /// Session が開始時に DebugAttackData を選ぶ。現在は JPunch と GroundKick の静的2本。
    /// 将来の空中 Kick は別 ID / 別インスタンスとして追加する（GroundKick と混ぜない）。
    /// </summary>
    public sealed class DebugAttackData
    {
        /// <summary>
        /// J Punch の設定正本（1回だけ生成。毎 Frame / 毎 Hit で new しない）。
        /// </summary>
        public static readonly DebugAttackData JPunch = CreateJPunch();

        /// <summary>
        /// 地上通常 Kick の設定正本（仮値。Editor 確認後にここだけ直す）。
        /// </summary>
        public static readonly DebugAttackData Kick = CreateGroundKick();

        private readonly DebugAttackId attackId;
        private readonly string attackIdLabel;
        private readonly int startupFrames;
        private readonly int activeFrames;
        private readonly int recoveryFrames;
        private readonly int damage;
        private readonly int hitStopFrames;
        private readonly int hitStunFrames;
        private readonly float knockbackInitialVelocityX;
        private readonly float knockbackDecelerationPerCombatFrame;
        private readonly float hitBoxLocalCenterX;
        private readonly float hitBoxLocalCenterY;
        private readonly float hitBoxHalfWidth;
        private readonly float hitBoxHalfHeight;

        private DebugAttackData(
            DebugAttackId attackId,
            string attackIdLabel,
            int startupFrames,
            int activeFrames,
            int recoveryFrames,
            int damage,
            int hitStopFrames,
            int hitStunFrames,
            float knockbackInitialVelocityX,
            float knockbackDecelerationPerCombatFrame,
            float hitBoxLocalCenterX,
            float hitBoxLocalCenterY,
            float hitBoxHalfWidth,
            float hitBoxHalfHeight)
        {
            if (attackId == DebugAttackId.None)
            {
                throw new ArgumentException("AttackId が None です。", "attackId");
            }

            if (string.IsNullOrEmpty(attackIdLabel))
            {
                throw new ArgumentException("AttackIdLabel が空です。", "attackIdLabel");
            }

            if (startupFrames < 0
                || activeFrames < 1
                || recoveryFrames < 0
                || damage < 0
                || hitStopFrames < 0
                || hitStunFrames < 0
                || knockbackInitialVelocityX < 0f
                || knockbackDecelerationPerCombatFrame < 0f)
            {
                throw new ArgumentException("攻撃データの数値が不正です: " + attackIdLabel);
            }

            this.attackId = attackId;
            this.attackIdLabel = attackIdLabel;
            this.startupFrames = startupFrames;
            this.activeFrames = activeFrames;
            this.recoveryFrames = recoveryFrames;
            this.damage = damage;
            this.hitStopFrames = hitStopFrames;
            this.hitStunFrames = hitStunFrames;
            this.knockbackInitialVelocityX = knockbackInitialVelocityX;
            this.knockbackDecelerationPerCombatFrame = knockbackDecelerationPerCombatFrame;
            this.hitBoxLocalCenterX = hitBoxLocalCenterX;
            this.hitBoxLocalCenterY = hitBoxLocalCenterY;
            this.hitBoxHalfWidth = hitBoxHalfWidth;
            this.hitBoxHalfHeight = hitBoxHalfHeight;
        }

        /// <summary>enum の攻撃 ID（Hit 解決・技相性用）。</summary>
        public DebugAttackId Id
        {
            get { return attackId; }
        }

        /// <summary>ログ / HUD 用ラベル（"JPunch" / "GroundKick"）。</summary>
        public string AttackId
        {
            get { return attackIdLabel; }
        }

        public int StartupFrames
        {
            get { return startupFrames; }
        }

        public int ActiveFrames
        {
            get { return activeFrames; }
        }

        public int RecoveryFrames
        {
            get { return recoveryFrames; }
        }

        public int Damage
        {
            get { return damage; }
        }

        public int HitStopFrames
        {
            get { return hitStopFrames; }
        }

        public int HitStunFrames
        {
            get { return hitStunFrames; }
        }

        public float KnockbackInitialVelocityX
        {
            get { return knockbackInitialVelocityX; }
        }

        public float KnockbackDecelerationPerCombatFrame
        {
            get { return knockbackDecelerationPerCombatFrame; }
        }

        public float HitBoxLocalCenterX
        {
            get { return hitBoxLocalCenterX; }
        }

        public float HitBoxLocalCenterY
        {
            get { return hitBoxLocalCenterY; }
        }

        public float HitBoxHalfWidth
        {
            get { return hitBoxHalfWidth; }
        }

        public float HitBoxHalfHeight
        {
            get { return hitBoxHalfHeight; }
        }

        public int TotalFrames
        {
            get { return startupFrames + activeFrames + recoveryFrames; }
        }

        public int ActiveStartActionFrame
        {
            get { return startupFrames + 1; }
        }

        public int ActiveEndActionFrame
        {
            get { return startupFrames + activeFrames; }
        }

        public bool IsStartupFrame(int actionFrame)
        {
            return actionFrame >= 1 && actionFrame <= startupFrames;
        }

        public bool IsActiveFrame(int actionFrame)
        {
            return actionFrame >= ActiveStartActionFrame
                && actionFrame <= ActiveEndActionFrame;
        }

        public bool IsRecoveryFrame(int actionFrame)
        {
            return actionFrame > ActiveEndActionFrame
                && actionFrame <= TotalFrames;
        }

        public bool IsFinished(int actionFrame)
        {
            return actionFrame >= TotalFrames;
        }

        /// <summary>
        /// ActionFrame から区間を返します。攻撃外・AF0 は None。
        /// </summary>
        public DebugAttackPhase GetPhase(int actionFrame)
        {
            if (IsStartupFrame(actionFrame))
            {
                return DebugAttackPhase.Startup;
            }

            if (IsActiveFrame(actionFrame))
            {
                return DebugAttackPhase.Active;
            }

            if (IsRecoveryFrame(actionFrame))
            {
                return DebugAttackPhase.Recovery;
            }

            return DebugAttackPhase.None;
        }

        public DebugBox2D CreateLocalHitBoxDefinition(bool isActive)
        {
            return new DebugBox2D(
                hitBoxLocalCenterX,
                hitBoxLocalCenterY,
                hitBoxHalfWidth,
                hitBoxHalfHeight,
                isActive
            );
        }

        private static DebugAttackData CreateJPunch()
        {
            // Startup AF 1〜3 / Active 4〜6 / Recovery 7〜12（Total=12）
            return new DebugAttackData(
                DebugAttackId.JPunch,
                "JPunch",
                3,
                3,
                6,
                10,
                6,
                12,
                0.18f,
                0.015f,
                0.75f,
                1.25f,
                0.55f,
                0.35f
            );
        }

        private static DebugAttackData CreateGroundKick()
        {
            // Startup 8 / Active 3 / Recovery 4（Total=15）
            // Hit Box: Punch より遠く、蹴り脚に合わせて低く薄めにした仮値
            // centerX 0.95 / centerY 0.55 / half 0.60×0.25
            return new DebugAttackData(
                DebugAttackId.GroundKick,
                "GroundKick",
                8,
                3,
                4,
                14,
                7,
                14,
                0.24f,
                0.015f,
                0.95f,
                0.55f,
                0.60f,
                0.25f
            );
        }
    }
}
