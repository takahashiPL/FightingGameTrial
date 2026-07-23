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
        [Tooltip("時間状態と Fighter 参照へ到達するための SimulationSession です。")]
        [SerializeField]
        private SimulationSession simulationSession;

        [Tooltip(
            "HUD本文を表示する TextMeshProUGUI です。"
            + " Font Asset には Scene 上で NotoSansJP-Regular SDF を設定してください。"
        )]
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
        /// 画面に出す全文を組み立てます。
        /// 縦幅節約のため、入力・Fighter行を詰めています（操作説明は増やしません）。
        /// </summary>
        private string BuildHudText(SimulationTimeState timeState)
        {
            string pauseLabel = timeState.IsPaused ? "はい" : "いいえ";
            string actionPlayingLabel = timeState.IsActionPlaying ? "はい" : "いいえ";

            string stepLabel = timeState.LastStepResult;
            if (string.IsNullOrEmpty(stepLabel))
            {
                stepLabel = "未実行";
            }

            string statusLabel = timeState.LastStatusMessage;
            if (string.IsNullOrEmpty(statusLabel))
            {
                statusLabel = "（なし）";
            }

            string hitStopLabel = timeState.HitStopRemaining.ToString();

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

            string fighterXLabel = "-";
            string fighterFacingLabel = "-";
            DebugFighterMotor motor = simulationSession.DebugFighterMotor;
            if (motor != null)
            {
                fighterXLabel = motor.LogicalX.ToString("0.00");
                fighterFacingLabel = motor.FacingRight ? "右" : "左";
            }

            string visualLabel = "Idle";
            DebugFighterVisual visual = simulationSession.DebugFighterVisual;
            if (visual != null)
            {
                visualLabel = visual.CurrentVisualLabel;
            }

            string text = "";
            text = text + "SimulationTick : " + timeState.SimulationTick + "\n";
            text = text + "CombatFrame    : " + timeState.CombatFrame + "\n";
            text = text + "ActionFrame    : " + timeState.ActionFrame + "\n";
            text = text + "Action再生中   : " + actionPlayingLabel + "\n";
            text = text + "Pause/Step/HS  : " + pauseLabel
                + " / " + stepLabel
                + " / " + hitStopLabel + "\n";
            text = text + "入力 L R U D   : " + leftLabel
                + " " + rightLabel
                + " " + upLabel
                + " " + downLabel + "\n";
            text = text + "AtkHeld/Pressed: " + attackHeldLabel
                + " / " + attackPressedLabel + "\n";
            text = text + "Fighter X/向き : " + fighterXLabel
                + " / " + fighterFacingLabel + "\n";
            text = text + "現在Sprite     : " + visualLabel + "\n";
            text = text + "状態           : " + statusLabel + "\n";
            text = text + "\n";
            // 操作説明は増やさない（縦幅節約）
            text = text + "操作:\n";
            text = text + "Space=Pause切替      .=1Tick送り\n";
            text = text + "H=HitStop            A=Action開始\n";
            text = text + "R=Actionリセット     矢印=方向\n";
            text = text + "J=Attack";
            return text;
        }
    }
}
