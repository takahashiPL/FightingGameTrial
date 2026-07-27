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
    /// なぜ Session の const から分離するか:
    /// 固定値が Session / Participant / Visual に散ると「どれが正本か」が追えない。
    /// 参照元を1つにすると、将来の複数攻撃や ScriptableObject 化の接続点が明確になる。
    ///
    /// local Hit Box と world Hit Box:
    /// - ここは Facing Right 基準のローカル定義（Center / Half）だけを持つ
    /// - World 変換・Facing 反転・原点計算は Participant.EvaluateWorldHitBox の責務
    ///
    /// Frame 区間の考え方（ActionFrame）:
    /// StartJPunch 直後は AF=0。Combat ごとに +1。
    /// Startup = 1〜StartupFrames、Active = その直後の ActiveFrames 分、
    /// Recovery = Active 終了翌〜TotalFrames。AF &gt;= TotalFrames で攻撃終了。
    ///
    /// 今回 ScriptableObject 化しない理由:
    /// 学習用に「値の正本がコード上の1か所」であることを優先する。
    /// Inspector / アセット依存を増やす前に、参照経路を整理する段階。
    ///
    /// 将来の複数攻撃:
    /// Session が「今使う DebugAttackData」を攻撃開始時に選ぶ接続点を残す。
    /// 現段階は JPunch 静的1本のみ。
    /// </summary>
    public sealed class DebugAttackData
    {
        /// <summary>
        /// J Punch の設定正本（1回だけ生成。毎 Frame / 毎 Hit で new しない）。
        /// </summary>
        public static readonly DebugAttackData JPunch = CreateJPunch();

        private readonly string attackId;
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
            string attackId,
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
            if (string.IsNullOrEmpty(attackId))
            {
                throw new ArgumentException("AttackId が空です。", "attackId");
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
                throw new ArgumentException("攻撃データの数値が不正です: " + attackId);
            }

            this.attackId = attackId;
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

        public string AttackId
        {
            get { return attackId; }
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

        /// <summary>
        /// Startup + Active + Recovery。ActionFrame がこの値以上で攻撃終了。
        /// </summary>
        public int TotalFrames
        {
            get { return startupFrames + activeFrames + recoveryFrames; }
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

        /// <summary>
        /// Knockback 初速の絶対値（符号は Session が LogicalX 比較で決める）。
        /// </summary>
        public float KnockbackInitialVelocityX
        {
            get { return knockbackInitialVelocityX; }
        }

        public float KnockbackDecelerationPerCombatFrame
        {
            get { return knockbackDecelerationPerCombatFrame; }
        }

        /// <summary>
        /// Facing Right 基準の Hit Box ローカル Center X。
        /// </summary>
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

        /// <summary>
        /// Active 区間の最初の ActionFrame（1始まり区間の先頭）。
        /// </summary>
        public int ActiveStartActionFrame
        {
            get { return startupFrames + 1; }
        }

        /// <summary>
        /// Active 区間の最後の ActionFrame（含む）。
        /// </summary>
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

        /// <summary>
        /// 既存 Session と同じ: ActionFrame &gt;= TotalFrames で終了。
        /// </summary>
        public bool IsFinished(int actionFrame)
        {
            return actionFrame >= TotalFrames;
        }

        /// <summary>
        /// Facing Right 基準のローカル Hit Box を新しい DebugBox2D として返す。
        /// IsActive は呼び出し側が ActionFrame から決める。
        /// </summary>
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

        /// <summary>
        /// 現行実装の J Punch 固定値をそのまま写した工場です。
        /// Docs 推測ではなく、Session / Participant にあった値を移行する。
        /// </summary>
        private static DebugAttackData CreateJPunch()
        {
            // Startup AF 1〜3 / Active 4〜6 / Recovery 7〜12（Total=12）
            // Damage=10, HitStop=6, HitStun=12, KB=0.18 / decel=0.015
            // Hit Box local: (0.75, 1.25, 0.55, 0.35)
            return new DebugAttackData(
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
    }
}
