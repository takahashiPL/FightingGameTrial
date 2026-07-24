using System;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 1体の Participant が持つ被 Hit / HitStun 状態です（段階12A）。
    ///
    /// 何を担当するか:
    /// - TotalHitCount（被弾累計）
    /// - HitStun 残り Combat Frame
    /// - 今 CombatFrame で被弾したか / 直近被弾 CombatFrame
    ///
    /// なぜ Participant 側が所有するか:
    /// HitStop は試合全体の共有時間（SimulationTimeState）だが、
    /// HitStun は「その機体が行動不能な硬直」であり機体ごとに独立する。
    /// P1 が被弾しても同じ経路を使えるよう、P2 専用変数にはしない。
    ///
    /// HitStop と HitStun の違い:
    /// - HitStop … 試合全体の Combat 進行停止（移動・Action・判定も止まる）
    /// - HitStun … HitStop 終了後も続く、被弾側だけの行動不能
    ///
    /// なぜ HitStop 中に HitStun を減らさないか:
    /// CombatFrame が進まない区間だから。残りは HitStop 終了後の Combat で消費する。
    ///
    /// やらないこと:
    /// - ノックバック / HP / ガード
    /// - Unity Update での時間進行
    /// - Hit 判定そのもの（Session / Resolver の責務）
    /// </summary>
    [Serializable]
    public class DebugFighterHitState
    {
        private int totalHitCount;
        private int hitStunRemainingFrames;
        private bool wasHitThisCombatFrame;
        private int lastHitCombatFrame = -1;

        public int TotalHitCount
        {
            get { return totalHitCount; }
        }

        public int HitStunRemainingFrames
        {
            get { return hitStunRemainingFrames; }
        }

        /// <summary>
        /// HitStun 残りが 1 以上なら行動不能（HitStop 中も含む表示・制限に使う）。
        /// </summary>
        public bool IsInHitStun
        {
            get { return hitStunRemainingFrames > 0; }
        }

        public bool WasHitThisCombatFrame
        {
            get { return wasHitThisCombatFrame; }
        }

        public int LastHitCombatFrame
        {
            get { return lastHitCombatFrame; }
        }

        /// <summary>
        /// CombatFrame 開始時に「今フレーム被弾」旗だけ下ろします。
        /// HitStun 残りはここでは減らしません。
        /// </summary>
        public void BeginCombatFrame()
        {
            wasHitThisCombatFrame = false;
        }

        /// <summary>
        /// Hit 成立時に呼ばれ、累計と HitStun 残りを設定します。
        ///
        /// HitStop はこのメソッドでは扱いません（Session が共有 HitStop を設定）。
        /// 同じ攻撃の二重 Receive は Session / MarkHit 側で防ぐ前提です。
        /// </summary>
        public void BeginHitStun(int hitStunFrames, int combatFrame)
        {
            totalHitCount = totalHitCount + 1;
            wasHitThisCombatFrame = true;
            lastHitCombatFrame = combatFrame;

            if (hitStunFrames < 0)
            {
                hitStunFrames = 0;
            }

            hitStunRemainingFrames = hitStunFrames;
        }

        /// <summary>
        /// 1 Combat Frame 分だけ HitStun を消費します。
        ///
        /// 呼び出し側（Session）は HitStop 外の Combat 処理の末尾で、
        /// 参加者ごとに1回だけ呼ぶこと。SimulationTick 単位では呼ばない。
        /// </summary>
        public void TickCombatFrame()
        {
            if (hitStunRemainingFrames <= 0)
            {
                return;
            }

            hitStunRemainingFrames = hitStunRemainingFrames - 1;
            if (hitStunRemainingFrames < 0)
            {
                hitStunRemainingFrames = 0;
            }
        }

        /// <summary>
        /// Reset / 初期化用。累計・Stun・旗をすべてクリアします。
        /// </summary>
        public void Reset()
        {
            totalHitCount = 0;
            hitStunRemainingFrames = 0;
            wasHitThisCombatFrame = false;
            lastHitCombatFrame = -1;
        }
    }
}
