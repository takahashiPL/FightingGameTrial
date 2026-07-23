using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    /// docs/rules.md §10.3 の処理順を、ここで上から追える形にします。
    ///
    /// 段階4:
    /// - SimulationTick は毎回 +1（HitStop中も進む）
    /// - HitStopRemaining > 0 のあいだは CombatFrame を進めない
    /// - HitStopRemaining は SimulationTick ごとに1減る（負数にはしない）
    ///
    /// やらないこと（外側＝SimulationClockDriver側）:
    /// - Pause切替 / Step / テスト用HitStop発生キーの取得
    /// - 自動進行するかどうかの判断
    ///
    /// Pause（自動進行停止）と HitStop（CombatFrameだけ停止）は混同しません。
    /// </summary>
    public class SimulationSession : MonoBehaviour
    {
        [Header("時間状態（進行ロジックは持たない入れ物）")]
        [Tooltip("SimulationTick / CombatFrame / HitStop残り / 状態メッセージを保持します。")]
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
        /// Unityがこのコンポーネントを有効化した直後に1回呼びます。
        /// </summary>
        private void Awake()
        {
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
            // 2. 将来: 入力サンプリング・入力履歴（rules.md §10.3 手順1〜2）
            //    今回は未実装（空の予約位置）
            // ------------------------------------------------------------

            // ------------------------------------------------------------
            // 3〜4. HitStopRemaining を確認
            //    1以上なら残りを1減らし、CombatFrameは進めずに終了する。
            //    return する理由: このtickの戦闘進行（手順4〜18相当）をスキップするため。
            // ------------------------------------------------------------
            if (timeState.HitStopRemaining > 0)
            {
                timeState.HitStopRemaining = timeState.HitStopRemaining - 1;
                if (timeState.HitStopRemaining < 0)
                {
                    timeState.HitStopRemaining = 0;
                }

                timeState.LastStatusMessage = "HitStop中。CombatFrameは進めませんでした";

                // 残りが今ちょうど0になった = このtickでHitStop消費が終わった
                if (timeState.HitStopRemaining == 0)
                {
                    Debug.Log("[FightDebug] Test HitStop ended");
                }

                WriteConsoleLogIfNeeded();
                return;
            }

            // ------------------------------------------------------------
            // 5. HitStopなし: 将来のCombat処理スタブのあと、CombatFrameを1進める
            // ------------------------------------------------------------
            // 将来: 手順4〜17（状態遷移・移動・判定など）をここに追加する

            timeState.CombatFrame = timeState.CombatFrame + 1;

            // 数値はログ側で組み立てる。ここには処理結果の日本語だけ入れる。
            timeState.LastStatusMessage = "HitStopなし。CombatFrameを進めました";

            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// Console を埋めないよう、間隔ごとのみログします。
        /// HitStop中の毎tickログは出しません（終了時の1件ログは上で別途出します）。
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

            Debug.Log(
                "[FightDebug] SimulationTick=" + timeState.SimulationTick
                + " / CombatFrame=" + timeState.CombatFrame
                + " / HitStopRemaining=" + timeState.HitStopRemaining
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
