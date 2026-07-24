using System;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 1体のFighterが持つ、現在の攻撃進行状態です。
    ///
    /// 何を保持するか:
    /// - 攻撃ボタンの前tick状態
    /// - 今tickで攻撃ボタンが押されたか
    /// - 現在攻撃中か
    /// - 現在のActionFrame
    /// - 現在の攻撃がすでにHitしたか
    /// - 直近の攻撃結果
    ///
    /// なぜFighter単位で持つか:
    /// 従来はSimulationSession / SimulationTimeStateに
    /// P1専用の攻撃状態が置かれていました。
    ///
    /// その構造ではP2が攻撃するときに、
    /// P1用変数を複製する必要があります。
    ///
    /// Fighterごとに本クラスを1つ持たせることで、
    /// P1とP2が同じ攻撃開始・進行・Hit処理を使用できます。
    ///
    /// やらないこと:
    /// - UnityのUpdateを持たない
    /// - 入力デバイスを直接読まない
    /// - Hit判定を実行しない
    /// - Spriteを直接変更しない
    ///
    /// 実際の処理順はSimulationSessionが管理し、
    /// このクラスは状態の保持と初期化だけを担当します。
    /// </summary>
    [Serializable]
    public class DebugFighterAttackState
    {
        private bool previousAttackHeld;
        private bool attackPressedThisTick;

        private bool isActionPlaying;
        private bool isJPunchAttack;
        private bool hasCurrentJPunchHit;

        private int actionFrame;

        private string lastAttackResult = "None";

        public bool PreviousAttackHeld
        {
            get { return previousAttackHeld; }
        }

        public bool AttackPressedThisTick
        {
            get { return attackPressedThisTick; }
        }

        public bool IsActionPlaying
        {
            get { return isActionPlaying; }
        }

        public bool IsJPunchAttack
        {
            get { return isJPunchAttack; }
        }

        public bool HasCurrentJPunchHit
        {
            get { return hasCurrentJPunchHit; }
        }

        public int ActionFrame
        {
            get { return actionFrame; }
        }

        public string LastAttackResult
        {
            get { return lastAttackResult; }
        }

        /// <summary>
        /// 現在入力と前tick入力を比較し、
        /// 攻撃ボタンが今tickで押されたかを確定します。
        ///
        /// HitStop中にも入力の押下状態だけは更新する、
        /// という現在のSession仕様を維持できるよう、
        /// ActionFrame進行とは分離しています。
        /// </summary>
        public void SampleAttackInput(bool attackHeldNow)
        {
            attackPressedThisTick =
                attackHeldNow && previousAttackHeld == false;

            previousAttackHeld = attackHeldNow;
        }

        /// <summary>
        /// Jパンチを開始状態にします。
        /// </summary>
        public void StartJPunch()
        {
            actionFrame = 0;
            isActionPlaying = true;
            isJPunchAttack = true;
            hasCurrentJPunchHit = false;
            lastAttackResult = "None";
        }

        /// <summary>
        /// 攻撃中のActionFrameを1進めます。
        /// 攻撃中でない場合は何もしません。
        /// </summary>
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

        /// <summary>
        /// 現在の攻撃がHitしたことを記録します。
        /// </summary>
        public void MarkHit()
        {
            hasCurrentJPunchHit = true;
            lastAttackResult = "Hit";
        }

        /// <summary>
        /// 現在の攻撃を終了します。
        ///
        /// HitしていなければMissとして記録します。
        /// 終了後もlastAttackResultはHUD確認用に残します。
        /// </summary>
        public void EndJPunch()
        {
            if (hasCurrentJPunchHit == false)
            {
                lastAttackResult = "Miss";
            }

            actionFrame = 0;
            isActionPlaying = false;
            isJPunchAttack = false;
            hasCurrentJPunchHit = false;
        }

        /// <summary>
        /// Scene開始時やデバッグ状態の再初期化時に使用します。
        /// </summary>
        public void Reset()
        {
            previousAttackHeld = false;
            attackPressedThisTick = false;

            isActionPlaying = false;
            isJPunchAttack = false;
            hasCurrentJPunchHit = false;

            actionFrame = 0;
            lastAttackResult = "None";
        }
    }
}
