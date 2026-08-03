using System;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 1体の Participant が持つ被 Hit / HitStun / ノックバック速度です（段階12A / 13A）。
    ///
    /// 何を担当するか:
    /// - TotalHitCount（被弾累計）
    /// - HitStun 残り Combat Frame
    /// - 今 CombatFrame で被弾したか / 直近被弾 CombatFrame
    /// - ノックバック横速度 knockbackVelocityX（段階13A）
    ///
    /// なぜ Participant 側が所有するか:
    /// HitStop は試合全体の共有時間（SimulationTimeState）だが、
    /// HitStun / ノックバックは「その機体の被弾反応」であり機体ごとに独立する。
    /// P1 が被弾しても同じ経路を使えるよう、P2 専用変数にはしない。
    ///
    /// なぜノックバック速度も HitState に含めるか:
    /// 寿命が HitStun と同じ（ReceiveHit で開始、HitStop 中は据え置き、
    /// HitStun 終了 / Reset で 0）であり、専用クラスを増やすより追跡しやすい。
    /// 位置の書き込みは Motor.SetLogicalX の責務。ここは速度の正本のみ。
    ///
    /// HitStop と HitStun / ノックバックの時間関係:
    /// - Hit 成立時に初速をセットするが、その Combat Frame ではまだ移動しない
    ///   （移動フェーズが Hit 判定より前のため）
    /// - HitStop 中は Combat 全体が止まるので、移動も減速もしない
    /// - HitStop 終了後の Combat Frame から、1CF ごとに移動→減速を1回行う
    ///
    /// なぜ Time.deltaTime を使わないか:
    /// 正本は固定 60Hz の Combat Frame。Pause / Step と揃えるため、
    /// 実時間ではなく「1 Combat Frame = 1回の加減算」で扱う。
    ///
    /// HitStop と HitStun の違い:
    /// - HitStop … 試合全体の Combat 進行停止（移動・Action・判定も止まる）
    /// - HitStun … HitStop 終了後も続く、被弾側だけの行動不能
    ///
    /// やらないこと:
    /// - Transform への直接書き込み（Motor の責務）
    /// - Push 計算・ステージ端の壁処理（段階13B で壁際 Push 配分を検討）
    /// - Y 方向・加速度・重力・HP / ガード
    /// - Unity Update での時間進行
    /// - Hit 判定そのもの（Session / Resolver の責務）
    /// </summary>
    [Serializable]
    public class DebugFighterHitState
    {
        private int totalHitCount;
        private int hitStunRemainingFrames;

        /// <summary>
        /// Ground Clash 成立後の専用反動残り Combat Frame。
        /// 通常 HitStun とは分け、被弾回数や赤表示へ混ぜません。
        /// </summary>
        private int clashRecoilRemainingFrames;

        /// <summary>
        /// 立ちガード成立後の専用硬直残り Combat Frame。
        /// HitStun とは分け、HitCount や赤表示へ混ぜません。
        /// </summary>
        private int guardStunRemainingFrames;

        private bool wasHitThisCombatFrame;
        private int lastHitCombatFrame = -1;

        /// <summary>
        /// 現在のノックバック横速度（Combat Frame 単位。正=右、負=左）。
        /// </summary>
        private float knockbackVelocityX;

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

        public int ClashRecoilRemainingFrames
        {
            get { return clashRecoilRemainingFrames; }
        }

        /// <summary>
        /// Ground Clash の専用反動中か。通常 HitStun とは別状態です。
        /// </summary>
        public bool IsInClashRecoil
        {
            get { return clashRecoilRemainingFrames > 0; }
        }

        public int GuardStunRemainingFrames
        {
            get { return guardStunRemainingFrames; }
        }

        /// <summary>
        /// 立ちガード硬直中か。通常 HitStun / ClashRecoil とは別状態です。
        /// </summary>
        public bool IsInGuardStun
        {
            get { return guardStunRemainingFrames > 0; }
        }

        /// <summary>
        /// 入力不能・ノックバック継続の対象となる戦闘リアクション中か。
        /// 通常 HitStun / Ground Clash 反動 / GuardStun をまとめて判定するときだけ使います。
        /// </summary>
        public bool IsInCombatReaction
        {
            get { return IsInHitStun || IsInClashRecoil || IsInGuardStun; }
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
        /// 現在のノックバック横速度（Combat Frame 単位）。
        /// </summary>
        public float KnockbackVelocityX
        {
            get { return knockbackVelocityX; }
        }

        /// <summary>
        /// ノックバック適用中か（速度が 0 でない）。HitStun 終了時は 0 に戻す。
        /// </summary>
        public bool IsBeingKnockedBack
        {
            get { return knockbackVelocityX != 0f; }
        }

        /// <summary>
        /// CombatFrame 開始時に「今フレーム被弾」旗だけ下ろします。
        /// HitStun 残り・ノックバック速度はここでは減らしません。
        /// </summary>
        public void BeginCombatFrame()
        {
            wasHitThisCombatFrame = false;
        }

        /// <summary>
        /// Hit 成立時に呼ばれ、累計・HitStun 残り・ノックバック初速を設定します。
        ///
        /// HitStop はこのメソッドでは扱いません（Session が共有 HitStop を設定）。
        /// 同じ攻撃の二重 Receive は Session / MarkHit 側で防ぐ前提です。
        /// このフレームでは移動しません（Session の移動フェーズは Hit より前）。
        /// </summary>
        public void BeginHitStun(int hitStunFrames, int combatFrame, float knockbackVelocity)
        {
            totalHitCount = totalHitCount + 1;
            wasHitThisCombatFrame = true;
            lastHitCombatFrame = combatFrame;

            if (hitStunFrames < 0)
            {
                hitStunFrames = 0;
            }

            // 通常 Hit が成立した時点で、Clash / Guard 専用状態は終了します。
            clashRecoilRemainingFrames = 0;
            guardStunRemainingFrames = 0;
            hitStunRemainingFrames = hitStunFrames;
            knockbackVelocityX = knockbackVelocity;
        }

        /// <summary>
        /// Ground Clash 成立時の専用反動を開始します。
        ///
        /// 通常 Hit との違い:
        /// - TotalHitCount を増やさない
        /// - WasHitThisCombatFrame / LastHitCombatFrame を変更しない
        /// - Damage は扱わない
        /// - Clash 専用表示のため、通常 HitStun とは別の残り時間を持つ
        ///
        /// HitStop は試合全体の共有時間なので Session が設定します。
        /// </summary>
        public void BeginClashRecoil(int recoilFrames, float knockbackVelocity)
        {
            if (recoilFrames < 0)
            {
                recoilFrames = 0;
            }

            // Clash は通常被弾ではないため、HitStun / GuardStun を使い回さない。
            hitStunRemainingFrames = 0;
            guardStunRemainingFrames = 0;
            clashRecoilRemainingFrames = recoilFrames;
            knockbackVelocityX = knockbackVelocity;
        }

        /// <summary>
        /// 立ちガード成立時の専用硬直を開始します。
        ///
        /// 通常 Hit / Clash との違い:
        /// - TotalHitCount を増やさない
        /// - WasHitThisCombatFrame / LastHitCombatFrame を変更しない
        /// - Damage は扱わない
        /// - HitStun を使い回さない（赤被弾表示と混同しない）
        ///
        /// HitStop は試合全体の共有時間なので Session が設定します。
        /// </summary>
        public void BeginGuardStun(int guardStunFrames, float knockbackVelocity)
        {
            if (guardStunFrames < 0)
            {
                guardStunFrames = 0;
            }

            hitStunRemainingFrames = 0;
            clashRecoilRemainingFrames = 0;
            guardStunRemainingFrames = guardStunFrames;
            knockbackVelocityX = knockbackVelocity;
        }

        /// <summary>
        /// 1 Combat Frame 分だけ通常 HitStun / ClashRecoil / GuardStun を消費します。
        ///
        /// 呼び出し側（Session）は HitStop 外の Combat 処理の末尾で、
        /// 参加者ごとに1回だけ呼ぶこと。SimulationTick 単位では呼ばない。
        /// ノックバック速度の減速は Session の移動直後に別途行う。
        /// </summary>
        public void TickCombatFrame()
        {
            if (hitStunRemainingFrames > 0)
            {
                hitStunRemainingFrames = hitStunRemainingFrames - 1;
                if (hitStunRemainingFrames < 0)
                {
                    hitStunRemainingFrames = 0;
                }
            }

            if (clashRecoilRemainingFrames > 0)
            {
                clashRecoilRemainingFrames = clashRecoilRemainingFrames - 1;
                if (clashRecoilRemainingFrames < 0)
                {
                    clashRecoilRemainingFrames = 0;
                }
            }

            if (guardStunRemainingFrames > 0)
            {
                guardStunRemainingFrames = guardStunRemainingFrames - 1;
                if (guardStunRemainingFrames < 0)
                {
                    guardStunRemainingFrames = 0;
                }
            }
        }

        /// <summary>
        /// 1 Combat Frame 分だけノックバック速度を 0 へ減速します。
        ///
        /// Time.deltaTime は掛けません（Combat Frame 単位の固定量）。
        /// 符号をまたいで往復しないよう、0 を超えたら 0 に固定します。
        /// </summary>
        public void TickKnockbackVelocity(float deceleration)
        {
            if (knockbackVelocityX == 0f)
            {
                return;
            }

            if (deceleration < 0f)
            {
                deceleration = 0f;
            }

            if (knockbackVelocityX > 0f)
            {
                knockbackVelocityX = knockbackVelocityX - deceleration;
                if (knockbackVelocityX < 0f)
                {
                    knockbackVelocityX = 0f;
                }
            }
            else
            {
                knockbackVelocityX = knockbackVelocityX + deceleration;
                if (knockbackVelocityX > 0f)
                {
                    knockbackVelocityX = 0f;
                }
            }
        }

        /// <summary>
        /// HitStun 終了時や Reset で残速度を捨てます。
        /// ノックバックは HitStun 中だけ適用する方針のため、Stun=0 で速度も 0 にする。
        /// </summary>
        public void ClearKnockback()
        {
            knockbackVelocityX = 0f;
        }

        /// <summary>
        /// Reset / 初期化用。累計・Stun・ノックバック・旗をすべてクリアします。
        /// </summary>
        public void Reset()
        {
            totalHitCount = 0;
            hitStunRemainingFrames = 0;
            clashRecoilRemainingFrames = 0;
            guardStunRemainingFrames = 0;
            wasHitThisCombatFrame = false;
            lastHitCombatFrame = -1;
            knockbackVelocityX = 0f;
        }
    }
}
