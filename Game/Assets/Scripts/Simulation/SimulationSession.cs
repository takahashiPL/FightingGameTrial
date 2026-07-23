using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    /// docs/rules.md §10.3 の処理順を、ここで上から追える形にします。
    ///
    /// 段階6:
    /// - SimulationTick 開始直後に、DebugGameplayInput の物理入力を論理入力へコピーする
    /// - 入力サンプリングは HitStop 判定より前（HitStop中も入力を受け付ける土台）
    /// - CombatFrame / ActionFrame は従来どおり HitStop で止まる
    ///
    /// Unity Update と SimulationTick の違い:
    /// - Update … 描画フレームごとに動く。物理キーの最新状態を拾う場所。
    /// - SimulationTick … 60Hzの論理更新。ここで入力を1回確定し、戦闘を進める。
    ///
    /// やらないこと（外側＝SimulationClockDriver側）:
    /// - Pause切替 / Step / テスト用HitStop / テスト用Action開始・Reset のキー取得
    /// - 自動進行するかどうかの判断
    /// - ゲーム入力内容の解釈（移動・攻撃など）
    ///
    /// Pause / HitStop / Action停止は混同しません。
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
            // 2〜5. 入力サンプリング（HitStop判定より前）
            //    Update側の物理入力を、この SimulationTick の論理入力として確定する。
            //
            //    なぜ HitStop より前か:
            //    HitStop中でも入力受付・入力保持を行う将来仕様の土台にするため。
            //    CombatFrame / ActionFrame は止まっても、入力サンプルは止めない。
            //
            //    Pause中の注意:
            //    Pause中は自動でここまで来ない。Step で1回だけ来る。
            //    そのため Pause中に物理キーを変えても、Step前の HUD 確定入力は古いまま。
            // ------------------------------------------------------------
            SampleCurrentInputFromGameplay();

            // ------------------------------------------------------------
            // 6〜7. HitStopRemaining を確認
            //    1以上なら残りを1減らし、CombatFrameもActionFrameも進めずに終了する。
            //    return する理由: このtickの戦闘進行（手順4〜18相当）をスキップするため。
            //    入力サンプルは上で既に更新済み。
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
            // 8. HitStopなし: 将来のCombat処理スタブのあと、CombatFrameを1進める
            // ------------------------------------------------------------
            // 将来: 手順4〜17（状態遷移・移動・判定など）をここに追加する

            timeState.CombatFrame = timeState.CombatFrame + 1;

            // ------------------------------------------------------------
            // IsActionPlaying を確認し、再生中だけ ActionFrame を進める
            // Action停止中でも CombatFrame は上で既に進んでいる。
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
        /// DebugGameplayInput の最新物理状態を CurrentInput へコピーして確定します。
        /// SampleSequence を1増やし、SampledAtSimulationTick を現在の SimulationTick にします。
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
            // 左右相殺・優先順位・8方向化はまだしない（方向解決は後段の入力解釈責務）。
            // Attack は Held のみ。PressedThisTick / ReleasedThisTick / バッファはまだ持たない。
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
        /// 入力の押下・離上ごとのログや、毎tickログは出しません。
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
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
