using FightingGameTrial.Combat;
using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    ///
    /// 段階10B-2:
    /// - 攻撃状態（入力・開始・ActionFrame・終了・Visual）は各 Participant.AttackState
    /// - Hit 判定は attacker / defender 共通関数で行い、defender.ReceiveHit で被弾を記録する
    /// - 同一 CombatFrame 内で P1→P2 と P2→P1 の両方を判定する（相打ち将来対応）
    /// - P2 は Neutral 入力のため、現状は通常攻撃しない（棒立ち）
    /// - SimulationTimeState は共有時間状態のみ（攻撃状態は持たない）
    ///
    /// 段階10B-1（維持）:
    /// - P1/P2 を同じ Participant + Motor + Visual 構成で扱う
    /// - P1 は Gameplay 入力、P2 は Neutral 入力（棒立ち）
    /// - 両体の移動後、Hit 前に双方の Facing を相手向きへ確定する
    ///
    /// 段階10A（維持）:
    /// - 移動入力と Facing を分離する
    /// - Facing 確定のあとで Hit 判定を行う
    /// - ほぼ同位置なら直前 Facing を維持する
    ///
    /// 1 CombatFrame 内の処理順:
    /// 1. P1/P2 入力解決
    /// 2. P1/P2 攻撃入力サンプリング（HitStop判定より前）
    /// 3. （HitStop中なら Combat をスキップして return）
    /// 4. CombatFrame +1
    /// 5. BeginCombatFrame（P1/P2 被弾旗リセット）
    /// 6. P1/P2 攻撃開始判定
    /// 7. P1 移動 / P2 移動
    /// 8. P1 Facing / P2 Facing
    /// 9. P1/P2 ActionFrame 進行
    /// 10. P1→P2 Hit / P2→P1 Hit（共通関数）
    /// 11. Visual 更新（AttackState 反映）
    /// 12. P1/P2 攻撃終了判定
    ///
    /// HitStop中:
    /// Combat 処理全体をスキップするため、移動も Facing も Action も進めない。
    /// ただし攻撃入力の SampleAttackInput は HitStop 前に行い、
    /// previousAttackHeld 相当の更新だけは維持する（既存仕様）。
    /// Hit 成立 tick では HitStopRemaining を設定するだけで、同じ tick 内では減らさない
    /// （次 tick 先頭から Combat 停止が始まる）。
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

        [Tooltip("P1 参加枠です。Motor / Visual / Opponent / AttackState を内包します。")]
        [SerializeField]
        private DebugFighterParticipant participantP1;

        [Tooltip("P2 参加枠です。Motor / Visual / Opponent / AttackState を内包します。")]
        [SerializeField]
        private DebugFighterParticipant participantP2;

        [Header("Jパンチ判定（段階9・暫定）")]
        [Tooltip(
            "Jパンチの攻撃距離（ワールド単位）です。"
            + " 向いている側に相手がいて、この距離以内なら Active で Hit します。"
        )]
        [SerializeField]
        private float attackRange = 1.35f;

        [Header("時間状態")]
        [Tooltip(
            "SimulationTick / CombatFrame / Pause / HitStop など共有時間状態を保持します。"
            + " 攻撃状態は各 Participant.AttackState が正本です。"
        )]
        [SerializeField]
        private SimulationTimeState timeState = new SimulationTimeState();

        [Header("Consoleログ（毎tickは出さない）")]
        [Tooltip("何 SimulationTick ごとに1回ログを出すか。60なら約1秒に1回です。")]
        [SerializeField]
        private int consoleLogIntervalTicks = 60;

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

        /// <summary>
        /// HUD 互換用。P1 の AttackState から Attack 立ち上がりを返します。
        /// </summary>
        public bool AttackPressedThisTick
        {
            get
            {
                if (participantP1 == null || participantP1.AttackState == null)
                {
                    return false;
                }

                return participantP1.AttackState.AttackPressedThisTick;
            }
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

            // 2. 物理入力 → P1 用 CurrentInput（P2 Neutral は別オブジェクト）
            SampleCurrentInputFromGameplay();

            // 3. P1/P2 入力を一度だけ解決（以後の移動・攻撃サンプリングで共用）
            SimulationInputState inputP1 = ResolveInputForParticipant(participantP1);
            SimulationInputState inputP2 = ResolveInputForParticipant(participantP2);

            // 4. 攻撃入力サンプリング（HitStop判定より前。ActionFrame進行とは分離）
            SampleAttackInputForParticipant(participantP1, inputP1);
            SampleAttackInputForParticipant(participantP2, inputP2);

            // 5. 既存 HitStop 判定
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
                    Debug.Log("[FightDebug] HitStop ended");
                }

                WriteConsoleLogIfNeeded();
                return;
            }

            // 6. CombatFrame +1
            timeState.CombatFrame = timeState.CombatFrame + 1;

            // 7. 被弾旗リセット（Participant 単位）
            BeginCombatFrameForParticipant(participantP1);
            BeginCombatFrameForParticipant(participantP2);

            // 8. Jパンチ開始判定（Participant 単位。P2 は Neutral のため通常は開始しない）
            TryStartJPunchForParticipant(participantP1);
            TryStartJPunchForParticipant(participantP2);

            // 9. 両体移動（ワールド X のみ。Facing はここでは変えない）
            ProcessOneFighterMovement(participantP1, inputP1);
            ProcessOneFighterMovement(participantP2, inputP2);

            // 10. 両体 Facing 確定（Hit 判定より前）
            UpdateFacingTowardOpponent(participantP1);
            UpdateFacingTowardOpponent(participantP2);

            // 11. ActionFrame 進行（Participant 単位）
            AdvanceActionForParticipant(participantP1);
            AdvanceActionForParticipant(participantP2);

            // 12. Hit 判定（attacker / defender 共通。同一tickで両方向を評価してから HitStop）
            TryResolveJPunchHit(participantP1, participantP2);
            TryResolveJPunchHit(participantP2, participantP1);

            // 13. Visual 更新（各 AttackState を反映）
            RefreshFighterVisual();

            // 14. 攻撃終了判定（AttackState 側）
            TryEndJPunchForParticipant(participantP1);
            TryEndJPunchForParticipant(participantP2);
            RefreshFighterVisual();

            // 15. 状態文 / ログ
            UpdateStatusMessage();
            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// HUD互換用: Idle / Startup / Active / Recovery（P1 AttackState 参照）。
        /// </summary>
        public string GetPunchPhaseLabel()
        {
            if (participantP1 == null || participantP1.AttackState == null)
            {
                return "Idle";
            }

            DebugFighterAttackState attackState = participantP1.AttackState;
            if (attackState.IsJPunchAttack == false || attackState.IsActionPlaying == false)
            {
                return "Idle";
            }

            int frame = attackState.ActionFrame;
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

        /// <summary>
        /// 両 Participant の Visual を、各自の AttackState から更新します。
        /// </summary>
        public void RefreshFighterVisual()
        {
            RefreshOneFighterVisual(participantP1);
            RefreshOneFighterVisual(participantP2);
        }

        /// <summary>
        /// Aキー用: P1 の AttackState でテスト用パンチを開始します（段階10B-2整理）。
        ///
        /// 何をするか: AttackState.StartJPunch → P1 Visual 即時更新。
        /// なぜ Session 経由か: ClockDriver が Participant を検索せず、所有は Session が持つため。
        ///
        /// SimulationTick は進めません。Pause 中でも呼べます。
        /// 進行・Hit・終了は次以降の通常 ProcessOneSimulationTick（Participant 共通経路）に任せます。
        /// PreviousAttackHeld / AttackPressedThisTick は変更しません（通常 J 入力経路を壊さない）。
        /// </summary>
        public void StartTestActionForP1()
        {
            if (participantP1 == null || participantP1.AttackState == null)
            {
                Debug.LogError("SimulationSession: StartTestActionForP1 に P1 AttackState がありません。");
                return;
            }

            DebugFighterAttackState attackState = participantP1.AttackState;

            // すでに再生中なら二重開始しない（通常 J 開始と同じ）。
            if (attackState.IsActionPlaying)
            {
                return;
            }

            attackState.StartJPunch();
            RefreshOneFighterVisual(participantP1);

            if (timeState != null)
            {
                timeState.LastStatusMessage = "Test Action start P1";
            }

            Debug.Log("[FightDebug] Test Action started slot=P1");
        }

        /// <summary>
        /// Rキー用: P1 の AttackState を初期状態へ戻します（段階10B-2整理）。
        ///
        /// 何をするか: AttackState.Reset → P1 Visual を Idle へ即時更新。
        /// SimulationTick は進めません。Pause 中でも呼べます。
        /// Reset 後は J を一度離して再度押せば、通常攻撃を開始できる状態になります。
        /// </summary>
        public void ResetTestActionForP1()
        {
            if (participantP1 == null || participantP1.AttackState == null)
            {
                Debug.LogError("SimulationSession: ResetTestActionForP1 に P1 AttackState がありません。");
                return;
            }

            participantP1.AttackState.Reset();
            RefreshOneFighterVisual(participantP1);

            if (timeState != null)
            {
                timeState.LastStatusMessage = "Test Action reset P1";
            }

            Debug.Log("[FightDebug] Test Action reset slot=P1");
        }

        /// <summary>
        /// 1体分の Visual を AttackState から反映します。
        /// Sprite のみ切替し、color（Tint）は触りません。
        /// </summary>
        private void RefreshOneFighterVisual(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Visual == null)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;

            if (attackState == null)
            {
                participant.Visual.Apply(false, 0, false);
                return;
            }

            participant.Visual.Apply(
                attackState.IsActionPlaying,
                attackState.ActionFrame,
                attackState.IsJPunchAttack
            );
        }

        /// <summary>
        /// Participant 単位で攻撃ボタンの立ち上がりをサンプリングします。
        /// HitStop中でも呼ばれ、ActionFrame進行とは分離しています。
        /// </summary>
        private void SampleAttackInputForParticipant(
            DebugFighterParticipant participant,
            SimulationInputState input)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            bool attackHeldNow = false;

            if (input != null)
            {
                attackHeldNow = input.Attack;
            }

            participant.AttackState.SampleAttackInput(attackHeldNow);
        }

        /// <summary>
        /// Participant 単位で Jパンチ開始を試みます。
        /// AttackPressedThisTick かつ未 Action のときだけ開始します。
        /// </summary>
        private void TryStartJPunchForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;

            if (attackState.AttackPressedThisTick == false)
            {
                return;
            }

            if (attackState.IsActionPlaying)
            {
                return;
            }

            attackState.StartJPunch();

            Debug.Log(
                "[FightDebug] Attack started slot=" + participant.SlotId
            );
        }

        /// <summary>
        /// Participant 単位で ActionFrame を1進めます。
        /// </summary>
        private void AdvanceActionForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            participant.AttackState.AdvanceActionFrame();
        }

        /// <summary>
        /// Participant 単位で Jパンチ終了を試みます。
        /// AttackState.EndJPunch のみを使い、専用の別進行は持ちません。
        /// </summary>
        private void TryEndJPunchForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState.IsJPunchAttack == false || attackState.IsActionPlaying == false)
            {
                return;
            }

            int endFrame = 12;
            if (participant.Visual != null)
            {
                endFrame = participant.Visual.ActionEndFrame;
            }

            if (attackState.ActionFrame >= endFrame)
            {
                attackState.EndJPunch();

                Debug.Log(
                    "[FightDebug] Attack ended slot=" + participant.SlotId
                );
            }
        }

        /// <summary>
        /// CombatFrame 開始時に、この参加者の被弾フラグを下ろします。
        /// </summary>
        private void BeginCombatFrameForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            participant.BeginCombatFrame();
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

        /// <summary>
        /// attacker → defender の Jパンチ Hit を判定します（段階10B-2）。
        ///
        /// 何をするか:
        /// - attacker.AttackState と両 Motor の論理座標で距離・向き込み判定
        /// - 成立時に defender.ReceiveHit / attackState.MarkHit / HitStop 設定
        ///
        /// なぜ attacker / defender 形式か:
        /// P1→P2 と P2→P1 を同じ関数で扱い、専用分岐を増やさないため。
        ///
        /// 1攻撃1Hit:
        /// attackState.HasCurrentJPunchHit が正本。MarkHit 後は同じ攻撃で再Hitしない。
        ///
        /// HitStop:
        /// 成立 tick では Remaining を代入するだけ。同じ tick 内では減らさない。
        /// 次 tick 先頭の HitStop 判定から Combat 停止が始まる（既存仕様）。
        ///
        /// 攻撃結果の正本は attacker.AttackState.LastAttackResult のみです。
        /// </summary>
        private bool TryResolveJPunchHit(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender)
        {
            if (attacker == null || defender == null)
            {
                return false;
            }

            if (attacker.AttackState == null)
            {
                return false;
            }

            if (attacker.Motor == null || defender.Motor == null)
            {
                return false;
            }

            DebugFighterAttackState attackState = attacker.AttackState;

            bool isHit = DebugPunchHitResolver.TryResolveHit(
                attackState.IsJPunchAttack,
                attackState.ActionFrame,
                PunchActiveStartFrame,
                PunchActiveEndFrame,
                attackState.HasCurrentJPunchHit,
                attacker.Motor.LogicalX,
                attacker.Motor.FacingRight,
                defender.Motor.LogicalX,
                attackRange
            );

            if (isHit == false)
            {
                return false;
            }

            // 被弾記録は defender（Participant）側が所有する
            defender.ReceiveHit(timeState.CombatFrame);

            // 1攻撃1Hit の正本は attacker の AttackState
            attackState.MarkHit();

            timeState.LastStatusMessage =
                attacker.SlotId.ToString() + " Punch Hit";

            Debug.Log(
                "[FightDebug] Punch hit"
                + " attacker=" + attacker.SlotId
                + " defender=" + defender.SlotId
                + " CombatFrame=" + timeState.CombatFrame
            );

            // 既存 HitStop を開始（同tickでは減らさない）
            // 両方向 Hit でも同じ定数のため、単純代入でよい。
            timeState.HitStopRemaining = PunchHitStopFrames;

            return true;
        }

        private void UpdateStatusMessage()
        {
            DebugFighterAttackState p1Attack = null;
            if (participantP1 != null)
            {
                p1Attack = participantP1.AttackState;
            }

            if (p1Attack != null && p1Attack.IsActionPlaying)
            {
                if (p1Attack.IsJPunchAttack)
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

            DebugFighterAttackState p1Attack = null;
            if (participantP1 != null)
            {
                p1Attack = participantP1.AttackState;
            }

            bool isActionPlaying = false;
            int actionFrame = 0;
            bool punchHitDoneFlag = false;
            string attackResult = "None";

            if (p1Attack != null)
            {
                isActionPlaying = p1Attack.IsActionPlaying;
                actionFrame = p1Attack.ActionFrame;
                punchHitDoneFlag = p1Attack.HasCurrentJPunchHit;
                attackResult = p1Attack.LastAttackResult;
            }

            if (string.IsNullOrEmpty(attackResult))
            {
                attackResult = "None";
            }

            string actionPlayingLabel = isActionPlaying ? "true" : "false";

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
            int attackPressedValue = AttackPressedThisTick ? 1 : 0;
            int punchHitDone = punchHitDoneFlag ? 1 : 0;

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

            string p2HitPart = "";
            if (participantP2 != null)
            {
                p2HitPart = " P2HitCount=" + participantP2.HitCount;
            }

            Debug.Log(
                "[FightDebug] SimulationTick=" + timeState.SimulationTick
                + " / CombatFrame=" + timeState.CombatFrame
                + " / ActionFrame=" + actionFrame
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
                + p2HitPart
                + " PunchPhase=" + GetPunchPhaseLabel()
                + " AttackResult=" + attackResult
                + " PunchHitDone=" + punchHitDone
                + "\n（" + timeState.LastStatusMessage + "）"
            );
        }
    }
}
