using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    /// docs/rules.md §10.3 の処理順を、ここで上から追える形にします。
    ///
    /// 段階7:
    /// - HitStopなしの Combat 処理内で DebugFighterMotor を1回進める
    /// - 移動に使う入力は確定済み CurrentInput（物理キー直接読みではない）
    /// - HitStop中は Motor を呼ばない（CombatFrame が止まるのと同じ）
    ///
    /// 処理順（このメソッド内）:
    /// 1. SimulationTick +1
    /// 2. 入力サンプリング
    /// 3. HitStop判定
    /// 4. HitStop中なら return（Motorも呼ばない）
    /// 5. CombatFrame +1
    /// 6. DebugFighterMotor へ CurrentInput を渡して1 CombatFrame 分進める
    /// 7. ActionFrame 処理
    /// 8. 状態メッセージ更新
    ///
    /// Pause中は ClockDriver が自動でここを呼ばないため、自動移動もしません。
    /// Pause中の Step ではここが1回だけ呼ばれ、その結果 Motor も1回だけ進みます。
    /// </summary>
    public class SimulationSession : MonoBehaviour
    {
        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip(
            "物理キーの最新状態を持つ DebugGameplayInput です。"
            + " SimulationTick ごとにここから読んで CurrentInput へ確定します。"
        )]
        [SerializeField]
        private DebugGameplayInput debugGameplayInput;

        [Tooltip(
            "テスト用プレイヤーの左右移動を行う DebugFighterMotor です。"
            + " HitStopなしの Combat 処理内でのみ呼び出します。"
        )]
        [SerializeField]
        private DebugFighterMotor debugFighterMotor;

        [Header("時間状態（進行ロジックは持たない入れ物）")]
        [Tooltip("SimulationTick / CombatFrame / ActionFrame / 確定入力 / HitStop残り / 状態メッセージを保持します。")]
        [SerializeField]
        private SimulationTimeState timeState = new SimulationTimeState();

        [Header("Consoleログ（毎tickは出さない）")]
        [Tooltip("何 SimulationTick ごとに1回ログを出すか。60なら約1秒に1回です。")]
        [SerializeField]
        private int consoleLogIntervalTicks = 60;

        /// <summary>
        /// 外部（ClockDriverなど）から読むための時間状態です。
        /// </summary>
        public SimulationTimeState TimeState
        {
            get { return timeState; }
        }

        /// <summary>
        /// HUD などから Fighter 表示値を読むための参照です。
        /// </summary>
        public DebugFighterMotor DebugFighterMotor
        {
            get { return debugFighterMotor; }
        }

        /// <summary>
        /// Unityがこのコンポーネントを有効化した直後に1回呼びます。
        /// </summary>
        private void Awake()
        {
            if (debugGameplayInput == null)
            {
                Debug.LogError(
                    "SimulationSession: DebugGameplayInput が未設定です。"
                );
            }

            if (debugFighterMotor == null)
            {
                Debug.LogError(
                    "SimulationSession: DebugFighterMotor が未設定です。"
                );
            }

            if (timeState == null)
            {
                timeState = new SimulationTimeState();
            }

            timeState.ResetToInitialValues();
            timeState.LastStepResult = "未実行";
            timeState.LastStatusMessage = "SimulationSession を初期化しました。まだ自動進行前です。";
        }

        /// <summary>
        /// 論理 SimulationTick をちょうど1回分進めます。
        /// SimulationClockDriver から呼ばれます（自動進行または Pause中の Step）。
        /// </summary>
        public void ProcessOneSimulationTick()
        {
            // ------------------------------------------------------------
            // 1. SimulationTick を1増やす（HitStop中も進む）
            // ------------------------------------------------------------
            timeState.SimulationTick = timeState.SimulationTick + 1;

            // ------------------------------------------------------------
            // 2. 入力サンプリング（HitStop判定より前）
            // ------------------------------------------------------------
            SampleCurrentInputFromGameplay();

            // ------------------------------------------------------------
            // 3〜4. HitStopRemaining を確認
            //    HitStop中は CombatFrame / ActionFrame / Fighter移動を進めずに終了。
            // ------------------------------------------------------------
            if (timeState.HitStopRemaining > 0)
            {
                timeState.HitStopRemaining = timeState.HitStopRemaining - 1;
                if (timeState.HitStopRemaining < 0)
                {
                    timeState.HitStopRemaining = 0;
                }

                timeState.LastStatusMessage =
                    "HitStop中。CombatFrameとActionFrameは進めませんでした";

                if (timeState.HitStopRemaining == 0)
                {
                    Debug.Log("[FightDebug] Test HitStop ended");
                }

                WriteConsoleLogIfNeeded();
                return;
            }

            // ------------------------------------------------------------
            // 5. HitStopなし: CombatFrame を1進める
            // ------------------------------------------------------------
            timeState.CombatFrame = timeState.CombatFrame + 1;

            // ------------------------------------------------------------
            // 6. Fighter を1 CombatFrame 分進める（確定入力を渡す）
            //    HitStop中は上で return 済みなので、ここには来ない。
            // ------------------------------------------------------------
            if (debugFighterMotor != null)
            {
                debugFighterMotor.ProcessOneCombatFrame(timeState.CurrentInput);
            }

            // ------------------------------------------------------------
            // 7〜8. ActionFrame 処理と状態メッセージ
            // ------------------------------------------------------------
            if (timeState.IsActionPlaying)
            {
                timeState.ActionFrame = timeState.ActionFrame + 1;
                if (timeState.ActionFrame < 0)
                {
                    timeState.ActionFrame = 0;
                }

                timeState.LastStatusMessage =
                    "HitStopなし。CombatFrameとActionFrameを進めました";
            }
            else
            {
                timeState.LastStatusMessage =
                    "HitStopなし。CombatFrameを進め、ActionFrameは停止中です";
            }

            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// DebugGameplayInput の最新物理状態を CurrentInput へコピーして確定します。
        /// </summary>
        private void SampleCurrentInputFromGameplay()
        {
            if (timeState.CurrentInput == null)
            {
                timeState.CurrentInput = new SimulationInputState();
            }

            bool left = false;
            bool right = false;
            bool up = false;
            bool down = false;
            bool attack = false;

            if (debugGameplayInput != null)
            {
                left = debugGameplayInput.IsLeftPressed;
                right = debugGameplayInput.IsRightPressed;
                up = debugGameplayInput.IsUpPressed;
                down = debugGameplayInput.IsDownPressed;
                attack = debugGameplayInput.IsAttackPressed;
            }

            // 同時方向もそのまま保持する。
            // 左右相殺はここではしない。移動解釈は DebugFighterMotor 側。
            timeState.CurrentInput.CopyFromPhysicalAndCommit(
                left,
                right,
                up,
                down,
                attack,
                timeState.SimulationTick
            );
        }

        /// <summary>
        /// Console を埋めないよう、間隔ごとのみログします。
        /// </summary>
        private void WriteConsoleLogIfNeeded()
        {
            if (consoleLogIntervalTicks <= 0)
            {
                return;
            }

            bool shouldLog = (timeState.SimulationTick % consoleLogIntervalTicks) == 0;
            if (shouldLog == false)
            {
                return;
            }

            string actionPlayingLabel = timeState.IsActionPlaying ? "true" : "false";

            SimulationInputState input = timeState.CurrentInput;
            if (input == null)
            {
                input = new SimulationInputState();
            }

            int leftValue = input.Left ? 1 : 0;
            int rightValue = input.Right ? 1 : 0;
            int upValue = input.Up ? 1 : 0;
            int downValue = input.Down ? 1 : 0;
            int attackValue = input.Attack ? 1 : 0;

            string fighterPart = "";
            if (debugFighterMotor != null)
            {
                string facingLabel = debugFighterMotor.FacingRight ? "true" : "false";
                fighterPart =
                    " FighterX=" + debugFighterMotor.LogicalX.ToString("0.00")
                    + " FacingRight=" + facingLabel;
            }

            Debug.Log(
                "[FightDebug] SimulationTick=" + timeState.SimulationTick
                + " / CombatFrame=" + timeState.CombatFrame
                + " / ActionFrame=" + timeState.ActionFrame
                + " / IsActionPlaying=" + actionPlayingLabel
                + " / HitStopRemaining=" + timeState.HitStopRemaining
                + "\nInputSample=" + input.SampleSequence
                + " InputTick=" + input.SampledAtSimulationTick
                + " L=" + leftValue
                + " R=" + rightValue
                + " U=" + upValue
                + " D=" + downValue
                + " Attack=" + attackValue
                + fighterPart
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
