using FightingGameTrial.Combat;
using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    ///
    /// 段階10B-1:
    /// - P1/P2 を同じ Participant + Motor + Visual 構成で扱う
    /// - P1 は Gameplay 入力、P2 は Neutral 入力（棒立ち）
    /// - 両体の移動後、Hit 前に双方の Facing を相手向きへ確定する
    /// - 攻撃状態の共通化・対称 Hit・Push Box・CharacterDefinition はまだ行わない
    ///
    /// 段階10A（維持）:
    /// - 移動入力と Facing を分離する
    /// - Facing 確定のあとで Hit 判定を行う
    /// - ほぼ同位置なら直前 Facing を維持する
    ///
    /// 段階9（維持）:
    /// - Active Frame で P1→P2 の論理座標 Hit 判定
    /// - Hit 成立で既存 HitStopRemaining を設定（同tickでは減らさない）
    /// - 1攻撃1Hit（HasCurrentJPunchHit）
    ///
    /// 1 CombatFrame 内の処理順:
    /// 1. P1 移動
    /// 2. P2 移動
    /// 3. P1 Facing 確定
    /// 4. P2 Facing 確定
    /// 5. ActionFrame 進行
    /// 6. 既存 P1→P2 暫定 Hit 判定
    /// 7. Visual 更新
    ///
    /// HitStop中:
    /// Combat 処理全体をスキップするため、移動も Facing 更新もしない。
    /// Pause中は自動進行せず、Step 時だけ本メソッドが1回呼ばれます。
    /// </summary>
    public class SimulationSession : MonoBehaviour
    {
        private const int PunchStartupEndFrame = 3;
        private const int PunchActiveStartFrame = 4;
        private const int PunchActiveEndFrame = 6;
        private const int PunchHitStopFrames = 6;

        /// <summary>
        /// selfX と opponentX がほぼ同じときの Facing 維持用しきい値です。
        /// 完全一致や微小な誤差で毎フレーム向きが反転しないようにします。
        /// </summary>
        private const float FacingSameXEpsilon = 0.001f;

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("物理キーの最新状態を持つ DebugGameplayInput です（P1用）。")]
        [SerializeField]
        private DebugGameplayInput debugGameplayInput;

        [Tooltip("P1 参加枠です。Motor / Visual / Opponent を内包します。")]
        [SerializeField]
        private DebugFighterParticipant participantP1;

        [Tooltip("P2 参加枠です。Motor / Visual / Opponent を内包します。")]
        [SerializeField]
        private DebugFighterParticipant participantP2;

        [Tooltip(
            "P2 被弾デバッグ用の DebugDummyTarget です（段階10B-1 案A）。"
            + " HitCount / ReceiveHit のみ。LogicalX の正本は P2 Motor です。"
        )]
        [SerializeField]
        private DebugDummyTarget debugDummyTarget;

        [Header("Jパンチ判定（段階9・暫定）")]
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

        /// <summary>
        /// P2 専用の Neutral 入力です。
        /// P1 の CurrentInput とは別インスタンスで、毎tick new しません。
        /// 静的共有値にもしません（誤って書き換えられるのを防ぐため）。
        /// </summary>
        private SimulationInputState p2NeutralInput;

        public SimulationTimeState TimeState
        {
            get { return timeState; }
        }

        public DebugFighterParticipant ParticipantP1
        {
            get { return participantP1; }
        }

        public DebugFighterParticipant ParticipantP2
        {
            get { return participantP2; }
        }

        /// <summary>
        /// HUD / ClockDriver 互換用。P1 Motor を返します。
        /// </summary>
        public DebugFighterMotor DebugFighterMotor
        {
            get
            {
                if (participantP1 == null)
                {
                    return null;
                }

                return participantP1.Motor;
            }
        }

        /// <summary>
        /// HUD / ClockDriver 互換用。P1 Visual を返します。
        /// </summary>
        public DebugFighterVisual DebugFighterVisual
        {
            get
            {
                if (participantP1 == null)
                {
                    return null;
                }

                return participantP1.Visual;
            }
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

            if (participantP1 == null)
            {
                Debug.LogError("SimulationSession: ParticipantP1 が未設定です。");
            }

            if (participantP2 == null)
            {
                Debug.LogError("SimulationSession: ParticipantP2 が未設定です。");
            }

            if (debugDummyTarget == null)
            {
                Debug.LogError("SimulationSession: DebugDummyTarget が未設定です。");
            }

            if (timeState == null)
            {
                timeState = new SimulationTimeState();
            }

            // P2 Neutral: 別インスタンスを1つだけ作り、以後書き換えない（全 false のまま）。
            p2NeutralInput = new SimulationInputState();
            p2NeutralInput.ResetToInitialValues();

            timeState.ResetToInitialValues();
            timeState.LastStepResult = "未実行";
            timeState.LastStatusMessage = "Session ready";

            previousAttackHeld = false;
            attackPressedThisTick = false;
        }

        private void Start()
        {
            // Motor.Awake 直後は両体とも右向き初期値のため、開始表示だけ相手向きへ合わせる。
            // Combat 進行ではないので Hit / Action は触らない。
            ApplyInitialFacingTowardOpponents();
            RefreshFighterVisual();
        }

        public void ProcessOneSimulationTick()
        {
            // 1. SimulationTick +1（HitStop中も進む）
            timeState.SimulationTick = timeState.SimulationTick + 1;

            // 2. 入力サンプリング（P1 用 CurrentInput。P2 Neutral は別オブジェクト）
            SampleCurrentInputFromGameplay();

            // 3. Attack 立ち上がり判定（P1 のみ。攻撃状態の共通化は 10B-2）
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

            // 8. Jパンチ開始判定（P1 のみ・既存）
            if (attackPressedThisTick && (timeState.IsActionPlaying == false))
            {
                StartJPunchAttack();
            }

            // 9. 両体移動（ワールド X のみ。Facing はここでは変えない）
            //    P1: Gameplay 入力 / P2: Neutral → 棒立ち
            ProcessOneFighterMovement(participantP1, ResolveInputForParticipant(participantP1));
            ProcessOneFighterMovement(participantP2, ResolveInputForParticipant(participantP2));

            // 10. 両体 Facing 確定（Hit 判定より前）
            UpdateFacingTowardOpponent(participantP1);
            UpdateFacingTowardOpponent(participantP2);

            // 11. ActionFrame 進行
            if (timeState.IsActionPlaying)
            {
                timeState.ActionFrame = timeState.ActionFrame + 1;
                if (timeState.ActionFrame < 0)
                {
                    timeState.ActionFrame = 0;
                }
            }

            // 12. Active なら Hit 判定 → Dummy通知 → HitStopRemaining=6 設定
            //     Facing は 10 で確定済み。P2 位置は P2 Motor.LogicalX を読む。
            TryResolveJPunchHit();

            // 13. Visual 更新
            RefreshFighterVisual();

            // 14. Action 終了判定（未Hitなら Miss ログ1回）
            if (timeState.IsJPunchAttack && timeState.IsActionPlaying)
            {
                int endFrame = 12;
                DebugFighterVisual p1Visual = DebugFighterVisual;
                if (p1Visual != null)
                {
                    endFrame = p1Visual.ActionEndFrame;
                }

                if (timeState.ActionFrame >= endFrame)
                {
                    EndJPunchAttack();
                    RefreshFighterVisual();
                }
            }

            // 15. 状態文 / previousAttackHeld
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
            if (timeState == null)
            {
                return;
            }

            // P1: 既存の攻撃状態を反映
            if (participantP1 != null && participantP1.Visual != null)
            {
                participantP1.Visual.Apply(
                    timeState.IsActionPlaying,
                    timeState.ActionFrame,
                    timeState.IsJPunchAttack
                );
            }

            // P2: 今回は攻撃しないため Idle のまま（共通 Visual 経路は通す）
            if (participantP2 != null && participantP2.Visual != null)
            {
                participantP2.Visual.Apply(false, 0, false);
            }
        }

        /// <summary>
        /// 参加枠へ 1 CombatFrame 分の移動を依頼します。
        /// Facing は変えません。
        /// </summary>
        private void ProcessOneFighterMovement(
            DebugFighterParticipant participant,
            SimulationInputState input)
        {
            if (participant == null)
            {
                return;
            }

            if (participant.Motor == null)
            {
                return;
            }

            participant.Motor.ProcessOneCombatFrame(input);
        }

        /// <summary>
        /// Participant の UsesGameplayInput に応じて入力を選びます。
        /// P1: CurrentInput / P2: 専用 Neutral（共有・静的・毎tick new しない）。
        /// </summary>
        private SimulationInputState ResolveInputForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return p2NeutralInput;
            }

            if (participant.UsesGameplayInput)
            {
                return timeState.CurrentInput;
            }

            return p2NeutralInput;
        }

        /// <summary>
        /// 移動後の論理座標から、self が相手と向き合う Facing を決めます（段階10A/10B-1）。
        ///
        /// - selfX が opponentX より小さい → 右向き
        /// - selfX が opponentX より大きい → 左向き
        /// - ほぼ同位置 → 直前 Facing を維持
        ///
        /// 入力 Left/Right では呼ばない。P1 専用に増築せず、両体で同じ処理を使います。
        /// </summary>
        private void UpdateFacingTowardOpponent(DebugFighterParticipant self)
        {
            if (self == null || self.Motor == null)
            {
                return;
            }

            DebugFighterParticipant opponent = self.Opponent;
            if (opponent == null || opponent.Motor == null)
            {
                return;
            }

            float selfX = self.Motor.LogicalX;
            float opponentX = opponent.Motor.LogicalX;
            float deltaX = opponentX - selfX;

            // 同位置付近では向きを切り替えない。
            // 理由: 浮動小数の微小差やすれ違い直後に、毎 CombatFrame で flipX が点滅するのを防ぐため。
            if (deltaX > FacingSameXEpsilon)
            {
                self.Motor.SetFacingRight(true);
            }
            else if (deltaX < -FacingSameXEpsilon)
            {
                self.Motor.SetFacingRight(false);
            }
            else
            {
                // |deltaX| <= epsilon: 直前 Facing を維持（何もしない）
            }
        }

        private void ApplyInitialFacingTowardOpponents()
        {
            UpdateFacingTowardOpponent(participantP1);
            UpdateFacingTowardOpponent(participantP2);
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
        /// 攻撃状態はまだ Session/TimeState 側（P1専用）。位置は両 Motor から読む。
        /// </summary>
        private void TryResolveJPunchHit()
        {
            if (timeState.IsJPunchAttack == false)
            {
                return;
            }

            DebugFighterMotor p1Motor = DebugFighterMotor;
            DebugFighterMotor p2Motor = null;
            if (participantP2 != null)
            {
                p2Motor = participantP2.Motor;
            }

            if (p1Motor == null || p2Motor == null || debugDummyTarget == null)
            {
                return;
            }

            bool isHit = DebugPunchHitResolver.TryResolveHit(
                timeState.IsJPunchAttack,
                timeState.ActionFrame,
                PunchActiveStartFrame,
                PunchActiveEndFrame,
                timeState.HasCurrentJPunchHit,
                p1Motor.LogicalX,
                p1Motor.FacingRight,
                p2Motor.LogicalX,
                attackRange
            );

            if (isHit == false)
            {
                return;
            }

            // DummyTarget へ通知（HitCount のみ。座標は Motor 側）
            debugDummyTarget.ReceiveHit(timeState.CombatFrame);
            timeState.HasCurrentJPunchHit = true;
            timeState.LastAttackResult = "Hit";
            timeState.LastStatusMessage = "Punch Hit";

            Debug.Log(
                "[FightDebug] Punch hit Dummy at CombatFrame=" + timeState.CombatFrame
            );

            // 既存 HitStop を開始（同tickでは減らさない）
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

            string p1Part = "";
            DebugFighterMotor p1Motor = DebugFighterMotor;
            if (p1Motor != null)
            {
                string facingLabel = p1Motor.FacingRight ? "true" : "false";
                p1Part =
                    " P1X=" + p1Motor.LogicalX.ToString("0.00")
                    + " P1FacingRight=" + facingLabel;
            }

            string visualLabel = "Idle";
            DebugFighterVisual p1Visual = DebugFighterVisual;
            if (p1Visual != null)
            {
                visualLabel = p1Visual.CurrentVisualLabel;
            }

            string p2Part = "";
            if (participantP2 != null && participantP2.Motor != null)
            {
                string p2FacingLabel = participantP2.Motor.FacingRight ? "true" : "false";
                p2Part =
                    " P2X=" + participantP2.Motor.LogicalX.ToString("0.00")
                    + " P2FacingRight=" + p2FacingLabel;
            }

            string dummyHitPart = "";
            if (debugDummyTarget != null)
            {
                dummyHitPart = " DummyHitCount=" + debugDummyTarget.HitCount;
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
                + p1Part
                + p2Part
                + dummyHitPart
                + " PunchPhase=" + GetPunchPhaseLabel()
                + " AttackResult=" + attackResult
                + " PunchHitDone=" + punchHitDone
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
