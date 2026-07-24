using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using FightingGameTrial.Simulation;
using TMPro;
using UnityEngine;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// デバッグ用の画面表示だけを担当します。
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
            string pauseLabel = timeState.IsPaused ? "Yes" : "No";
            string actionPlayingLabel = timeState.IsActionPlaying ? "Yes" : "No";

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
            DebugFighterParticipant p1 = simulationSession.ParticipantP1;
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
            DebugDummyTarget dummy = simulationSession.DebugDummyTarget;
            if (dummy != null)
            {
                p2HitLabel = dummy.HitCount.ToString();
            }

            string punchPhase = simulationSession.GetPunchPhaseLabel();
            string attackResult = timeState.LastAttackResult;
            if (string.IsNullOrEmpty(attackResult))
            {
                attackResult = "None";
            }

            string punchHitDone = timeState.HasCurrentJPunchHit ? "1" : "0";

            string text = "";
            text = text + "Tick/Combat/AF : " + timeState.SimulationTick
                + " / " + timeState.CombatFrame
                + " / " + timeState.ActionFrame + "\n";
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
