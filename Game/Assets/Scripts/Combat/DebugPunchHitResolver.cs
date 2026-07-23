namespace FightingGameTrial.Combat
{
    /// <summary>
    /// Jパンチの Active 判定だけを行う小さな判定役です（段階9）。
    ///
    /// なぜ Collider ではなく論理座標で判定するか:
    /// ゲーム仕様の正本は SimulationTick / CombatFrame / ActionFrame です。
    /// Physics のタイミングに依存すると、Pause / Step / HitStop とズレやすくなります。
    ///
    /// なぜ Active Frame だけ判定するか:
    /// Startup は振りかぶり、Recovery は硬直です。
    /// 見た目が攻撃中でも、仕様上「当たるコマ」は Active に限定します。
    ///
    /// なぜ向いている側だけ Hit するか:
    /// 後ろの相手へ手が届くのは不自然です。
    /// 距離だけでなく Facing 方向を必須条件にします。
    ///
    /// なぜ 1攻撃 1Hit にするか:
    /// Active が複数フレームあっても、同じパンチで何度も Hit すると学習用確認が難しくなります。
    /// HasCurrentJPunchHit で最初の1回だけ成立させます。
    ///
    /// ダメージやノックバックを入れない理由:
    /// 今回の目的は「Hit 検出 → 既存 HitStop を発生」までの最小確認です。
    /// </summary>
    public static class DebugPunchHitResolver
    {
        /// <summary>
        /// Active かつ未Hit かつ向き込み距離条件を満たすとき true を返します。
        /// </summary>
        public static bool TryResolveHit(
            bool isJPunchAttack,
            int actionFrame,
            int activeStartFrame,
            int activeEndFrame,
            bool hasCurrentJPunchHit,
            float playerX,
            bool playerFacingRight,
            float dummyX,
            float attackRange)
        {
            if (isJPunchAttack == false)
            {
                return false;
            }

            if (hasCurrentJPunchHit)
            {
                return false;
            }

            if (actionFrame < activeStartFrame || actionFrame > activeEndFrame)
            {
                return false;
            }

            if (attackRange < 0f)
            {
                return false;
            }

            // 向いている側に相手がいて、距離が範囲以内であること
            if (playerFacingRight)
            {
                if (dummyX < playerX)
                {
                    return false;
                }

                float distance = dummyX - playerX;
                if (distance > attackRange)
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (dummyX > playerX)
                {
                    return false;
                }

                float distance = playerX - dummyX;
                if (distance > attackRange)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
