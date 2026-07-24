using FightingGameTrial.Combat;
using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    ///
    /// 段階10:
    /// - 移動入力と Facing を分離する
    /// - 移動後の PlayerX / DummyX から「相手と向き合う Facing」を確定する
    /// - Facing 確定のあとで Hit 判定を行う（向き込み距離判定のため）
    ///
    /// 段階9（維持）:
    /// - Active Frame でダミーへの論理座標 Hit 判定
    /// - Hit 成立で既存 HitStopRemaining を設定（同tickでは減らさない）
    /// - 1攻撃1Hit（HasCurrentJPunchHit）
    ///
    /// HitStop中:
    /// Combat 処理全体をスキップするため、移動も Facing 更新もしない。
    /// </summary>
    public class SimulationSession : MonoBehaviour
    {
        private const int PunchStartupEndFrame = 3;
        private const int PunchActiveStartFrame = 4;
        private const int PunchActiveEndFrame = 6;
        private const int PunchHitStopFrames = 6;

        /// <summary>
        /// PlayerX と DummyX がほぼ同じときの Facing 維持用しきい値です。
        /// 完全一致や微小な誤差で毎フレーム向きが反転しないようにします。
        /// </summary>
        private const float FacingSameXEpsilon = 0.001f;

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("物理キーの最新状態を持つ DebugGameplayInput です。")]
        [SerializeField]
        private DebugGameplayInput debugGameplayInput;

        [Tooltip("テスト用プレイヤーの左右移動を行う DebugFighterMotor です。")]
        [SerializeField]
        private DebugFighterMotor debugFighterMotor;

        [Tooltip("Idle/Attack Sprite を切り替える DebugFighterVisual です。")]
        [SerializeField]
        private DebugFighterVisual debugFighterVisual;

        [Tooltip("相手ダミーの DebugDummyTarget です。Hit通知先です。")]
        [SerializeField]
        private DebugDummyTarget debugDummyTarget;

        [Header("Jパンチ判定（段階9）")]
        [Tooltip(
            "Jパンチの攻撃距離（ワールド単位）です。"
            + " 向いている側に相手がいて、この距離以内なら Active で Hit します。"
        )]
        [SerializeField]
        private float attackRange = 1.35f;

        [Header("時間状態")]
        [Tooltip("SimulationTick / CombatFrame / ActionFrame / HitStop / 攻撃結果などを保持します。")]
        [SerializeField]
        private SimulationTimeState timeState = new SimulationTimeState();

        [Header("Consoleログ（毎tickは出さない）")]
        [Tooltip("何 SimulationTick ごとに1回ログを出すか。60なら約1秒に1回です。")]
        [SerializeField]
        private int consoleLogIntervalTicks = 60;

        private bool previousAttackHeld;
        private bool attackPressedThisTick;

        public SimulationTimeState TimeState
        {
            get { return timeState; }
        }

        public DebugFighterMotor DebugFighterMotor
        {
            get { return debugFighterMotor; }
        }

        public DebugFighterVisual DebugFighterVisual
        {
            get { return debugFighterVisual; }
        }

        public DebugDummyTarget DebugDummyTarget
        {
            get { return debugDummyTarget; }
        }

        public bool AttackPressedThisTick
        {
            get { return attackPressedThisTick; }
        }

        private void Awake()
        {
            if (debugGameplayInput == null)
            {
                Debug.LogError("SimulationSession: DebugGameplayInput が未設定です。");
            }

            if (debugFighterMotor == null)
            {
                Debug.LogError("SimulationSession: DebugFighterMotor が未設定です。");
            }

            if (debugFighterVisual == null)
            {
                Debug.LogError("SimulationSession: DebugFighterVisual が未設定です。");
            }

            if (debugDummyTarget == null)
            {
                Debug.LogError("SimulationSession: DebugDummyTarget が未設定です。");
            }

            if (timeState == null)
            {
                timeState = new SimulationTimeState();
            }

            timeState.ResetToInitialValues();
            timeState.LastStepResult = "未実行";
            timeState.LastStatusMessage = "Session ready";

            previousAttackHeld = false;
            attackPressedThisTick = false;
        }

        public void ProcessOneSimulationTick()
        {
            // 1. SimulationTick +1（HitStop中も進む）
            timeState.SimulationTick = timeState.SimulationTick + 1;

            // 2. 入力サンプリング
            SampleCurrentInputFromGameplay();

            // 3. Attack 立ち上がり判定
            bool attackHeldNow = false;
            if (timeState.CurrentInput != null)
            {
                attackHeldNow = timeState.CurrentInput.Attack;
            }

            attackPressedThisTick = attackHeldNow && (previousAttackHeld == false);

            // 4〜5. 既存 HitStop 判定
            //    Remaining>0 なら Combat 処理へ入らず、ここで1減らして return。
            //    （Hit成立tickで設定した6は、次tickから減り始める）
            if (timeState.HitStopRemaining > 0)
            {
                timeState.HitStopRemaining = timeState.HitStopRemaining - 1;
                if (timeState.HitStopRemaining < 0)
                {
                    timeState.HitStopRemaining = 0;
                }

                timeState.LastStatusMessage = "HitStop: Combat/Action/Fighter stop";

                if (timeState.HitStopRemaining == 0)
                {
                    Debug.Log("[FightDebug] Test HitStop ended");
                }

                previousAttackHeld = attackHeldNow;
                WriteConsoleLogIfNeeded();
                return;
            }

            // 6. CombatFrame +1
            timeState.CombatFrame = timeState.CombatFrame + 1;

            // 7. Dummy の WasHitThisCombatFrame をリセット
            if (debugDummyTarget != null)
            {
                debugDummyTarget.BeginCombatFrame();
            }

            // 8. Jパンチ開始判定
            if (attackPressedThisTick && (timeState.IsActionPlaying == false))
            {
                StartJPunchAttack();
            }

            // 9. Player 移動（ワールド X のみ。Facing はここでは変えない）
            if (debugFighterMotor != null)
            {
                debugFighterMotor.ProcessOneCombatFrame(timeState.CurrentInput);
            }

            // 9.5 Facing 確定（移動後の PlayerX / DummyX から相手向き合い）
            //     Hit 判定が Facing を読む前に、ここで確定する必要がある。
            UpdatePlayerFacingTowardDummy();

            // 10. ActionFrame 進行
            if (timeState.IsActionPlaying)
            {
                timeState.ActionFrame = timeState.ActionFrame + 1;
                if (timeState.ActionFrame < 0)
                {
                    timeState.ActionFrame = 0;
                }
            }

            // 11〜13. Active なら Hit 判定 → Dummy通知 → HitStopRemaining=6 設定
            //         （このtickでは Remaining を減らさない）
            //         Facing は 9.5 で確定済み。
            TryResolveJPunchHit();

            // 14. Visual 更新
            RefreshFighterVisual();

            // 15. Action 終了判定（未Hitなら Miss ログ1回）
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

            // 16. 状態文 / previousAttackHeld
            UpdateStatusMessage();
            previousAttackHeld = attackHeldNow;
            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// HUD用: Idle / Startup / Active / Recovery
        /// </summary>
        public string GetPunchPhaseLabel()
        {
            if (timeState == null || timeState.IsJPunchAttack == false || timeState.IsActionPlaying == false)
            {
                return "Idle";
            }

            int frame = timeState.ActionFrame;
            if (frame >= 1 && frame <= PunchStartupEndFrame)
            {
                return "Startup";
            }

            if (frame >= PunchActiveStartFrame && frame <= PunchActiveEndFrame)
            {
                return "Active";
            }

            if (frame > PunchActiveEndFrame)
            {
                return "Recovery";
            }

            return "Idle";
        }

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
        /// 移動後の論理座標から、Player が Dummy と向き合う Facing を決めます（段階10）。
        ///
        /// 正式方針:
        /// - PlayerX が DummyX より小さい → 右向き
        /// - PlayerX が DummyX より大きい → 左向き
        /// - ほぼ同位置 → 直前 Facing を維持（毎フレーム反転を防ぐ）
        ///
        /// 入力 Left/Right では呼ばない。移動入力と Facing を分離するため。
        /// Inspector で接続済みの debugFighterMotor / debugDummyTarget だけを使う（検索しない）。
        /// </summary>
        private void UpdatePlayerFacingTowardDummy()
        {
            if (debugFighterMotor == null)
            {
                return;
            }

            if (debugDummyTarget == null)
            {
                return;
            }

            float playerX = debugFighterMotor.LogicalX;
            float dummyX = debugDummyTarget.LogicalX;
            float deltaX = dummyX - playerX;

            // 同位置付近では向きを切り替えない。
            // 理由: 浮動小数の微小差やすれ違い直後に、毎 CombatFrame で flipX が点滅するのを防ぐため。
            if (deltaX > FacingSameXEpsilon)
            {
                // Dummy が右側 → Player は右向き（相手と向き合う）
                debugFighterMotor.SetFacingRight(true);
            }
            else if (deltaX < -FacingSameXEpsilon)
            {
                // Dummy が左側 → Player は左向き
                debugFighterMotor.SetFacingRight(false);
            }
            else
            {
                // |deltaX| <= epsilon: 直前 Facing を維持（何もしない）
            }
        }

        private void StartJPunchAttack()
        {
            timeState.ActionFrame = 0;
            timeState.IsActionPlaying = true;
            timeState.IsJPunchAttack = true;
            timeState.HasCurrentJPunchHit = false;
            timeState.LastAttackResult = "None";
            timeState.LastStatusMessage = "J Punch start";
            Debug.Log("[FightDebug] Attack started");
        }

        private void EndJPunchAttack()
        {
            // Miss は攻撃終了時に一度だけ（Active毎tickでは出さない）
            if (timeState.HasCurrentJPunchHit == false)
            {
                timeState.LastAttackResult = "Miss";
                Debug.Log("[FightDebug] Punch missed");
            }

            timeState.ActionFrame = 0;
            timeState.IsActionPlaying = false;
            timeState.IsJPunchAttack = false;
            timeState.HasCurrentJPunchHit = false;
            timeState.LastStatusMessage = "J Punch end";
            Debug.Log("[FightDebug] Attack ended");
        }

        /// <summary>
        /// Active Frame の Hit 判定と、成立時の Dummy通知 / HitStop 設定。
        /// </summary>
        private void TryResolveJPunchHit()
        {
            if (timeState.IsJPunchAttack == false)
            {
                return;
            }

            if (debugFighterMotor == null || debugDummyTarget == null)
            {
                return;
            }

            bool isHit = DebugPunchHitResolver.TryResolveHit(
                timeState.IsJPunchAttack,
                timeState.ActionFrame,
                PunchActiveStartFrame,
                PunchActiveEndFrame,
                timeState.HasCurrentJPunchHit,
                debugFighterMotor.LogicalX,
                debugFighterMotor.FacingRight,
                debugDummyTarget.LogicalX,
                attackRange
            );

            if (isHit == false)
            {
                return;
            }

            // 12. Dummy へ通知
            debugDummyTarget.ReceiveHit(timeState.CombatFrame);
            timeState.HasCurrentJPunchHit = true;
            timeState.LastAttackResult = "Hit";
            timeState.LastStatusMessage = "Punch Hit";

            Debug.Log(
                "[FightDebug] Punch hit Dummy at CombatFrame=" + timeState.CombatFrame
            );

            // 13. 既存 HitStop を開始（同tickでは減らさない）
            //     次の SimulationTick 先頭の if (HitStopRemaining > 0) から Combat 停止が始まる。
            timeState.HitStopRemaining = PunchHitStopFrames;
        }

        private void UpdateStatusMessage()
        {
            if (timeState.IsActionPlaying)
            {
                if (timeState.IsJPunchAttack)
                {
                    timeState.LastStatusMessage = "J Punch " + GetPunchPhaseLabel();
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
        }

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
            int punchHitDone = timeState.HasCurrentJPunchHit ? 1 : 0;

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

            string dummyPart = "";
            if (debugDummyTarget != null)
            {
                dummyPart =
                    " DummyX=" + debugDummyTarget.LogicalX.ToString("0.00")
                    + " DummyHitCount=" + debugDummyTarget.HitCount;
            }

            string attackResult = timeState.LastAttackResult;
            if (string.IsNullOrEmpty(attackResult))
            {
                attackResult = "None";
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
                + dummyPart
                + " PunchPhase=" + GetPunchPhaseLabel()
                + " AttackResult=" + attackResult
                + " PunchHitDone=" + punchHitDone
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
