using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using FightingGameTrial.Simulation;
using TMPro;
using UnityEngine;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// デバッグ用の画面表示だけを担当します。
    ///
    /// 攻撃状態表示の正本:
    /// P1 DebugFighterParticipant.AttackState（Action / AF / Punch Phase / AttackResult / PunchHitDone）。
    ///
    /// SimulationTimeState が担う共有時間状態:
    /// SimulationTick / CombatFrame / Pause / HitStop / Step / Status / 入力サンプル。
    ///
    /// Inspector 明示参照のみ使い、検索はしません。
    /// </summary>
    public class DebugHudView : MonoBehaviour
    {
        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("時間状態と P1/P2 Participant 参照へ到達するための SimulationSession です。")]
        [SerializeField]
        private SimulationSession simulationSession;

        [Tooltip("HUD本文を表示する TextMeshProUGUI です。")]
        [SerializeField]
        private TextMeshProUGUI hudText;

        private void Awake()
        {
            if (simulationSession == null)
            {
                Debug.LogError("DebugHudView: SimulationSession が未設定です。");
            }

            if (hudText == null)
            {
                Debug.LogError("DebugHudView: TextMeshProUGUI が未設定です。");
            }
        }

        private void Update()
        {
            if (simulationSession == null || hudText == null)
            {
                return;
            }

            SimulationTimeState timeState = simulationSession.TimeState;
            if (timeState == null)
            {
                return;
            }

            hudText.text = BuildHudText(timeState);
        }

        /// <summary>
        /// 縦幅節約のため行を統合。追加ラベルは ASCII 優先（TMP欠落回避）。
        /// 操作説明は増やさない。
        /// </summary>
        private string BuildHudText(SimulationTimeState timeState)
        {
            // P1 AttackState を1回だけ取得（攻撃表示の正本。重複取得しない）
            DebugFighterParticipant p1 = simulationSession.ParticipantP1;
            DebugFighterAttackState attackState = null;
            if (p1 != null)
            {
                attackState = p1.AttackState;
            }

            string pauseLabel = timeState.IsPaused ? "Yes" : "No";

            // Action 表示: P1 AttackState が正本
            string actionPlayingLabel = "No";
            if (attackState != null && attackState.IsActionPlaying)
            {
                actionPlayingLabel = "Yes";
            }

            // AF 表示: P1 AttackState が正本
            int actionFrameValue = 0;
            if (attackState != null)
            {
                actionFrameValue = attackState.ActionFrame;
            }

            string stepLabel = timeState.LastStepResult;
            if (string.IsNullOrEmpty(stepLabel))
            {
                stepLabel = "-";
            }

            string statusLabel = timeState.LastStatusMessage;
            if (string.IsNullOrEmpty(statusLabel))
            {
                statusLabel = "-";
            }

            SimulationInputState input = timeState.CurrentInput;
            if (input == null)
            {
                input = new SimulationInputState();
            }

            string leftLabel = input.Left ? "1" : "0";
            string rightLabel = input.Right ? "1" : "0";
            string upLabel = input.Up ? "1" : "0";
            string downLabel = input.Down ? "1" : "0";
            string attackHeldLabel = input.Attack ? "1" : "0";
            string attackPressedLabel = simulationSession.AttackPressedThisTick ? "1" : "0";

            // ASCII labels only (avoid adding new JP glyphs to NotoSansJP SDF).
            string p1XLabel = "-";
            string p1FacingLabel = "-";
            if (p1 != null && p1.Motor != null)
            {
                p1XLabel = p1.Motor.LogicalX.ToString("0.00");
                p1FacingLabel = p1.Motor.FacingRight ? "R" : "L";
            }

            string visualLabel = "Idle";
            if (p1 != null && p1.Visual != null)
            {
                visualLabel = p1.Visual.CurrentVisualLabel;
            }

            string p2XLabel = "-";
            string p2FacingLabel = "-";
            DebugFighterParticipant p2 = simulationSession.ParticipantP2;
            if (p2 != null && p2.Motor != null)
            {
                p2XLabel = p2.Motor.LogicalX.ToString("0.00");
                p2FacingLabel = p2.Motor.FacingRight ? "R" : "L";
            }

            string p2HitLabel = "0";
            if (p2 != null)
            {
                p2HitLabel = p2.HitCount.ToString();
            }

            string punchPhase = simulationSession.GetPunchPhaseLabel();
            if (string.IsNullOrEmpty(punchPhase))
            {
                punchPhase = "Idle";
            }

            // AttackResult / PunchHitDone も同じ attackState を再利用
            string attackResult = "None";
            string punchHitDone = "0";
            if (attackState != null)
            {
                attackResult = attackState.LastAttackResult;
                if (attackState.HasCurrentJPunchHit)
                {
                    punchHitDone = "1";
                }
            }

            if (string.IsNullOrEmpty(attackResult))
            {
                attackResult = "None";
            }

            string text = "";
            // Tick / Combat は timeState。AF だけ AttackState。
            text = text + "Tick/Combat/AF : " + timeState.SimulationTick
                + " / " + timeState.CombatFrame
                + " / " + actionFrameValue + "\n";
            // Action は AttackState。Pause / HitStop は timeState。
            text = text + "Action/Pause/HS: " + actionPlayingLabel
                + " / " + pauseLabel
                + " / " + timeState.HitStopRemaining + "\n";
            text = text + "Step           : " + stepLabel + "\n";
            text = text + "LRUD / Atk H/P : " + leftLabel + rightLabel + upLabel + downLabel
                + " / " + attackHeldLabel + "/" + attackPressedLabel + "\n";
            text = text + "P1 X/Face      : " + p1XLabel
                + " / " + p1FacingLabel + "\n";
            text = text + "Sprite         : " + visualLabel + "\n";
            text = text + "P2 X/Face/Hit  : " + p2XLabel
                + " / " + p2FacingLabel
                + " / " + p2HitLabel + "\n";

            // Push Box（段階10B-3）: ASCII のみ。常時大量ログは出さず HUD で確認する。
            string pushDistLabel = simulationSession.LastPushCenterDistance.ToString("0.00");
            string pushOverlapLabel = simulationSession.LastPushWasOverlapping ? "1" : "0";
            text = text + "Push Dist/Over : " + pushDistLabel
                + " / " + pushOverlapLabel + "\n";

            // Box 可視化（段階11A）: P1 の World Box 状態を短く表示。
            string pushBoxOnLabel = "0";
            string hurtBoxOnLabel = "0";
            string hitBoxOnLabel = "0";
            string hitBoxCenterXLabel = "-";
            if (p1 != null)
            {
                DebugBox2D pushWorld = p1.EvaluateWorldPushBox();
                DebugBox2D hurtWorld = p1.EvaluateWorldHurtBox();
                DebugBox2D hitWorld = p1.EvaluateWorldHitBox();
                if (pushWorld != null && pushWorld.IsActive)
                {
                    pushBoxOnLabel = "1";
                }

                if (hurtWorld != null && hurtWorld.IsActive)
                {
                    hurtBoxOnLabel = "1";
                }

                if (hitWorld != null && hitWorld.IsActive)
                {
                    hitBoxOnLabel = "1";
                    hitBoxCenterXLabel = hitWorld.CenterX.ToString("0.00");
                }
            }

            text = text + "Box P/H/Hit    : " + pushBoxOnLabel
                + " / " + hurtBoxOnLabel
                + " / " + hitBoxOnLabel + "\n";
            text = text + "HitBox CenterX : " + hitBoxCenterXLabel + "\n";

            text = text + "Punch Phase    : " + punchPhase + "\n";
            text = text + "AttackResult   : " + attackResult + "\n";
            text = text + "PunchHitDone   : " + punchHitDone + "\n";
            text = text + "Status         : " + statusLabel + "\n";
            text = text + "\n";
            text = text + "操作:\n";
            text = text + "Space=Pause切替      .=1Tick送り\n";
            text = text + "H=HitStop            A=Action開始\n";
            text = text + "R=Actionリセット     矢印=方向\n";
            text = text + "J=Attack";
            return text;
        }
    }
}
