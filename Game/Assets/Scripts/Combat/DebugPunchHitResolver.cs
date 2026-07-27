using FightingGameTrial.Fighter;

namespace FightingGameTrial.Combat
{
    /// <summary>
    /// Jパンチ Hit の評価結果ラベルです（HUD 用・段階11B）。
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
    /// Jパンチの Hit 成立条件を純関数で評価します（段階11B）。
    ///
    /// なぜ距離判定をやめたか:
    /// 可視化した Hit Box / Hurt Box と実判定がズレると学習・確認が難しい。
    /// 同じ DebugBox2D（World）の重なりで判定し、見た目と結果を一致させる。
    ///
    /// なぜ Unity Physics / Collider を使わないか:
    /// ゲーム仕様の正本は 60Hz の SimulationTick / CombatFrame です。
    /// Physics のタイミングに依存すると Pause / Step / HitStop とズレやすい。
    ///
    /// なぜ Active だけか:
    /// Startup は振りかぶり、Recovery は硬直。当たるコマは Active に限定する。
    /// Active 範囲そのものは Participant.EvaluateWorldHitBox().IsActive が正本
    /// （可視化と同じ経路。ここに独自フレーム範囲は持たない）。
    ///
    /// 1攻撃1Hit:
    /// hasCurrentJPunchHit が true なら AlreadyHit。実際の旗更新は Session の MarkHit。
    ///
    /// 境界接触:
    /// DebugBox2D.Overlaps は等号あり（接しているだけでも Hit）。
    /// </summary>
    public static class DebugPunchHitResolver
    {
        /// <summary>
        /// Hit Box × Hurt Box の重なりで Hit 可否を評価します。
        ///
        /// 戻り値 true のときだけ Session が MarkHit / ReceiveHit / HitStop を適用します。
        /// checkLabel は HUD 用（Inactive / NoOverlap / Hit / AlreadyHit）。
        /// </summary>
        public static bool TryResolveHit(
            bool isJPunchAttack,
            bool hasCurrentJPunchHit,
            DebugBox2D attackerHitBox,
            DebugBox2D defenderHurtBox,
            out string checkLabel,
            out bool boxesOverlap)
        {
            checkLabel = DebugPunchHitCheckLabels.Inactive;
            boxesOverlap = false;

            if (isJPunchAttack == false)
            {
                return false;
            }

            if (attackerHitBox == null || attackerHitBox.IsActive == false)
            {
                // Startup / Recovery / Idle、または Hit Box 未評価
                return false;
            }

            if (hasCurrentJPunchHit)
            {
                // 同じ攻撃で既に Hit 済み（Active 中に重なり続けても二重 Hit しない）
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
