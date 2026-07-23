using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    /// docs/rules.md §10.3 の処理順を、ここで上から追える形にします。
    ///
    /// 段階5:
    /// - SimulationTick は毎回 +1（HitStop中も進む）
    /// - HitStopRemaining > 0 のあいだは CombatFrame も ActionFrame も進めない
    /// - HitStopなしなら CombatFrame を +1
    /// - さらに IsActionPlaying なら ActionFrame も +1（停止中は据え置き）
    ///
    /// ActionFrame が CombatFrame と別である理由:
    /// CombatFrameは「対戦全体の進行」、ActionFrameは「今の行動・技の進行」です。
    /// 同じtickで両方進むこともあれば、Action停止中はCombatだけ進むこともあります。
    ///
    /// やらないこと（外側＝SimulationClockDriver側）:
    /// - Pause切替 / Step / テスト用HitStop / テスト用Action開始・Reset のキー取得
    /// - 自動進行するかどうかの判断
    ///
    /// Pause（自動進行停止）と HitStop（Combat/Action停止）と Action停止（ActionFrameだけ停止）は混同しません。
    /// </summary>
    public class SimulationSession : MonoBehaviour
    {
        [Header("時間状態（進行ロジックは持たない入れ物）")]
        [Tooltip("SimulationTick / CombatFrame / ActionFrame / HitStop残り / 状態メッセージを保持します。")]
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
            //    1以上なら残りを1減らし、CombatFrameもActionFrameも進めずに終了する。
            //    return する理由: このtickの戦闘進行（手順4〜18相当）をスキップするため。
            // ------------------------------------------------------------
            if (timeState.HitStopRemaining > 0)
            {
                timeState.HitStopRemaining = timeState.HitStopRemaining - 1;
                if (timeState.HitStopRemaining < 0)
                {
                    timeState.HitStopRemaining = 0;
                }

                // HitStop中は CombatFrame も ActionFrame も据え置き
                timeState.LastStatusMessage =
                    "HitStop中。CombatFrameとActionFrameは進めませんでした";

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

            // ------------------------------------------------------------
            // 6〜8. IsActionPlaying を確認し、再生中だけ ActionFrame を進める
            //    Action停止中でも CombatFrame は上で既に進んでいる。
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
                // ActionFrame は据え置き（CombatFrameだけ進んだ）
                timeState.LastStatusMessage =
                    "HitStopなし。CombatFrameを進め、ActionFrameは停止中です";
            }

            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// Console を埋めないよう、間隔ごとのみログします。
        /// Action再生中・HitStop中の毎tickログは出しません
        /// （開始/終了/Resetの1件ログは別途出します）。
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

            Debug.Log(
                "[FightDebug] SimulationTick=" + timeState.SimulationTick
                + " / CombatFrame=" + timeState.CombatFrame
                + " / ActionFrame=" + timeState.ActionFrame
                + " / IsActionPlaying=" + actionPlayingLabel
                + " / HitStopRemaining=" + timeState.HitStopRemaining
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
