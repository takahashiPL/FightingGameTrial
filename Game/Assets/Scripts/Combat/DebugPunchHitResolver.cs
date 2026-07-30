using FightingGameTrial.Fighter;

namespace FightingGameTrial.Combat
{
    /// <summary>
    /// Hit ラベル（HUD 用）。
    /// </summary>
    public static class DebugPunchHitCheckLabels
    {
        public const string Inactive = "Inactive";
        public const string NoOverlap = "NoOverlap";
        public const string Hit = "Hit";
        public const string AlreadyHit = "AlreadyHit";
        public const string DefenderKO = "DefenderKO";
    }

    /// <summary>
    /// Hit Box × Hurt Box の重なりで「Hit 候補になり得るか」を純関数で評価します。
    ///
    /// ここでは Damage / HitStun / Knockback を適用しません。
    /// Session が両方向の候補を集めてから NormalHit / Clash を決めます。
    ///
    /// isAttackPlaying / hasCurrentAttackHit は攻撃種類を問わない汎用フラグです
    /// （J Punch / Ground Kick どちらでも同じ経路）。
    /// </summary>
    public static class DebugPunchHitResolver
    {
        public static bool TryResolveHit(
            bool isAttackPlaying,
            bool hasCurrentAttackHit,
            DebugBox2D attackerHitBox,
            DebugBox2D defenderHurtBox,
            out string checkLabel,
            out bool boxesOverlap)
        {
            checkLabel = DebugPunchHitCheckLabels.Inactive;
            boxesOverlap = false;

            if (isAttackPlaying == false)
            {
                return false;
            }

            if (attackerHitBox == null || attackerHitBox.IsActive == false)
            {
                return false;
            }

            if (hasCurrentAttackHit)
            {
                checkLabel = DebugPunchHitCheckLabels.AlreadyHit;
                if (defenderHurtBox != null && defenderHurtBox.IsActive)
                {
                    boxesOverlap = DebugBox2D.Overlaps(attackerHitBox, defenderHurtBox);
                }

                return false;
            }

            if (defenderHurtBox == null || defenderHurtBox.IsActive == false)
            {
                return false;
            }

            boxesOverlap = DebugBox2D.Overlaps(attackerHitBox, defenderHurtBox);
            if (boxesOverlap == false)
            {
                checkLabel = DebugPunchHitCheckLabels.NoOverlap;
                return false;
            }

            checkLabel = DebugPunchHitCheckLabels.Hit;
            return true;
        }
    }
}
