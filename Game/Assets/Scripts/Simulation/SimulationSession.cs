using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    /// docs/rules.md §10.3 の処理順を、将来ここで上から追える形に育てます。
    ///
    /// 今回（段階1）:
    /// - SimulationTick を1増やす
    /// - HitStop未実装のため CombatFrame も1増やす
    /// - 状態メッセージを更新する
    /// - 一定間隔でのみ Console にログを出す
    ///
    /// 今回はやらないこと:
    /// - Pause / Step / HitStop / 入力本処理 / CSV / 判定
    ///
    /// 設計メモ（将来）:
    /// Pause切替やStep要求の取得は、SimulationTickが止まっている間も必要です。
    /// そのためデバッグ操作の取得とPause切替は ProcessOneSimulationTick の外側で行います。
    /// </summary>
    public class SimulationSession : MonoBehaviour
    {
        [Header("時間状態（進行ロジックは持たない入れ物）")]
        [Tooltip("SimulationTick / CombatFrame / 状態メッセージを保持します。")]
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
        /// ここでは初期値を整え、開始メッセージだけ残します。
        /// </summary>
        private void Awake()
        {
            if (timeState == null)
            {
                timeState = new SimulationTimeState();
            }

            timeState.ResetToInitialValues();
            timeState.LastStatusMessage = "SimulationSession を初期化しました。まだ自動進行前です。";
        }

        /// <summary>
        /// 論理 SimulationTick をちょうど1回分進めます。
        /// SimulationClockDriver から、1/60秒ごと（または catch-up 分）に呼ばれます。
        /// Unityの Update 自体がゲーム仕様の正本ではありません。
        /// </summary>
        public void ProcessOneSimulationTick()
        {
            // ------------------------------------------------------------
            // 将来: 手順1〜2（入力サンプリング・入力履歴）をここに追加する
            // 今回は未実装（空の予約位置）
            // ------------------------------------------------------------

            // ------------------------------------------------------------
            // 手順相当: SimulationTick 番号を1進める
            // ------------------------------------------------------------
            timeState.SimulationTick = timeState.SimulationTick + 1;

            // ------------------------------------------------------------
            // 将来: HitStop 分岐を入れる位置（rules.md §10.3 手順3）
            //
            // if (HitStopRemaining > 0)
            // {
            //     HitStopRemaining を1減らす
            //     CombatFrame は進めない
            //     LastStatusMessage を更新して return
            // }
            //
            // 今回は HitStop 未実装のため、この分岐はまだ書きません。
            // ------------------------------------------------------------

            // ------------------------------------------------------------
            // 将来: 手順4〜17（状態遷移・移動・判定など）をここに追加する
            // 今回は CombatFrame カウンタだけ進める
            // ------------------------------------------------------------
            timeState.CombatFrame = timeState.CombatFrame + 1;

            // 数値はログ側で組み立てる。ここには処理結果の日本語だけ入れる。
            timeState.LastStatusMessage = "段階1: HitStop未実装のため両方進めました";

            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// Console を埋めないよう、間隔ごとのみログします。
        /// SimulationTick / CombatFrame の数値表示はここで1回だけ組み立てます。
        /// </summary>
        private void WriteConsoleLogIfNeeded()
        {
            if (consoleLogIntervalTicks <= 0)
            {
                return;
            }

            // SimulationTick が間隔の倍数になったときだけ出す
            bool shouldLog = (timeState.SimulationTick % consoleLogIntervalTicks) == 0;
            if (shouldLog == false)
            {
                return;
            }

            Debug.Log(
                "[FightDebug] SimulationTick=" + timeState.SimulationTick
                + " / CombatFrame=" + timeState.CombatFrame
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
