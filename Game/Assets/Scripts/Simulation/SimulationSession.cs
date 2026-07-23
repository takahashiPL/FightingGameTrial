using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    /// docs/rules.md §10.3 の処理順を、ここで上から追える形にします。
    ///
    /// 段階8:
    /// - CurrentInput.Attack（Held）から立ち上がり（Pressed）を作る
    /// - Jパンチ開始は Action 停止中かつ HitStop なしのときだけ
    /// - ActionFrame に応じて DebugFighterVisual で Sprite を切り替える
    /// - Jパンチは終了フレームで自動停止（AキーのデバッグActionは自動停止しない）
    ///
    /// 処理順（このメソッド内）:
    /// 1. SimulationTick +1
    /// 2. 入力サンプリング
    /// 3. Attack 立ち上がり判定
    /// 4. HitStop判定
    /// 5. HitStop中なら Combat/Action/移動を進めず、previousAttackHeld だけ更新して終了
    /// 6. CombatFrame +1
    /// 7. AttackPressed かつ Action停止中なら Jパンチ開始
    /// 8. Fighter移動
    /// 9. ActionFrame進行
    /// 10. Visual反映
    /// 11. Jパンチ終了フレームなら停止・0へ戻す
    /// 12. 状態メッセージ更新 / previousAttackHeld 更新
    ///
    /// Pause中は ClockDriver が自動でここを呼ばないため、自動攻撃進行もしません。
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

        [Tooltip(
            "ActionFrame に応じて Idle/Attack Sprite を切り替える DebugFighterVisual です。"
            + " HitStopなしの Combat 処理内、および A/R 直後の再描画で呼び出します。"
        )]
        [SerializeField]
        private DebugFighterVisual debugFighterVisual;

        [Header("時間状態（進行ロジックは持たない入れ物）")]
        [Tooltip("SimulationTick / CombatFrame / ActionFrame / 確定入力 / HitStop残り / 状態メッセージを保持します。")]
        [SerializeField]
        private SimulationTimeState timeState = new SimulationTimeState();

        [Header("Consoleログ（毎tickは出さない）")]
        [Tooltip("何 SimulationTick ごとに1回ログを出すか。60なら約1秒に1回です。")]
        [SerializeField]
        private int consoleLogIntervalTicks = 60;

        /// <summary>
        /// 直前 SimulationTick の Attack Held です。
        /// Held から Pressed（立ち上がり）を作るために使います。
        /// </summary>
        private bool previousAttackHeld;

        /// <summary>
        /// この SimulationTick で Attack の立ち上がりが起きたか（HUD / ログ用）。
        /// </summary>
        private bool attackPressedThisTick;

        /// <summary>
        /// 外部（ClockDriverなど）から読むための時間状態です。
        /// </summary>
        public SimulationTimeState TimeState
        {
            get { return timeState; }
        }

        /// <summary>
        /// HUD などから Fighter 移動値を読むための参照です。
        /// </summary>
        public DebugFighterMotor DebugFighterMotor
        {
            get { return debugFighterMotor; }
        }

        /// <summary>
        /// HUD などから見た目名を読むための参照です。
        /// </summary>
        public DebugFighterVisual DebugFighterVisual
        {
            get { return debugFighterVisual; }
        }

        /// <summary>
        /// この SimulationTick の Attack 立ち上がり（0/1 表示用）。
        /// </summary>
        public bool AttackPressedThisTick
        {
            get { return attackPressedThisTick; }
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

            if (debugFighterVisual == null)
            {
                Debug.LogError(
                    "SimulationSession: DebugFighterVisual が未設定です。"
                );
            }

            if (timeState == null)
            {
                timeState = new SimulationTimeState();
            }

            timeState.ResetToInitialValues();
            timeState.LastStepResult = "未実行";
            timeState.LastStatusMessage = "SimulationSession を初期化しました。まだ自動進行前です。";

            previousAttackHeld = false;
            attackPressedThisTick = false;
        }

        /// <summary>
        /// 論理 SimulationTick をちょうど1回分進めます。
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
            // 3. Attack 立ち上がり判定（CurrentInput のみ。Keyboard は読まない）
            //
            //    なぜ Held から Pressed を作るか:
            //    CurrentInput.Attack は押し続けでも true のままです。
            //    「押した瞬間」だけ攻撃開始するには、前tickの Held と比較が必要です。
            //
            //    押しっぱなし連打防止:
            //    previousAttackHeld が true のあいだは attackPressedThisTick が false。
            //    攻撃終了後も J を離すまで立ち上がりは起きません。
            // ------------------------------------------------------------
            bool attackHeldNow = false;
            if (timeState.CurrentInput != null)
            {
                attackHeldNow = timeState.CurrentInput.Attack;
            }

            attackPressedThisTick = attackHeldNow && (previousAttackHeld == false);

            // ------------------------------------------------------------
            // 4〜5. HitStopRemaining を確認
            //    HitStop中は CombatFrame / ActionFrame / 移動 / 攻撃開始を進めない。
            //    ただし入力サンプルと立ち上がり判定は上で済ませている。
            //
            //    HitStop中に AttackPressed が立っても、このtickでは攻撃開始しない。
            //    入力予約はまだ作らない。
            //    HitStop終了後に押しっぱなしでも自動開始しない
            //    （previousAttackHeld を更新するため、離して再押しが必要）。
            // ------------------------------------------------------------
            if (timeState.HitStopRemaining > 0)
            {
                timeState.HitStopRemaining = timeState.HitStopRemaining - 1;
                if (timeState.HitStopRemaining < 0)
                {
                    timeState.HitStopRemaining = 0;
                }

                timeState.LastStatusMessage =
                    "HitStop: Combat/Action/Fighter stop";

                if (timeState.HitStopRemaining == 0)
                {
                    Debug.Log("[FightDebug] Test HitStop ended");
                }

                // 立ち上がりを「消費」して、解除後の押しっぱなし連打を防ぐ
                previousAttackHeld = attackHeldNow;

                WriteConsoleLogIfNeeded();
                return;
            }

            // ------------------------------------------------------------
            // 6. HitStopなし: CombatFrame を1進める
            // ------------------------------------------------------------
            timeState.CombatFrame = timeState.CombatFrame + 1;

            // ------------------------------------------------------------
            // 7. AttackPressed かつ Action停止中なら Jパンチ開始
            //    攻撃中の再Jは IsActionPlaying のため開始しない（現在の攻撃終了を優先）。
            // ------------------------------------------------------------
            if (attackPressedThisTick && (timeState.IsActionPlaying == false))
            {
                StartJPunchAttack();
            }

            // ------------------------------------------------------------
            // 8. Fighter 移動（攻撃中も左右移動可）
            // ------------------------------------------------------------
            if (debugFighterMotor != null)
            {
                debugFighterMotor.ProcessOneCombatFrame(timeState.CurrentInput);
            }

            // ------------------------------------------------------------
            // 9. ActionFrame進行
            // ------------------------------------------------------------
            if (timeState.IsActionPlaying)
            {
                timeState.ActionFrame = timeState.ActionFrame + 1;
                if (timeState.ActionFrame < 0)
                {
                    timeState.ActionFrame = 0;
                }
            }

            // ------------------------------------------------------------
            // 10. Visual反映（ActionFrame と見た目の対応）
            // ------------------------------------------------------------
            RefreshFighterVisual();

            // ------------------------------------------------------------
            // 11. Jパンチ終了フレームなら停止・0へ戻す
            //     AキーのデバッグAction（IsJPunchAttack=false）はここでは終了しない。
            // ------------------------------------------------------------
            if (timeState.IsJPunchAttack && timeState.IsActionPlaying)
            {
                int endFrame = 12;
                if (debugFighterVisual != null)
                {
                    endFrame = debugFighterVisual.ActionEndFrame;
                }

                if (timeState.ActionFrame >= endFrame)
                {
                    EndJPunchAttack();
                    RefreshFighterVisual();
                }
            }

            // ------------------------------------------------------------
            // 12. 状態メッセージ / previousAttackHeld 更新
            // ------------------------------------------------------------
            if (timeState.IsActionPlaying)
            {
                if (timeState.IsJPunchAttack)
                {
                    timeState.LastStatusMessage = "J Punch active";
                }
                else
                {
                    timeState.LastStatusMessage = "Debug Action active";
                }
            }
            else
            {
                timeState.LastStatusMessage = "Combat ok / Action stop";
            }

            previousAttackHeld = attackHeldNow;

            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// A/R キー直後など、tick外で Action 状態が変わったときに見た目だけ合わせます。
        /// </summary>
        public void RefreshFighterVisual()
        {
            if (debugFighterVisual == null || timeState == null)
            {
                return;
            }

            debugFighterVisual.Apply(
                timeState.IsActionPlaying,
                timeState.ActionFrame,
                timeState.IsJPunchAttack
            );
        }

        /// <summary>
        /// Jキー時限パンチを開始します。
        /// </summary>
        private void StartJPunchAttack()
        {
            timeState.ActionFrame = 0;
            timeState.IsActionPlaying = true;
            timeState.IsJPunchAttack = true;
            timeState.LastStatusMessage = "J Punch start";
            Debug.Log("[FightDebug] Attack started");
        }

        /// <summary>
        /// Jキー時限パンチを終了し、ActionFrame を0へ戻します。
        /// </summary>
        private void EndJPunchAttack()
        {
            timeState.ActionFrame = 0;
            timeState.IsActionPlaying = false;
            timeState.IsJPunchAttack = false;
            timeState.LastStatusMessage = "J Punch end";
            Debug.Log("[FightDebug] Attack ended");
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
            int attackPressedValue = attackPressedThisTick ? 1 : 0;

            string fighterPart = "";
            if (debugFighterMotor != null)
            {
                string facingLabel = debugFighterMotor.FacingRight ? "true" : "false";
                fighterPart =
                    " FighterX=" + debugFighterMotor.LogicalX.ToString("0.00")
                    + " FacingRight=" + facingLabel;
            }

            string visualLabel = "Idle";
            if (debugFighterVisual != null)
            {
                visualLabel = debugFighterVisual.CurrentVisualLabel;
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
                + " AttackPressed=" + attackPressedValue
                + " FighterVisual=" + visualLabel
                + fighterPart
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
