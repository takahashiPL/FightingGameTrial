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
    /// 責務:
    /// - SimulationSession 経由で SimulationTimeState / DebugFighterMotor を読む
    /// - TextMeshProUGUI に日本語ラベル付きの文字列を設定する
    ///
    /// やらないこと:
    /// - Pause / Step / Tick / Action / 移動の制御
    /// - キー入力の読み取り
    /// - GameObject.Find や Singleton による参照取得
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
        /// 縦幅節約のため、入力行は詰めています（操作説明は増やしません）。
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
            string attackLabel = input.Attack ? "1" : "0";

            // Fighter 表示（Session経由。無ければ仮表示）
            string fighterXLabel = "-";
            string fighterFacingLabel = "-";
            DebugFighterMotor motor = simulationSession.DebugFighterMotor;
            if (motor != null)
            {
                fighterXLabel = motor.LogicalX.ToString("0.00");
                fighterFacingLabel = motor.FacingRight ? "右" : "左";
            }

            string text = "";
            text = text + "SimulationTick : " + timeState.SimulationTick + "\n";
            text = text + "CombatFrame    : " + timeState.CombatFrame + "\n";
            text = text + "ActionFrame    : " + timeState.ActionFrame + "\n";
            text = text + "Action再生中   : " + actionPlayingLabel + "\n";
            text = text + "Pause中        : " + pauseLabel + "\n";
            text = text + "直近Step       : " + stepLabel + "\n";
            text = text + "HitStop残り    : " + hitStopLabel + "\n";
            // 入力行を詰めて縦幅を確保（Fighter表示追加のため）
            text = text + "入力Sample/Tick: " + input.SampleSequence
                + " / " + input.SampledAtSimulationTick + "\n";
            text = text + "方向/Attack    : L=" + leftLabel
                + " R=" + rightLabel
                + " U=" + upLabel
                + " D=" + downLabel
                + " A=" + attackLabel + "\n";
            text = text + "Fighter X      : " + fighterXLabel + "\n";
            text = text + "Fighter向き    : " + fighterFacingLabel + "\n";
            text = text + "状態           : " + statusLabel + "\n";
            text = text + "\n";
            // 操作説明のみ短縮（縦幅節約）。増やさない。
            text = text + "操作:\n";
            text = text + "Space=Pause切替      .=1Tick送り\n";
            text = text + "H=HitStop            A=Action開始\n";
            text = text + "R=Actionリセット     矢印=方向\n";
            text = text + "J=Attack";
            return text;
        }
    }
}
