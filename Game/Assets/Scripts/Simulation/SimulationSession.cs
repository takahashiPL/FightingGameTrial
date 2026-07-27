using FightingGameTrial.Combat;
using FightingGameTrial.Fighter;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 1回分の論理 SimulationTick を進める入口です。
    ///
    /// 段階10B-3:
    /// - 両体移動のあと、Facing 更新の前に Participant 共通 Push Box で重なりを解消する
    /// - Facing は Push 補正後の最終位置を基準に更新する
    /// - P1 専用停止ではなく、DebugFighterPushResolver で双方へ等分補正する
    ///
    /// 段階11B:
    /// - Jパンチ Hit は距離ではなく Hit Box × Hurt Box の重なりで判定する
    /// - 可視化と同じ EvaluateWorldHitBox / EvaluateWorldHurtBox を実判定でも使う
    /// - 旧 attackRange 距離判定は使用しない
    ///
    /// 段階14B:
    /// - HP 0 到達時に defender.TryEnterKnockout を一度だけ呼ぶ
    /// - KO 中は入力移動・新規攻撃・被 Hit（追加 Damage/HitStop 等）を禁止
    /// - 最後の一撃の HitStop / HitStun / Knockback は通常どおり成立させる
    /// - Round 終了・勝敗表示はしない
    ///
    /// 段階14A:
    /// - 有効 Hit 成立時に defender.ApplyDamage を1回だけ呼ぶ（J Punch 暫定 Damage=10）
    /// - HP 正本は各 Participant。HitState / Motor / Push は HP に触れない
    ///
    /// 段階13B-1:
    /// - Push 補正でステージ端（Motor minX/maxX）により片方の実移動が足りないとき、
    ///   未解消量をもう片方へ再配分する（DebugFighterPushResolver + TryMoveLogicalXBy）
    /// - KnockbackVelocityX は Push で変更しない。Facing は既存どおり Push 後
    ///
    /// 段階13A:
    /// - Hit 成立時に被弾側へ横ノックバック初速を予約する（LogicalX 比較で符号決定）
    /// - HitStop 中は移動・減速しない。HitStop 終了後の Combat から固定フレームで移動
    /// - 入力移動 → ノックバック移動 → Push → Facing → Action → Hit → Visual → HitStun
    /// - ノックバックは Motor.SetLogicalX / TryMoveLogicalXBy 経由
    /// - ステージ端の壁バウンド・壁やられはしない（Push 再配分のみ 13B-1）
    ///
    /// 段階12A:
    /// - 被弾側 Participant が HitState（HitStun / TotalHitCount / ノックバック速度）を所有する
    /// - HitStop 中は HitStun を減らさない。Combat 末尾で1回だけ消費する
    /// - HitStun 中は移動・攻撃開始を止め、Push / Facing は維持する
    ///
    /// 段階10B-2（維持）:
    /// - 攻撃状態（入力・開始・ActionFrame・終了・Visual）は各 Participant.AttackState
    /// - Hit 判定は attacker / defender 共通関数で行い、defender.ReceiveHit で被弾を記録する
    /// - 同一 CombatFrame 内で P1→P2 と P2→P1 の両方を判定する（相打ち将来対応）
    /// - P2 は Neutral 入力のため、現状は通常攻撃しない（棒立ち）
    /// - SimulationTimeState は共有時間状態のみ（攻撃状態は持たない）
    ///
    /// 段階10B-1（維持）:
    /// - P1/P2 を同じ Participant + Motor + Visual 構成で扱う
    /// - P1 は Gameplay 入力、P2 は Neutral 入力（棒立ち）
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
    /// 7. P1/P2 入力移動（HitStun 中はスキップ）
    /// 8. P1/P2 ノックバック移動＋減速（HitStun 中かつ速度あり。1CFに1回）
    /// 9. Push Box 重なり解消（Participant 共通。速度は触らない）
    /// 10. P1 Facing / P2 Facing（Push 後の最終位置基準）
    /// 11. P1/P2 ActionFrame 進行
    /// 12. P1→P2 Hit / P2→P1 Hit（成立時: Damage / KO遷移 / HitStun / KB / HitStop）
    /// 13. Visual 更新（AttackState 反映）
    /// 14. P1/P2 攻撃終了判定
    /// 15. HitStun 消費（0 ならノックバック残速度もクリア）
    ///
    /// HitStop中:
    /// Combat 処理全体をスキップするため、移動も Push も Facing も Action もノックバックも進めない。
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
        /// Jパンチの暫定 Damage（段階14A）。攻撃データ化前。
        /// </summary>
        private const int PunchDamage = 10;

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

        /// <summary>
        /// HUD 用: 直近 CombatFrame の Push 中心間距離（補正後）。
        /// </summary>
        private float lastPushCenterDistance;

        /// <summary>
        /// HUD 用: 直近 CombatFrame で補正前に重なっていたか。
        /// </summary>
        private bool lastPushWasOverlapping;

        /// <summary>
        /// 直前 CombatFrame で Push 補正したか。
        /// 接触し続けている間の毎フレームログを避け、補正開始の立ち上がりだけ出すために使う。
        /// </summary>
        private bool previousPushDidCorrect;

        /// <summary>
        /// 直前 CombatFrame で壁際 Push 再配分を試みたか（段階13B-1）。
        /// 毎フレーム大量ログを避け、立ち上がりだけ出すために使う。
        /// </summary>
        private bool previousPushWallRedistributed;

        /// <summary>
        /// HUD 用: 直近の P1→P2 Hit 評価で Box が重なっていたか（段階11B）。
        /// </summary>
        private bool lastBoxOverlap;

        /// <summary>
        /// HUD 用: 直近の P1→P2 Hit 評価ラベル（Inactive / NoOverlap / Hit / AlreadyHit）。
        /// </summary>
        private string lastHitCheckLabel = DebugPunchHitCheckLabels.Inactive;

        public SimulationTimeState TimeState
        {
            get { return timeState; }
        }

        /// <summary>
        /// HUD 用: 直近の Push 中心間距離（補正後の絶対値）。
        /// </summary>
        public float LastPushCenterDistance
        {
            get { return lastPushCenterDistance; }
        }

        /// <summary>
        /// HUD 用: 直近 CombatFrame で Push 補正前に重なっていたか。
        /// </summary>
        public bool LastPushWasOverlapping
        {
            get { return lastPushWasOverlapping; }
        }

        /// <summary>
        /// HUD 用: 直近 P1→P2 の Hit Box × Hurt Box 重なり。
        /// </summary>
        public bool LastBoxOverlap
        {
            get { return lastBoxOverlap; }
        }

        /// <summary>
        /// HUD 用: 直近 P1→P2 の Hit 評価ラベル。
        /// </summary>
        public string LastHitCheckLabel
        {
            get
            {
                if (string.IsNullOrEmpty(lastHitCheckLabel))
                {
                    return DebugPunchHitCheckLabels.Inactive;
                }

                return lastHitCheckLabel;
            }
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
            // 開始時に初期配置が重なっていた場合だけ Push で離し、その後 Facing を合わせる。
            // Combat 進行ではないので Hit / Action は触らない。
            ResolvePushBoxBetweenParticipants(false);
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

            // 9. 両体移動（ワールド X のみ。Facing はここでは変えない。HitStun 中は入力移動スキップ）
            ProcessOneFighterMovement(participantP1, inputP1);
            ProcessOneFighterMovement(participantP2, inputP2);

            // 10. ノックバック移動＋減速（段階13A）
            //     Hit 成立フレームでは速度セットが後段のため、ここではまだ動かない。
            //     HitStop 終了後の Combat から適用。Time.deltaTime は使わない。
            //     Push より前に動かし、めり込みは同フレームの Push で解消する。
            ProcessKnockbackForParticipant(participantP1);
            ProcessKnockbackForParticipant(participantP2);

            // 11. Push Box 重なり解消（移動後・Facing 前。Participant 共通。ノックバック速度は触らない）
            ResolvePushBoxBetweenParticipants(true);

            // 12. 両体 Facing 確定（Push 後の最終位置基準。Hit 判定より前）
            UpdateFacingTowardOpponent(participantP1);
            UpdateFacingTowardOpponent(participantP2);

            // 13. ActionFrame 進行（Participant 単位）
            AdvanceActionForParticipant(participantP1);
            AdvanceActionForParticipant(participantP2);

            // 14. Hit 判定（attacker / defender 共通。同一tickで両方向を評価してから HitStop）
            TryResolveJPunchHit(participantP1, participantP2);
            TryResolveJPunchHit(participantP2, participantP1);

            // 15. Visual 更新（各 AttackState を反映。HitStun 中は Idle Sprite 優先）
            RefreshFighterVisual();

            // 16. 攻撃終了判定（AttackState 側）
            TryEndJPunchForParticipant(participantP1);
            TryEndJPunchForParticipant(participantP2);
            RefreshFighterVisual();

            // 17. HitStun 消費（Combat Frame 末尾・1回だけ。0 ならノックバック残速度もクリア）
            TickHitStunForParticipant(participantP1);
            TickHitStunForParticipant(participantP2);

            // 18. 状態文 / ログ
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
        /// Rキー用: デバッグ戦闘状態を初期化します（段階12A）。
        ///
        /// 何をするか:
        /// - P1/P2 の AttackState・HitState・表示色を Reset
        /// - 共有 HitStopRemaining を 0
        /// - Visual を Idle へ即時更新
        ///
        /// SimulationTick は進めません。Pause 中でも呼べます。
        /// 位置・Facing の Scene 初期化は従来どおり Play 再開に任せます（R では動かさない）。
        /// </summary>
        public void ResetTestActionForP1()
        {
            if (participantP1 != null)
            {
                participantP1.ResetCombatDebugState();
            }
            else
            {
                Debug.LogError("SimulationSession: ResetTestActionForP1 に P1 がありません。");
            }

            if (participantP2 != null)
            {
                participantP2.ResetCombatDebugState();
            }

            if (timeState != null)
            {
                timeState.HitStopRemaining = 0;
                timeState.LastStatusMessage = "Debug combat reset";
            }

            RefreshFighterVisual();
            Debug.Log("[FightDebug] Debug combat reset (Attack/HitStun/Knockback/HitStop/HP/KO)");
        }

        /// <summary>
        /// 1体分の Visual を AttackState / HitState / KO から反映します。
        ///
        /// 表示優先: KO または HitStun 中は Idle Sprite。色は Participant.ApplyDisplayColor
        /// （KO &gt; HitStun &gt; 通常）。color は Visual では触らない。
        /// </summary>
        private void RefreshOneFighterVisual(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Visual == null)
            {
                return;
            }

            // KO / Hit 表示 > Attack 表示 > Idle
            if (participant.IsKnockedOut || participant.IsInHitStun)
            {
                participant.Visual.Apply(false, 0, false);
                participant.ApplyDisplayColor();
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;

            if (attackState == null)
            {
                participant.Visual.Apply(false, 0, false);
                participant.ApplyDisplayColor();
                return;
            }

            participant.Visual.Apply(
                attackState.IsActionPlaying,
                attackState.ActionFrame,
                attackState.IsJPunchAttack
            );
            participant.ApplyDisplayColor();
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
        /// AttackPressedThisTick かつ未 Action かつ HitStun/KO でないときだけ開始します。
        /// </summary>
        private void TryStartJPunchForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            // KO 中は新規攻撃不可（段階14B）
            if (participant.IsKnockedOut)
            {
                return;
            }

            // HitStun 中は新規攻撃不可（段階12A）
            if (participant.IsInHitStun)
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
        /// Facing は変えません。HitStun / KO 中は移動入力を適用しません（Push / ノックバックは別経路）。
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

            // KO 中は本人の入力移動だけ無効（段階14B）。最後の一撃のノックバックは後段で効く。
            if (participant.IsKnockedOut)
            {
                return;
            }

            // HitStun 中は本人の移動だけ無効。Push / ノックバックは後段で効く。
            if (participant.IsInHitStun)
            {
                return;
            }

            participant.Motor.ProcessOneCombatFrame(input);
        }

        /// <summary>
        /// HitStun 中のノックバックを 1 Combat Frame 分だけ適用します（段階13A）。
        ///
        /// 流れ:
        /// LogicalX += knockbackVelocityX → 速度を deceleration だけ 0 へ近づける。
        /// Time.deltaTime は使わない（固定 Combat Frame 単位）。
        ///
        /// なぜ Hit 成立フレームでは動かないか:
        /// このメソッドは Hit 判定より前に走る。成立時にセットされた初速は
        /// HitStop 終了後の次 Combat から効く。
        ///
        /// Motor との責務:
        /// 速度の正本は HitState。位置書き込みは Motor.SetLogicalX。
        /// 既存 minX/maxX クランプはそのまま（壁バウンドはしない。Push 再配分は Resolver 側）。
        ///
        /// Push との関係:
        /// ノックバック後にめり込んだ場合、同フレームの Push で解消する。
        /// Push は knockbackVelocityX を変更しない。
        /// </summary>
        private void ProcessKnockbackForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            if (participant.Motor == null)
            {
                return;
            }

            // ノックバックは HitStun 中だけ適用する。
            if (participant.IsInHitStun == false)
            {
                return;
            }

            float velocityX = participant.KnockbackVelocityX;
            if (velocityX == 0f)
            {
                return;
            }

            float newX = participant.Motor.LogicalX + velocityX;
            participant.Motor.SetLogicalX(newX);

            // 移動した Combat Frame でのみ減速する（HitStop 中はここへ来ない）。
            participant.TickKnockbackVelocityForCombatFrame();
        }

        /// <summary>
        /// Combat 末尾で HitStun を1減らします（段階12A）。
        ///
        /// HitStop 中（成立 tick で Remaining を立てた直後を含む）は減らさない。
        /// HitStop 外の Combat にだけ到達し、かつ Remaining==0 のときだけ消費する。
        /// HitStun が 0 になると Participant 側でノックバック残速度もクリアする（段階13A）。
        /// </summary>
        private void TickHitStunForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            if (timeState != null && timeState.HitStopRemaining > 0)
            {
                return;
            }

            participant.TickHitStunForCombatFrame();
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
        /// 両 Participant の横方向 Push Box 重なりを解消します（段階10B-3 / 13B-1）。
        ///
        /// 何をするか:
        /// - DebugFighterPushResolver に等分分離と壁際再配分を依頼する
        /// - HUD 用に中心距離・重なり有無を記録する
        /// - 補正開始／壁際再配分の立ち上がりだけ1行ログを出す（常時大量ログは出さない）
        ///
        /// なぜこの順か:
        /// Facing と Hit は最終位置を使うため、移動の直後・Facing の直前で行う。
        /// Push は KnockbackVelocityX を変更しない（位置のみ）。
        ///
        /// logOnCorrect: Combat tick 中のみ true。Start 時の初期離しでは false。
        /// </summary>
        private void ResolvePushBoxBetweenParticipants(bool logOnCorrect)
        {
            if (participantP1 == null || participantP2 == null)
            {
                lastPushCenterDistance = 0f;
                lastPushWasOverlapping = false;
                return;
            }

            if (participantP1.Motor == null || participantP2.Motor == null)
            {
                lastPushCenterDistance = 0f;
                lastPushWasOverlapping = false;
                return;
            }

            float beforeP1X = participantP1.Motor.LogicalX;
            float beforeP2X = participantP2.Motor.LogicalX;

            float centerDistanceBefore;
            float requiredMinDistance;
            bool wasOverlapping;
            bool didWallRedistribute;
            string wallLog;

            bool didCorrect = DebugFighterPushResolver.TryResolveHorizontalOverlap(
                participantP1,
                participantP2,
                out centerDistanceBefore,
                out requiredMinDistance,
                out wasOverlapping,
                out didWallRedistribute,
                out wallLog
            );

            lastPushWasOverlapping = wasOverlapping;

            // HUD には補正後の中心距離を出す（接触中はほぼ最小距離になる）。
            float afterP1X = participantP1.Motor.LogicalX;
            float afterP2X = participantP2.Motor.LogicalX;
            lastPushCenterDistance = Mathf.Abs(afterP2X - afterP1X);

            // 補正開始の立ち上がりだけ1行ログ（押し続け中の毎フレーム出力はしない）。
            if (didCorrect && logOnCorrect && previousPushDidCorrect == false)
            {
                Debug.Log(
                    "[FightDebug] Push correct"
                    + " P1 " + beforeP1X.ToString("0.00") + "->" + afterP1X.ToString("0.00")
                    + " P2 " + beforeP2X.ToString("0.00") + "->" + afterP2X.ToString("0.00")
                    + " dist " + centerDistanceBefore.ToString("0.00")
                    + "->" + lastPushCenterDistance.ToString("0.00")
                    + " min=" + requiredMinDistance.ToString("0.00")
                );
            }

            // 壁際再配分の立ち上がりだけ1行（段階13B-1）。押し続け中は出さない。
            if (didWallRedistribute
                && logOnCorrect
                && previousPushWallRedistributed == false
                && string.IsNullOrEmpty(wallLog) == false)
            {
                Debug.Log("[FightDebug] Push wall redistribute " + wallLog);
            }

            previousPushDidCorrect = didCorrect;
            previousPushWallRedistributed = didWallRedistribute;
        }

        /// <summary>
        /// Push 補正後の論理座標から、self が相手と向き合う Facing を決めます（段階10A/10B-3）。
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
        /// attacker → defender の Jパンチ Hit を判定します（段階11B / 13A / 14A / 14B）。
        ///
        /// 何をするか:
        /// - 可視化と同じ EvaluateWorldHitBox / EvaluateWorldHurtBox を取得
        /// - DebugPunchHitResolver で Active・未Hit・重なりを評価
        /// - 成立時に ReceiveHit / ApplyDamage /（HP0なら）TryEnterKnockout / MarkHit / HitStop
        ///
        /// なぜ距離判定をやめたか:
        /// 赤い Hit Box と緑の Hurt Box の見た目と結果を一致させるため。
        ///
        /// 1攻撃1Hit / 1攻撃1Damage:
        /// attackState.HasCurrentJPunchHit が正本。MarkHit 後は同じ攻撃で再Hitしない。
        ///
        /// KO（段階14B）:
        /// - すでに KO の defender へは Hit を成立させない（Damage/HitStop 等なし）
        /// - 最後の一撃は ReceiveHit→ApplyDamage→KO遷移→HitStop の順で、
        ///   HitStun / Knockback / HitStop を失わない
        /// </summary>
        private bool TryResolveJPunchHit(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender)
        {
            if (attacker == null || defender == null)
            {
                return false;
            }

            if (attacker == defender)
            {
                return false;
            }

            if (attacker.AttackState == null)
            {
                return false;
            }

            // KO 済み防御者への追加 Hit は成立させない（段階14B）。
            // Damage / HitCount / HitStop / HitStun / Knockback を増やさない。
            if (defender.IsKnockedOut)
            {
                if (attacker == participantP1 && defender == participantP2)
                {
                    lastHitCheckLabel = DebugPunchHitCheckLabels.DefenderKO;
                    lastBoxOverlap = false;
                }

                return false;
            }

            DebugFighterAttackState attackState = attacker.AttackState;

            // 可視化（BoxView）と同じ取得経路。独自の Active 範囲や距離式は持たない。
            DebugBox2D hitBox = attacker.EvaluateWorldHitBox();
            DebugBox2D hurtBox = defender.EvaluateWorldHurtBox();

            string checkLabel;
            bool boxesOverlap;
            bool isHit = DebugPunchHitResolver.TryResolveHit(
                attackState.IsJPunchAttack,
                attackState.HasCurrentJPunchHit,
                hitBox,
                hurtBox,
                out checkLabel,
                out boxesOverlap
            );

            // HUD は主に P1→P2 を表示（毎フレーム Miss ログは出さない）
            if (attacker == participantP1 && defender == participantP2)
            {
                lastHitCheckLabel = checkLabel;
                lastBoxOverlap = boxesOverlap;
            }

            if (isHit == false)
            {
                return false;
            }

            // ノックバック符号は Facing ではなく Hit 成立時点の LogicalX 比較で決める。
            // 攻撃者から防御者を遠ざける方向。同位置は attacker Facing を fallback。
            float knockbackVelocityX = ResolveKnockbackVelocityX(attacker, defender);

            // 被弾記録は defender（Participant）側が所有する。
            // HitStun 開始・ノックバック初速予約・被弾側攻撃の中断（段階12A / 13A）。
            // ※ KO へ至る最後の一撃でも、ここで Stun/KB を先にセットする。
            defender.ReceiveHit(
                timeState.CombatFrame,
                defender.HitStunFrames,
                knockbackVelocityX
            );

            // Damage は有効 Hit 確定時に1回だけ（段階14A）。
            int requestedDamage = PunchDamage;
            int actualDamage = defender.ApplyDamage(requestedDamage);

            // HP 0 なら一度だけ KO（段階14B）。既 KO は上で弾いている。
            // 最後の一撃の HitStop はこの後で開始するため失わない。
            bool enteredKnockout = false;
            if (defender.CurrentHitPoints <= 0)
            {
                enteredKnockout = defender.TryEnterKnockout();
                if (enteredKnockout)
                {
                    Debug.Log(
                        "[FightDebug] Fighter KO"
                        + " slot=" + defender.SlotId
                        + " CombatFrame=" + timeState.CombatFrame
                        + " HP=" + defender.CurrentHitPoints
                        + "/" + defender.MaxHitPoints
                    );
                }
            }

            RefreshOneFighterVisual(defender);

            // 1攻撃1Hit の正本は attacker の AttackState
            attackState.MarkHit();

            timeState.LastStatusMessage =
                attacker.SlotId.ToString() + " Punch Hit";

            int koFlag = 0;
            if (defender.IsKnockedOut)
            {
                koFlag = 1;
            }

            Debug.Log(
                "[FightDebug] Punch hit"
                + " attacker=" + attacker.SlotId
                + " defender=" + defender.SlotId
                + " CombatFrame=" + timeState.CombatFrame
                + " Damage=" + requestedDamage
                + " actual=" + actualDamage
                + " HP=" + defender.CurrentHitPoints
                + "/" + defender.MaxHitPoints
                + " KO=" + koFlag
                + " KB=" + knockbackVelocityX.ToString("0.000")
                + " HitBox=[" + hitBox.MinX.ToString("0.00")
                + ".." + hitBox.MaxX.ToString("0.00")
                + "," + hitBox.MinY.ToString("0.00")
                + ".." + hitBox.MaxY.ToString("0.00") + "]"
                + " HurtBox=[" + hurtBox.MinX.ToString("0.00")
                + ".." + hurtBox.MaxX.ToString("0.00")
                + "," + hurtBox.MinY.ToString("0.00")
                + ".." + hurtBox.MaxY.ToString("0.00") + "]"
            );

            // 既存 HitStop を開始（同tickでは減らさない）
            // 両方向 Hit でも同じ定数のため、単純代入でよい。
            // KO へ至る最後の一撃でも HitStop は通常どおり開始する。
            timeState.HitStopRemaining = PunchHitStopFrames;

            return true;
        }

        /// <summary>
        /// ノックバック初速の符号付き値を決めます（段階13A）。
        ///
        /// 正本は Hit 成立時点の LogicalX 比較（Facing だけを正本にしない）。
        /// attacker.X &lt; defender.X → 右（+初速）
        /// attacker.X &gt; defender.X → 左（-初速）
        /// 同位置 → attacker の Facing を fallback（右向きなら +、左向きなら -）
        /// 大きさは defender の暫定 knockbackInitialSpeed。
        /// </summary>
        private static float ResolveKnockbackVelocityX(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender)
        {
            float speed = 0f;
            if (defender != null)
            {
                speed = defender.KnockbackInitialSpeed;
            }

            if (speed == 0f)
            {
                return 0f;
            }

            if (attacker == null || attacker.Motor == null || defender.Motor == null)
            {
                return 0f;
            }

            float attackerX = attacker.Motor.LogicalX;
            float defenderX = defender.Motor.LogicalX;

            if (attackerX < defenderX)
            {
                return speed;
            }

            if (attackerX > defenderX)
            {
                return -speed;
            }

            // 同位置: Facing を fallback（攻撃者が向いている側へ押し出す）
            if (attacker.Motor.FacingRight)
            {
                return speed;
            }

            return -speed;
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
