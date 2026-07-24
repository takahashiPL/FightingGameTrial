using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// P2 被弾デバッグ用の一時 Component です（段階10B-1 / 案A）。
    ///
    /// 責務（今回）:
    /// - HitCount / 直近 Hit 情報を保持する
    /// - Session からの ReceiveHit を受ける
    /// - CombatFrame 開始時に WasHitThisCombatFrame を下ろす
    ///
    /// やらないこと:
    /// - LogicalX / Facing を持たない（正本は同じ GameObject の DebugFighterMotor）
    /// - 入力 / Clock / Keyboard を読まない
    /// - Update / FixedUpdate を使わない
    /// - ダメージ・ノックバック・硬直を持たない
    ///
    /// なぜ残すか（案A）:
    /// 10B-1 では攻撃状態の共通化をしないため、既存の P1→P2 Hit 通知口を小さく維持する。
    /// LogicalX の二重管理だけを解消し、HitCount の完全移設と本 Component 廃止は 10B-2 へ延期する。
    /// </summary>
    public class DebugDummyTarget : MonoBehaviour
    {
        /// <summary>
        /// 累計 Hit 回数です。
        /// </summary>
        private int hitCount;

        /// <summary>
        /// 今の CombatFrame で Hit を受けたか。
        /// </summary>
        private bool wasHitThisCombatFrame;

        /// <summary>
        /// 直近で Hit した CombatFrame 番号です。未Hitは -1。
        /// </summary>
        private int lastHitCombatFrame = -1;

        public int HitCount
        {
            get { return hitCount; }
        }

        public bool WasHitThisCombatFrame
        {
            get { return wasHitThisCombatFrame; }
        }

        public int LastHitCombatFrame
        {
            get { return lastHitCombatFrame; }
        }

        private void Awake()
        {
            hitCount = 0;
            wasHitThisCombatFrame = false;
            lastHitCombatFrame = -1;
        }

        /// <summary>
        /// 新しい CombatFrame の開始時に、このフレームの Hit 旗を下ろします。
        /// </summary>
        public void BeginCombatFrame()
        {
            wasHitThisCombatFrame = false;
        }

        /// <summary>
        /// パンチ Hit を受け取ります。ダメージ等はまだありません。
        /// </summary>
        public void ReceiveHit(int combatFrame)
        {
            hitCount = hitCount + 1;
            wasHitThisCombatFrame = true;
            lastHitCombatFrame = combatFrame;
        }
    }
}
