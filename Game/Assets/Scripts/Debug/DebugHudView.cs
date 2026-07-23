using FightingGameTrial.Simulation;
using TMPro;
using UnityEngine;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// デバッグ用の画面表示だけを担当します。
    ///
    /// 責務:
    /// - SimulationSession 経由で SimulationTimeState を読む
    /// - TextMeshProUGUI に日本語ラベル付きの文字列を設定する
    ///
    /// やらないこと:
    /// - Pause / Step / Tick / Action 進行の制御
    /// - キー入力の読み取り
    /// - GameObject.Find や Singleton による参照取得
    /// - フォントの実行時生成（日本語フォントは Scene 上の NotoSansJP-Regular SDF を使用）
    ///
    /// なぜ制御ロジックを持たないか:
    /// HUDは「今の状態を見せる窓」です。
    /// 進行や入力の判断をここへ入れると、
    /// SimulationClockDriver との二重管理になり学習しづらくなります。
    /// </summary>
    public class DebugHudView : MonoBehaviour
    {
        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("時間状態へ到達するための SimulationSession です。")]
        [SerializeField]
        private SimulationSession simulationSession;

        [Tooltip(
            "HUD本文を表示する TextMeshProUGUI です。"
            + " Font Asset には Scene 上で NotoSansJP-Regular SDF を設定してください。"
        )]
        [SerializeField]
        private TextMeshProUGUI hudText;

        /// <summary>
        /// Unityが有効化直後に1回呼びます。
        /// 参照がつながっているかだけ検査します。
        /// </summary>
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

        /// <summary>
        /// Unityが描画フレームごとに呼びます。
        /// 論理 SimulationTick とは別に、画面表示だけを毎フレーム更新します。
        /// Consoleログは出しません。
        /// </summary>
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
        /// 学習用に、項目ごとの意味が上から追える並びへしています。
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

            // HitStopRemaining の実値を表示します。
            string hitStopLabel = timeState.HitStopRemaining.ToString();

            string text = "";
            text = text + "SimulationTick : " + timeState.SimulationTick + "\n";
            text = text + "CombatFrame    : " + timeState.CombatFrame + "\n";
            text = text + "ActionFrame    : " + timeState.ActionFrame + "\n";
            text = text + "Action再生中   : " + actionPlayingLabel + "\n";
            text = text + "Pause中        : " + pauseLabel + "\n";
            text = text + "直近Step       : " + stepLabel + "\n";
            text = text + "HitStop残り    : " + hitStopLabel + "\n";
            text = text + "状態           : " + statusLabel + "\n";
            text = text + "\n";
            text = text + "操作:\n";
            text = text + "Space = Pause切替\n";
            text = text + ".     = 1 SimulationTick送り（Pause中のみ）\n";
            text = text + "H     = テスト用HitStop発生\n";
            text = text + "A     = テスト用Action開始\n";
            text = text + "R     = Action停止・ActionFrameリセット";
            return text;
        }
    }
}
