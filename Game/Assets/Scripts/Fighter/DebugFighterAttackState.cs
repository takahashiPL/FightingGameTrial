using System;
using FightingGameTrial.Combat;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 1体のFighterが持つ、現在の攻撃進行状態です。
    ///
    /// 何を保持するか:
    /// - Attack / Kick ボタンの前tick状態と今tick押下エッジ
    /// - 現在攻撃中か・どの攻撃か（DebugAttackId + DebugAttackData）
    /// - 攻撃開始 CombatFrame・ActionFrame・Hit 済みか
    /// - 直近の攻撃結果ラベル（Hit / Miss / Clash）
    ///
    /// Punch と Kick は同時再生しません（IsActionPlaying が共通ゲート）。
    /// 将来の空中 Kick は別 AttackId / 別 Data で StartAttack する想定です。
    /// </summary>
    [Serializable]
    public class DebugFighterAttackState
    {
        private bool previousAttackHeld;
        private bool attackPressedThisTick;
        private bool previousKickHeld;
        private bool kickPressedThisTick;

        private bool isActionPlaying;
        private DebugAttackId currentAttackId;
        private DebugAttackData currentAttackData;
        private int attackStartedCombatFrame;
        private bool hasCurrentAttackHit;
        private int actionFrame;
        private string lastAttackResult = "None";

        /// <summary>
        /// 現在のジャンプで Air Kick を使ったか。
        /// 着地では解除せず、次のジャンプ開始成功時に解除することで1ジャンプ1回を明確にします。
        /// </summary>
        private bool airKickUsedThisJump;

        public bool PreviousAttackHeld
        {
            get { return previousAttackHeld; }
        }

        public bool AttackPressedThisTick
        {
            get { return attackPressedThisTick; }
        }

        public bool KickPressedThisTick
        {
            get { return kickPressedThisTick; }
        }

        public bool IsActionPlaying
        {
            get { return isActionPlaying; }
        }

        public DebugAttackId CurrentAttackId
        {
            get { return currentAttackId; }
        }

        public DebugAttackData CurrentAttackData
        {
            get { return currentAttackData; }
        }

        public int AttackStartedCombatFrame
        {
            get { return attackStartedCombatFrame; }
        }

        /// <summary>互換: 現在が J Punch か。</summary>
        public bool IsJPunchAttack
        {
            get { return isActionPlaying && currentAttackId == DebugAttackId.JPunch; }
        }

        /// <summary>現在が地上 Kick か。</summary>
        public bool IsGroundKickAttack
        {
            get { return isActionPlaying && currentAttackId == DebugAttackId.GroundKick; }
        }

        public bool IsAirKickAttack
        {
            get { return isActionPlaying && currentAttackId == DebugAttackId.AirKick; }
        }

        public bool AirKickUsedThisJump
        {
            get { return airKickUsedThisJump; }
        }

        /// <summary>互換: J Punch 再生中かつその攻撃で Hit 済みか。</summary>
        public bool HasCurrentJPunchHit
        {
            get { return IsJPunchAttack && hasCurrentAttackHit; }
        }

        public bool HasCurrentAttackHit
        {
            get { return hasCurrentAttackHit; }
        }

        public int ActionFrame
        {
            get { return actionFrame; }
        }

        public string LastAttackResult
        {
            get { return lastAttackResult; }
        }

        public DebugAttackPhase CurrentPhase
        {
            get
            {
                if (isActionPlaying == false || currentAttackData == null)
                {
                    return DebugAttackPhase.None;
                }

                return currentAttackData.GetPhase(actionFrame);
            }
        }

        /// <summary>
        /// Attack(J) と Kick(K) の押下エッジをサンプリングします。
        /// Held（押しっぱなし）ではエッジは立ちません。バッファも持ちません。
        /// HitStop 中も呼ばれ、ActionFrame 進行とは分離します。
        /// </summary>
        public void SampleAttackButtons(bool attackHeldNow, bool kickHeldNow)
        {
            attackPressedThisTick =
                attackHeldNow && previousAttackHeld == false;
            previousAttackHeld = attackHeldNow;

            kickPressedThisTick =
                kickHeldNow && previousKickHeld == false;
            previousKickHeld = kickHeldNow;
        }

        /// <summary>
        /// 互換: Attack ボタンのみサンプリング（Kick は離した扱い）。
        /// </summary>
        public void SampleAttackInput(bool attackHeldNow)
        {
            SampleAttackButtons(attackHeldNow, false);
        }

        /// <summary>
        /// 指定攻撃を開始します。攻撃データは呼び出し側が渡す正本参照です。
        /// </summary>
        public void StartAttack(
            DebugAttackId attackId,
            DebugAttackData attackData,
            int combatFrame)
        {
            if (attackId == DebugAttackId.None || attackData == null)
            {
                return;
            }

            currentAttackId = attackId;
            currentAttackData = attackData;
            attackStartedCombatFrame = combatFrame;
            actionFrame = 0;
            isActionPlaying = true;
            hasCurrentAttackHit = false;
            lastAttackResult = "None";
        }

        /// <summary>互換: Jパンチ開始。</summary>
        public void StartJPunch()
        {
            StartAttack(DebugAttackId.JPunch, DebugAttackData.JPunch, 0);
        }

        public void StartJPunch(int combatFrame)
        {
            StartAttack(DebugAttackId.JPunch, DebugAttackData.JPunch, combatFrame);
        }

        public void StartGroundKick(int combatFrame)
        {
            StartAttack(DebugAttackId.GroundKick, DebugAttackData.Kick, combatFrame);
        }

        public void StartAirKick(int combatFrame)
        {
            // 空中攻撃の進行は共通 AttackState を使いますが、ID/Data は Ground Kick と分けます。
            StartAttack(DebugAttackId.AirKick, DebugAttackData.AirKick, combatFrame);
            airKickUsedThisJump = true;
        }

        /// <summary>
        /// Motor が新しいジャンプを開始できたときだけ呼びます。
        /// 地上待機や着地直後に解除しないため、同じジャンプ中の再発動を防げます。
        /// </summary>
        public void BeginNewJumpForAirKickUsage()
        {
            airKickUsedThisJump = false;
        }

        public void AdvanceActionFrame()
        {
            if (isActionPlaying == false)
            {
                return;
            }

            actionFrame = actionFrame + 1;
            if (actionFrame < 0)
            {
                actionFrame = 0;
            }
        }

        public void MarkHit()
        {
            hasCurrentAttackHit = true;
            lastAttackResult = "Hit";
        }

        /// <summary>
        /// 被 Hit で攻撃を即中断します。Miss にはしません。
        /// </summary>
        public void InterruptByHit()
        {
            ClearPlayingAttackFields();
        }

        /// <summary>
        /// 通常終了。未 Hit なら Miss。Clash 終了は EndAttackAsClash を使う。
        /// </summary>
        public void EndAttack()
        {
            if (hasCurrentAttackHit == false)
            {
                lastAttackResult = "Miss";
            }

            ClearPlayingAttackFields();
        }

        /// <summary>互換: Jパンチ終了。</summary>
        public void EndJPunch()
        {
            EndAttack();
        }

        /// <summary>
        /// Clash で攻撃を即終了し、同じ攻撃が再判定されないよう Hit 済み扱いにします。
        /// </summary>
        public void EndAttackAsClash()
        {
            hasCurrentAttackHit = true;
            lastAttackResult = "Clash";
            ClearPlayingAttackFields();
        }

        public void Reset()
        {
            previousAttackHeld = false;
            attackPressedThisTick = false;
            previousKickHeld = false;
            kickPressedThisTick = false;
            airKickUsedThisJump = false;
            ClearPlayingAttackFields();
            lastAttackResult = "None";
        }

        public void ClearAttackEdgePrevious()
        {
            previousAttackHeld = false;
            attackPressedThisTick = false;
            previousKickHeld = false;
            kickPressedThisTick = false;
        }

        private void ClearPlayingAttackFields()
        {
            actionFrame = 0;
            isActionPlaying = false;
            currentAttackId = DebugAttackId.None;
            currentAttackData = null;
            attackStartedCombatFrame = 0;
            hasCurrentAttackHit = false;
        }
    }
}
