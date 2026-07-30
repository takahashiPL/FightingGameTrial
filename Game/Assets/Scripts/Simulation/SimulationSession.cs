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
    /// 段階15:
    /// - J Punch の固定設定（Frame / Damage / HitStop / HitStun / KB / Hit Box local）は
    ///   DebugAttackData.JPunch が正本。Session は進行と適用のみ行い、数値の正本にしない
    /// - ScriptableObject / Inspector 編集はまだしない（参照経路の整理が目的）
    ///
    /// 段階14B:
    /// - HP 0 到達時に defender.TryEnterKnockout を一度だけ呼ぶ
    /// - KO 中は入力移動・新規攻撃・被 Hit（追加 Damage/HitStop 等）を禁止
    /// - 最後の一撃の HitStop / HitStun / Knockback は通常どおり成立させる
    /// - Round 終了・勝敗表示はしない
    ///
    /// 段階14A:
    /// - 有効 Hit 成立時に defender.ApplyDamage を1回だけ呼ぶ（Damage は攻撃データ）
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
        /// <summary>
        /// J Punch 設定の参照（正本は DebugAttackData.JPunch。毎 Frame new しない）。
        /// </summary>
        private static readonly DebugAttackData JPunchData = DebugAttackData.JPunch;
        private static readonly DebugAttackData KickData = DebugAttackData.Kick;

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

        [Tooltip(
            "true なら Jump started / apex / landed のイベントログを出します。"
            + " false でもジャンプ処理は動きます。文字列生成はこのフラグが true のときだけ行います。"
        )]
        [SerializeField]
        private bool enableJumpDebugLog = true;

        [Header("Debug（Clash 再現用・本番機能ではない）")]
        [Tooltip(
            "true のとき、P1 が地上攻撃を開始した同じ CombatFrame で P2 も同じ技を開始します。"
            + " P2 手動操作が無い環境で Ground Clash を確認するための最小 Debug です。"
            + " 通常プレイでは false のままにしてください。"
        )]
        [SerializeField]
        private bool debugForceP2AttackWithP1ForClashTest = false;

        /// <summary>
        /// P2 専用の Neutral 入力です。
        /// P1 の CurrentInput とは別インスタンスで、毎tick new しません。
        /// 静的共有値にもしません（誤って書き換えられるのを防ぐため）。
        /// </summary>
        private SimulationInputState p2NeutralInput;

        /// <summary>
        /// 両方向 Hit 候補の再利用バッファ（毎 Frame new しない）。
        /// </summary>
        private readonly DebugPendingHit pendingHitP1ToP2 = new DebugPendingHit();
        private readonly DebugPendingHit pendingHitP2ToP1 = new DebugPendingHit();

        /// <summary>
        /// HUD 用: 直近の Hit 解決結果（None / NormalHit / Clash）。
        /// </summary>
        private DebugHitResolutionType lastHitResolutionType = DebugHitResolutionType.None;

        /// <summary>
        /// HUD 用: 直近 CombatFrame の Push 中心間距離（補正後）。
        /// </summary>
        private float lastPushCenterDistance;

        /// <summary>
        /// ジャンプ上入力の前 tick 保持（P1）。押下エッジ検出用。毎 tick new しない。
        /// </summary>
        private bool previousUpHeldP1;

        /// <summary>
        /// ジャンプ上入力の前 tick 保持（P2）。
        /// </summary>
        private bool previousUpHeldP2;

        /// <summary>
        /// Training Reset 後の共通 release gate。
        /// true の間は物理 Held を観測しつつ、Simulation へ渡す有効入力を全ニュートラルにする。
        /// Left/Right/Up/Down/Attack/Kick がすべて離れた tick で解除する（R は含めない）。
        /// </summary>
        private bool waitForAllGameplayInputReleaseAfterReset;

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

            // 4b. ジャンプ用 Up 押下エッジ（HitStop 中も前状態を更新し、解除後の誤エッジを防ぐ）
            bool jumpPressedP1 = SampleJumpUpPressedThisTick(inputP1, ref previousUpHeldP1);
            bool jumpPressedP2 = SampleJumpUpPressedThisTick(inputP2, ref previousUpHeldP2);

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

            // 8. 攻撃開始（Punch を Kick より先に判定 → 同時押しは Punch 優先）
            //    同一 CombatFrame で Up と攻撃が同時でも、攻撃開始を Jump より先に試みる。
            TryStartJPunchForParticipant(participantP1);
            TryStartJPunchForParticipant(participantP2);
            TryStartGroundKickForParticipant(participantP1);
            TryStartGroundKickForParticipant(participantP2);
            TryForceP2AttackWithP1ForClashDebug();

            // 8b. ジャンプ開始（地上・非 Attack・非 Landing。空中再ジャンプなし）
            TryStartJumpForParticipant(participantP1, inputP1, jumpPressedP1);
            TryStartJumpForParticipant(participantP2, inputP2, jumpPressedP2);
            FlushJumpDebugLogsForParticipant(participantP1);
            FlushJumpDebugLogsForParticipant(participantP2);

            // 9. 両体移動（地上: 入力移動 / 空中: ジャンプ軌道。HitStun・KO 中は入力移動スキップ）
            ProcessOneFighterMovement(participantP1, inputP1);
            ProcessOneFighterMovement(participantP2, inputP2);
            // 頂点・着地イベントは空中軌道更新後に発生するため、ここで消費してログする
            FlushJumpDebugLogsForParticipant(participantP1);
            FlushJumpDebugLogsForParticipant(participantP2);

            // 10. ノックバック移動＋減速（段階13A）
            //     Hit 成立フレームでは速度セットが後段のため、ここではまだ動かない。
            //     HitStop 終了後の Combat から適用。Time.deltaTime は使わない。
            //     Push より前に動かし、めり込みは同フレームの Push で解消する。
            ProcessKnockbackForParticipant(participantP1);
            ProcessKnockbackForParticipant(participantP2);

            // 11. Push Box 重なり解消（空中で十分高いときはスキップして飛び越え可能）
            ResolvePushBoxBetweenParticipants(true);

            // 12. 両体 Facing 確定（Push 後の最終位置基準。Hit 判定より前）
            UpdateFacingTowardOpponent(participantP1);
            UpdateFacingTowardOpponent(participantP2);

            // 13. ActionFrame 進行（Participant 単位）
            AdvanceActionForParticipant(participantP1);
            AdvanceActionForParticipant(participantP2);

            // 14. Hit 候補収集 → 結果決定 → 適用
            //     即適用しない理由: 同じ CombatFrame の両方向を揃えてから
            //     NormalHit / Ground Clash を決めるため（処理順で片側だけ有利にしない）。
            CollectAndResolveHitsForCombatFrame();

            // 15. Visual 更新（Attack / Kick / Jump / Walk / Idle。HitStun・KO は専用 State）
            //     同一 CombatFrame の本更新なので歩行 elapsed を進める。
            RefreshFighterVisual(true);

            // 16. 攻撃終了判定（AttackState 側。Punch / Kick 共通）
            TryEndAttackForParticipant(participantP1);
            TryEndAttackForParticipant(participantP2);
            // 攻撃終了で State が変わった場合の再適用。同一 CombatFrame のため elapsed は進めない。
            RefreshFighterVisual(false);

            // 17. HitStun 消費（Combat Frame 末尾・1回だけ。0 ならノックバック残速度もクリア）
            TickHitStunForParticipant(participantP1);
            TickHitStunForParticipant(participantP2);

            // 18. 状態文 / ログ
            UpdateStatusMessage();
            WriteConsoleLogIfNeeded();
        }

        /// <summary>
        /// HUD / ログ用: 現在参照している J Punch 攻撃データ。
        /// </summary>
        public DebugAttackData JPunchAttackData
        {
            get { return JPunchData; }
        }

        public DebugAttackData KickAttackData
        {
            get { return KickData; }
        }

        /// <summary>HUD 用: 直近 CombatFrame の Hit 解決結果。</summary>
        public DebugHitResolutionType LastHitResolutionType
        {
            get { return lastHitResolutionType; }
        }

        /// <summary>
        /// HUD用: 現在再生中攻撃の区間ラベル（Idle / Startup / Active / Recovery）。
        /// Punch / Kick 共通。未再生は Idle。
        /// </summary>
        public string GetPunchPhaseLabel()
        {
            if (participantP1 == null || participantP1.AttackState == null)
            {
                return "Idle";
            }

            DebugFighterAttackState attackState = participantP1.AttackState;
            if (attackState.IsActionPlaying == false || attackState.CurrentAttackData == null)
            {
                return "Idle";
            }

            DebugAttackPhase phase = attackState.CurrentPhase;
            if (phase == DebugAttackPhase.Startup)
            {
                return "Startup";
            }

            if (phase == DebugAttackPhase.Active)
            {
                return "Active";
            }

            if (phase == DebugAttackPhase.Recovery)
            {
                return "Recovery";
            }

            return "Idle";
        }

        /// <summary>
        /// 両 Participant の Visual を更新します。
        /// advanceVisualAnimationFrame が true のときだけ、同じ State の経過 CombatFrame を進めます。
        /// </summary>
        public void RefreshFighterVisual(bool advanceVisualAnimationFrame)
        {
            RefreshOneFighterVisual(participantP1, advanceVisualAnimationFrame);
            RefreshOneFighterVisual(participantP2, advanceVisualAnimationFrame);
        }

        /// <summary>
        /// 互換: 経過加算なしで両体 Visual を更新します（Reset / Start 時など）。
        /// </summary>
        public void RefreshFighterVisual()
        {
            RefreshFighterVisual(false);
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

            // 空中攻撃は今回未実装（A キーも地上のみ）
            if (participantP1.Motor != null && participantP1.Motor.IsGrounded == false)
            {
                return;
            }

            attackState.StartJPunch(timeState != null ? timeState.CombatFrame : 0);
            RefreshOneFighterVisual(participantP1, false);

            if (timeState != null)
            {
                timeState.LastStatusMessage = "Test Action start P1";
            }

            Debug.Log("[FightDebug] Test Action started slot=P1");
        }

        /// <summary>
        /// Rキー用: 練習モードの Training Reset（段階12A＋位置・向き・ジャンプ復帰）。
        ///
        /// 何をするか:
        /// - P1/P2 の AttackState・HitState・HP・KO・表示色を Reset
        /// - P1/P2 の論理位置 X/Y とジャンプ状態を Scene 開始時へ戻す（Motor）
        /// - 初期配置に基づき互いに向き合う Facing を再適用
        /// - 共有 HitStopRemaining を 0、Sampled 入力をクリア
        /// - 共通 release gate を立て、全ゲーム操作を一度離すまで有効入力をニュートラル化
        /// - 未消費の Jump debug event を破棄（Motor Reset 経路）
        /// - Visual を Idle へ即時更新
        ///
        /// SimulationTick は進めません。Pause 中でも呼べます。
        /// ClockDriver は Reset 受理フレームで通常 tick へ進まないこと（同一フレーム再処理防止）。
        /// 抑制中に再度 R を押した場合も再実行してよい（gate は維持・解除しない）。
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

            // 位置を先に戻し、その配置から Facing を決め直す（Training Reset）。
            // Motor 側で Jump 計測・pending started/apex/landed も破棄する。
            ResetParticipantLogicalPositionAndJump(participantP1);
            ResetParticipantLogicalPositionAndJump(participantP2);
            ApplyInitialFacingTowardOpponents();

            // ------------------------------------------------------------
            // 共通 release gate
            //
            // なぜ全操作を一度離すまで無効化するか:
            // Reset 直後に押しっぱなしの Left/Up/J 等をそのまま通すと、
            // 意図しない移動・再ジャンプ・再攻撃が始まるため。
            //
            // 一部だけ離しても解除しない（全ゲーム操作が false になるまで待つ）。
            // R 自体は DebugPlaybackInput 側であり、解除条件に含めない。
            //
            // 有効入力は SampleCurrentInputFromGameplay 側でニュートラル化する。
            // 物理 Held（DebugGameplayInput）は消さない。
            // ------------------------------------------------------------
            waitForAllGameplayInputReleaseAfterReset = true;

            if (timeState != null && timeState.CurrentInput != null)
            {
                timeState.CurrentInput.ResetToInitialValues();
            }

            if (p2NeutralInput != null)
            {
                p2NeutralInput.ResetToInitialValues();
            }

            // 有効入力はニュートラル前提なので、エッジ用 previous も false に揃える。
            // （個別の Up/Attack 押しっぱなし抑制は共通 gate に一本化した）
            ResyncGameplayInputEdgePreviousAfterReleaseGate();

            if (timeState != null)
            {
                timeState.HitStopRemaining = 0;
                timeState.LastStatusMessage = "Training reset";
            }

            // Visual を Idle に戻し、歩行 elapsed / JumpStart・Apex 残留を捨てる
            ResetFighterVisualToIdle(participantP1);
            ResetFighterVisualToIdle(participantP2);
            RefreshFighterVisual(false);
            Debug.Log(
                "[FightDebug] Training reset"
                + " (Attack/HitStun/Knockback/HitStop/HP/KO/LogicalX/LogicalY/Jump/Facing)"
            );
        }

        private static void ResetFighterVisualToIdle(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Visual == null)
            {
                return;
            }

            participant.Visual.ResetToIdle();
        }

        /// <summary>
        /// 共通 release gate 解除時・Reset 時に、エッジ検出用 previous をニュートラルへ再同期します。
        /// 解除直後サンプルで偽エッジ（Up/Attack）を出さないためです。
        /// </summary>
        private void ResyncGameplayInputEdgePreviousAfterReleaseGate()
        {
            previousUpHeldP1 = false;
            previousUpHeldP2 = false;
            ClearAttackEdgePreviousForParticipant(participantP1);
            ClearAttackEdgePreviousForParticipant(participantP2);
        }

        private static void ClearAttackEdgePreviousForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            participant.AttackState.ClearAttackEdgePrevious();
        }

        /// <summary>
        /// Participant の Motor 論理位置とジャンプ状態を Scene 開始時へ戻します。
        /// </summary>
        private void ResetParticipantLogicalPositionAndJump(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Motor == null)
            {
                return;
            }

            participant.Motor.ResetLogicalPositionAndJumpToInitial();
        }

        /// <summary>
        /// 互換ヘルパー（旧名）。位置＋ジャンプ Reset へ委譲します。
        /// </summary>
        private void ResetParticipantLogicalXToInitial(DebugFighterParticipant participant)
        {
            ResetParticipantLogicalPositionAndJump(participant);
        }

        /// <summary>
        /// 1体分の Visual を AttackState / HitState / KO / Jump / 移動入力から反映します。
        ///
        /// Sprite 優先:
        /// KO → HitStun → Attack(Punch) → Kick → JumpStart → JumpRise → JumpApex → JumpFall → Landing
        /// → WalkForward / WalkBackward → Idle
        ///
        /// 色は Participant.ApplyDisplayColor（HitStun 赤 &gt; KO 暗色 &gt; 通常）。
        /// color は Visual では触らない。
        /// </summary>
        private void RefreshOneFighterVisual(
            DebugFighterParticipant participant,
            bool advanceVisualAnimationFrame)
        {
            if (participant == null || participant.Visual == null)
            {
                return;
            }

            FighterVisualState visualState = ResolveFighterVisualState(participant);
            participant.Visual.Apply(visualState, advanceVisualAnimationFrame);
            participant.ApplyDisplayColor();
        }

        /// <summary>
        /// 1体の Visual State を決定します（見た目の正本決定。Sprite 差し替えは Visual）。
        ///
        /// 優先: KO → HitStun → Attack → Kick → JumpStart → JumpRise → JumpApex → JumpFall → Landing → Walk → Idle
        /// </summary>
        private FighterVisualState ResolveFighterVisualState(DebugFighterParticipant participant)
        {
            if (participant.IsKnockedOut)
            {
                return FighterVisualState.KO;
            }

            if (participant.IsInHitStun)
            {
                return FighterVisualState.HitStun;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState != null && participant.Visual != null)
            {
                if (participant.Visual.IsAttackPoseActive(
                    attackState.IsActionPlaying,
                    attackState.ActionFrame,
                    attackState.IsJPunchAttack))
                {
                    return FighterVisualState.Attack;
                }

                int kickTotal = KickData.TotalFrames;
                if (participant.Visual.IsKickPoseActive(
                    attackState.IsActionPlaying,
                    attackState.ActionFrame,
                    attackState.IsGroundKickAttack,
                    kickTotal))
                {
                    return FighterVisualState.Kick;
                }
            }

            DebugFighterMotor motor = participant.Motor;
            if (motor != null)
            {
                if (motor.IsGrounded == false)
                {
                    return ResolveAirborneJumpVisualState(participant.Visual, motor);
                }

                if (motor.IsLanding)
                {
                    return FighterVisualState.Landing;
                }
            }

            return ResolveLocomotionVisualState(participant);
        }

        /// <summary>
        /// 空中の Jump Visual State を決めます。
        /// JumpStart / Apex の長さは Visual 側の見た目用パラメータ（軌道は変更しない）。
        /// Apex は Motor の頂点通過フラグ＋経過を使い、毎 Frame の速度 Abs だけでは決めません。
        /// </summary>
        private static FighterVisualState ResolveAirborneJumpVisualState(
            DebugFighterVisual visual,
            DebugFighterMotor motor)
        {
            int jumpStartFrames = 2;
            int jumpApexFrames = 2;
            if (visual != null)
            {
                jumpStartFrames = visual.JumpStartVisualFrames;
                jumpApexFrames = visual.JumpApexVisualFrames;
            }

            // Jump 開始直後は JumpStart（例: Elapsed 1〜2）
            if (motor.JumpElapsedFrames <= jumpStartFrames)
            {
                return FighterVisualState.JumpStart;
            }

            // 頂点通過後の短い見た目窓
            if (motor.HasPassedJumpApex
                && motor.FramesSinceJumpApex >= 0
                && motor.FramesSinceJumpApex < jumpApexFrames)
            {
                return FighterVisualState.JumpApex;
            }

            if (motor.HasPassedJumpApex == false && motor.IsRising)
            {
                return FighterVisualState.JumpRise;
            }

            if (motor.IsRising)
            {
                return FighterVisualState.JumpRise;
            }

            return FighterVisualState.JumpFall;
        }

        /// <summary>
        /// 左右入力と Facing から前進／後退／停止の Visual State を決めます。
        ///
        /// 右向き+右 / 左向き+左 → WalkForward
        /// 右向き+左 / 左向き+右 → WalkBackward
        /// 入力なし・左右同時 → Idle
        ///
        /// 実座標の変化は見ない（入力意図ベース。壁際でも入力があれば歩行 State）。
        /// Push / Knockback による移動は歩行にしない（入力が無いため）。
        /// </summary>
        private FighterVisualState ResolveLocomotionVisualState(DebugFighterParticipant participant)
        {
            if (participant.Motor == null)
            {
                return FighterVisualState.Idle;
            }

            SimulationInputState input = ResolveInputForParticipant(participant);
            if (input == null)
            {
                return FighterVisualState.Idle;
            }

            bool left = input.Left;
            bool right = input.Right;

            // Motor と同じ: Left+Right 同時は移動なし → Idle
            if (left && right)
            {
                return FighterVisualState.Idle;
            }

            if (left == false && right == false)
            {
                return FighterVisualState.Idle;
            }

            bool facingRight = participant.Motor.FacingRight;

            if (facingRight)
            {
                if (right)
                {
                    return FighterVisualState.WalkForward;
                }

                return FighterVisualState.WalkBackward;
            }

            // 左向き
            if (left)
            {
                return FighterVisualState.WalkForward;
            }

            return FighterVisualState.WalkBackward;
        }

        /// <summary>
        /// Participant 単位で Attack / Kick ボタンの立ち上がりをサンプリングします。
        /// HitStop中でも呼ばれ、ActionFrame進行とは分離しています。
        /// Held（押しっぱなし）ではエッジは立ちません。
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
            bool kickHeldNow = false;

            if (input != null)
            {
                attackHeldNow = input.Attack;
                kickHeldNow = input.Kick;
            }

            participant.AttackState.SampleAttackButtons(attackHeldNow, kickHeldNow);
        }

        /// <summary>
        /// 地上 J Punch 開始。Kick より先に呼ばれ、同時押しは Punch 優先。
        /// </summary>
        private void TryStartJPunchForParticipant(DebugFighterParticipant participant)
        {
            if (CanStartGroundAttack(participant) == false)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState.AttackPressedThisTick == false)
            {
                return;
            }

            attackState.StartJPunch(timeState.CombatFrame);
            LogAttackStarted(participant, JPunchData);
        }

        /// <summary>
        /// 地上 Ground Kick 開始。Punch 開始の後に呼ぶ（同時押しで Punch が先に取った場合は IsActionPlaying で弾く）。
        /// </summary>
        private void TryStartGroundKickForParticipant(DebugFighterParticipant participant)
        {
            if (CanStartGroundAttack(participant) == false)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState.KickPressedThisTick == false)
            {
                return;
            }

            attackState.StartGroundKick(timeState.CombatFrame);
            LogAttackStarted(participant, KickData);
        }

        /// <summary>
        /// 地上攻撃共通ゲート: KO / HitStun / 空中 / 他 Action 中は不可。
        /// </summary>
        private static bool CanStartGroundAttack(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return false;
            }

            if (participant.IsKnockedOut)
            {
                return false;
            }

            if (participant.IsInHitStun)
            {
                return false;
            }

            if (participant.Motor != null && participant.Motor.IsGrounded == false)
            {
                return false;
            }

            if (participant.AttackState.IsActionPlaying)
            {
                return false;
            }

            return true;
        }

        private static void LogAttackStarted(
            DebugFighterParticipant participant,
            DebugAttackData attackData)
        {
            Debug.Log(
                "[FightDebug] Attack started slot=" + participant.SlotId
                + " attack=" + attackData.AttackId
                + " S/A/R=" + attackData.StartupFrames
                + "/" + attackData.ActiveFrames
                + "/" + attackData.RecoveryFrames
                + " Damage=" + attackData.Damage
                + " HitStop=" + attackData.HitStopFrames
                + " HitStun=" + attackData.HitStunFrames
                + " KB=" + attackData.KnockbackInitialVelocityX.ToString("0.000")
            );
        }

        /// <summary>
        /// Clash 確認用 Debug: P1 が今フレーム攻撃を開始したら P2 も同じ技を開始する。
        /// </summary>
        private void TryForceP2AttackWithP1ForClashDebug()
        {
            if (debugForceP2AttackWithP1ForClashTest == false)
            {
                return;
            }

            if (participantP1 == null
                || participantP2 == null
                || participantP1.AttackState == null
                || participantP2.AttackState == null)
            {
                return;
            }

            DebugFighterAttackState p1 = participantP1.AttackState;
            if (p1.IsActionPlaying == false || p1.ActionFrame != 0)
            {
                return;
            }

            if (CanStartGroundAttack(participantP2) == false)
            {
                return;
            }

            if (p1.CurrentAttackId == DebugAttackId.JPunch)
            {
                participantP2.AttackState.StartJPunch(timeState.CombatFrame);
                LogAttackStarted(participantP2, JPunchData);
                Debug.Log("[FightDebug] ClashDebug: forced P2 JPunch with P1");
            }
            else if (p1.CurrentAttackId == DebugAttackId.GroundKick)
            {
                participantP2.AttackState.StartGroundKick(timeState.CombatFrame);
                LogAttackStarted(participantP2, KickData);
                Debug.Log("[FightDebug] ClashDebug: forced P2 GroundKick with P1");
            }
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
        /// Punch / Kick 共通の攻撃終了。終了条件の正本は CurrentAttackData.TotalFrames。
        /// </summary>
        private void TryEndAttackForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.AttackState == null)
            {
                return;
            }

            DebugFighterAttackState attackState = participant.AttackState;
            if (attackState.IsActionPlaying == false || attackState.CurrentAttackData == null)
            {
                return;
            }

            DebugAttackData attackData = attackState.CurrentAttackData;
            if (attackData.IsFinished(attackState.ActionFrame))
            {
                string attackLabel = attackData.AttackId;
                attackState.EndAttack();

                Debug.Log(
                    "[FightDebug] Attack ended slot=" + participant.SlotId
                    + " attack=" + attackLabel
                );
            }
        }

        /// <summary>互換: 旧名。</summary>
        private void TryEndJPunchForParticipant(DebugFighterParticipant participant)
        {
            TryEndAttackForParticipant(participant);
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
        /// 参加枠へ 1 CombatFrame 分の移動／ジャンプ軌道を依頼します。
        /// Facing は変えません。HitStun / KO 中は入力移動・空中制御を適用しません。
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

            DebugFighterMotor motor = participant.Motor;

            // 空中軌道は HitStun / KO 中も重力落下を進める（入力による空中制御は Motor 側で入力 null 相当にできる）
            if (motor.IsGrounded == false)
            {
                if (participant.IsKnockedOut || participant.IsInHitStun)
                {
                    // プレイヤー空中制御なし（Up/左右を無視した軌道継続）
                    motor.ProcessOneAirborneCombatFrame(null);
                }
                else
                {
                    motor.ProcessOneAirborneCombatFrame(input);
                }

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
        /// Up の押下エッジを検出し、前 tick 保持を更新します（HitStop 前でも呼ぶ）。
        /// </summary>
        private static bool SampleJumpUpPressedThisTick(
            SimulationInputState input,
            ref bool previousUpHeld)
        {
            bool upHeldNow = false;
            if (input != null)
            {
                upHeldNow = input.Up;
            }

            bool pressed = upHeldNow && previousUpHeld == false;
            previousUpHeld = upHeldNow;
            return pressed;
        }

        /// <summary>
        /// 上入力エッジでジャンプ開始を試みます。
        /// Attack 再生中・空中・Landing・HitStun・KO では開始しません。
        /// </summary>
        private void TryStartJumpForParticipant(
            DebugFighterParticipant participant,
            SimulationInputState input,
            bool upPressedThisTick)
        {
            if (participant == null || participant.Motor == null)
            {
                return;
            }

            if (upPressedThisTick == false)
            {
                return;
            }

            if (participant.IsKnockedOut || participant.IsInHitStun)
            {
                return;
            }

            DebugFighterMotor motor = participant.Motor;
            if (motor.IsGrounded == false)
            {
                return;
            }

            if (motor.IsLanding)
            {
                return;
            }

            if (participant.AttackState != null && participant.AttackState.IsActionPlaying)
            {
                return;
            }

            FighterJumpType jumpType = ResolveJumpType(motor.FacingRight, input);
            if (jumpType == FighterJumpType.None)
            {
                return;
            }

            bool started = motor.TryStartJump(jumpType);
            if (started && timeState != null)
            {
                timeState.LastStatusMessage = "Jump " + jumpType;
            }
        }

        /// <summary>
        /// Motor が立てたジャンプ計測イベントを消費し、slot 付きでログします。
        ///
        /// なぜ Session がログするか: slot（P1/P2）を知るのは参加枠側だからです。
        /// なぜ毎フレーム呼ばないか: イベントがあるときだけ文字列を作り、GC を抑えるためです。
        /// enableJumpDebugLog=false でも Consume して旗を下ろし、古いログの遅延出力を防ぎます。
        /// </summary>
        private void FlushJumpDebugLogsForParticipant(DebugFighterParticipant participant)
        {
            if (participant == null || participant.Motor == null)
            {
                return;
            }

            DebugFighterMotor motor = participant.Motor;
            string slotLabel = participant.SlotId.ToString();

            FighterJumpType startedType;
            float startedX;
            float startedY;
            bool startedFacing;
            if (motor.TryConsumeJumpStartedDebug(
                out startedType,
                out startedX,
                out startedY,
                out startedFacing))
            {
                if (enableJumpDebugLog)
                {
                    Debug.Log(
                        "[FightDebug] Jump started"
                        + " slot=" + slotLabel
                        + " type=" + startedType
                        + " startX=" + startedX.ToString("0.00")
                        + " startY=" + startedY.ToString("0.00")
                        + " facingRight=" + (startedFacing ? "true" : "false")
                    );
                }
            }

            FighterJumpType apexType;
            int apexElapsed;
            int apexHeld;
            int apexDirHeld;
            float apexHeight;
            float apexXDistance;
            if (motor.TryConsumeJumpApexDebug(
                out apexType,
                out apexElapsed,
                out apexHeld,
                out apexDirHeld,
                out apexHeight,
                out apexXDistance))
            {
                if (enableJumpDebugLog)
                {
                    Debug.Log(
                        "[FightDebug] Jump apex"
                        + " slot=" + slotLabel
                        + " type=" + apexType
                        + " elapsed=" + apexElapsed
                        + " jumpHeld=" + apexHeld
                        + " directionHeld=" + apexDirHeld
                        + " height=" + apexHeight.ToString("0.00")
                        + " xDistance=" + apexXDistance.ToString("0.00")
                    );
                }
            }

            FighterJumpType landedType;
            int landedTotal;
            int landedHeld;
            int landedDirHeld;
            float landedMaxHeight;
            float landedHoriz;
            float landedStartX;
            float landedEndX;
            bool landedFacing;
            if (motor.TryConsumeJumpLandedDebug(
                out landedType,
                out landedTotal,
                out landedHeld,
                out landedDirHeld,
                out landedMaxHeight,
                out landedHoriz,
                out landedStartX,
                out landedEndX,
                out landedFacing))
            {
                if (enableJumpDebugLog)
                {
                    Debug.Log(
                        "[FightDebug] Jump landed"
                        + " slot=" + slotLabel
                        + " type=" + landedType
                        + " totalFrames=" + landedTotal
                        + " jumpHeld=" + landedHeld
                        + " directionHeld=" + landedDirHeld
                        + " maxHeight=" + landedMaxHeight.ToString("0.00")
                        + " horizontalDistance=" + landedHoriz.ToString("0.00")
                        + " startX=" + landedStartX.ToString("0.00")
                        + " endX=" + landedEndX.ToString("0.00")
                        + " facingRight=" + (landedFacing ? "true" : "false")
                    );
                }
            }
        }

        /// <summary>
        /// Facing と左右入力から Neutral / Forward / Backward を決めます。
        /// 左右同時は Neutral。JumpType 自体は開始後に変更しません。
        /// </summary>
        private static FighterJumpType ResolveJumpType(bool facingRight, SimulationInputState input)
        {
            if (input == null)
            {
                return FighterJumpType.Neutral;
            }

            bool left = input.Left;
            bool right = input.Right;

            if (left && right)
            {
                return FighterJumpType.Neutral;
            }

            if (left == false && right == false)
            {
                return FighterJumpType.Neutral;
            }

            if (facingRight)
            {
                if (right)
                {
                    return FighterJumpType.Forward;
                }

                return FighterJumpType.Backward;
            }

            if (left)
            {
                return FighterJumpType.Forward;
            }

            return FighterJumpType.Backward;
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
            // 減速量は J Punch 攻撃データ（被弾共通設定ではなく、現状 J Punch 固有値）。
            participant.TickKnockbackVelocityForCombatFrame(
                JPunchData.KnockbackDecelerationPerCombatFrame
            );
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
        /// 空中で十分な高さがあるとき横 Push をスキップするか。
        /// 開始直後の低高度ではすり抜けず、頂点付近で飛び越え可能にする。
        /// </summary>
        private static bool ShouldSkipPushForAerialSeparation(
            DebugFighterMotor motorA,
            DebugFighterMotor motorB)
        {
            if (motorA == null || motorB == null)
            {
                return false;
            }

            // 両方地上なら通常 Push
            if (motorA.IsGrounded && motorB.IsGrounded)
            {
                return false;
            }

            float thresholdA = motorA.JumpSettings.PushBoxVerticalSeparationThreshold;
            float thresholdB = motorB.JumpSettings.PushBoxVerticalSeparationThreshold;
            float threshold = thresholdA;
            if (thresholdB > threshold)
            {
                threshold = thresholdB;
            }

            float heightA = motorA.HeightAboveGround;
            float heightB = motorB.HeightAboveGround;
            float maxHeightAboveGround = heightA;
            if (heightB > maxHeightAboveGround)
            {
                maxHeightAboveGround = heightB;
            }

            float absDeltaY = Mathf.Abs(motorA.LogicalY - motorB.LogicalY);

            // どちらかが十分高く、かつ互いの Y 差も閾値以上
            if (maxHeightAboveGround >= threshold && absDeltaY >= threshold)
            {
                return true;
            }

            return false;
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

            // 十分高い空中では横 Push をスキップし、飛び越えを許可する。
            if (ShouldSkipPushForAerialSeparation(participantP1.Motor, participantP2.Motor))
            {
                float airDist = Mathf.Abs(
                    participantP2.Motor.LogicalX - participantP1.Motor.LogicalX
                );
                lastPushCenterDistance = airDist;
                lastPushWasOverlapping = false;
                previousPushDidCorrect = false;
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
        /// 同じ CombatFrame の両方向 Hit 候補を先に集め、
        /// 片方向だけなら NormalHit、双方なら Ground Clash として解決します。
        /// </summary>
        private void CollectAndResolveHitsForCombatFrame()
        {
            pendingHitP1ToP2.Clear();
            pendingHitP2ToP1.Clear();
            lastHitResolutionType = DebugHitResolutionType.None;

            CollectPendingHit(participantP1, participantP2, pendingHitP1ToP2);
            CollectPendingHit(participantP2, participantP1, pendingHitP2ToP1);

            if (pendingHitP1ToP2.IsValid && pendingHitP2ToP1.IsValid)
            {
                ApplyGroundClash(pendingHitP1ToP2, pendingHitP2ToP1);
                lastHitResolutionType = DebugHitResolutionType.Clash;
                return;
            }

            if (pendingHitP1ToP2.IsValid)
            {
                ApplyNormalHit(pendingHitP1ToP2);
                lastHitResolutionType = DebugHitResolutionType.NormalHit;
                return;
            }

            if (pendingHitP2ToP1.IsValid)
            {
                ApplyNormalHit(pendingHitP2ToP1);
                lastHitResolutionType = DebugHitResolutionType.NormalHit;
            }
        }

        /// <summary>
        /// 片方向の Hit 候補だけを作ります。ここでは Damage 等をまだ適用しません。
        /// </summary>
        private void CollectPendingHit(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender,
            DebugPendingHit destination)
        {
            destination.Clear();

            if (attacker == null || defender == null || attacker == defender)
            {
                return;
            }

            DebugFighterAttackState attackState = attacker.AttackState;
            if (attackState == null
                || attackState.IsActionPlaying == false
                || attackState.CurrentAttackData == null
                || defender.IsKnockedOut)
            {
                return;
            }

            DebugBox2D hitBox = attacker.EvaluateWorldHitBox();
            DebugBox2D hurtBox = defender.EvaluateWorldHurtBox();

            string checkLabel;
            bool boxesOverlap;
            bool isCandidate = DebugPunchHitResolver.TryResolveHit(
                attackState.IsActionPlaying,
                attackState.HasCurrentAttackHit,
                hitBox,
                hurtBox,
                out checkLabel,
                out boxesOverlap
            );

            if (attacker == participantP1 && defender == participantP2)
            {
                lastHitCheckLabel = checkLabel;
                lastBoxOverlap = boxesOverlap;
            }

            if (isCandidate == false)
            {
                return;
            }

            destination.IsValid = true;
            destination.Attacker = attacker;
            destination.Defender = defender;
            destination.AttackId = attackState.CurrentAttackId;
            destination.AttackData = attackState.CurrentAttackData;
            destination.AttackStartedCombatFrame = attackState.AttackStartedCombatFrame;
            destination.HitCombatFrame = timeState.CombatFrame;
            destination.DistanceX = Mathf.Abs(
                defender.Motor.LogicalX - attacker.Motor.LogicalX
            );

            DebugFighterAttackState defenderAttack = defender.AttackState;
            destination.DefenderWasAttacking =
                defenderAttack != null && defenderAttack.IsActionPlaying;
            if (destination.DefenderWasAttacking)
            {
                destination.DefenderAttackId = defenderAttack.CurrentAttackId;
                destination.DefenderAttackPhase = defenderAttack.CurrentPhase;
            }

            destination.HitBox = hitBox;
            destination.HurtBox = hurtBox;
        }

        private void ApplyNormalHit(DebugPendingHit pendingHit)
        {
            if (pendingHit == null
                || pendingHit.IsValid == false
                || pendingHit.Attacker == null
                || pendingHit.Defender == null
                || pendingHit.AttackData == null)
            {
                return;
            }

            DebugFighterParticipant attacker = pendingHit.Attacker;
            DebugFighterParticipant defender = pendingHit.Defender;
            DebugAttackData attackData = pendingHit.AttackData;

            float knockbackVelocityX = ResolveKnockbackVelocityX(
                attacker,
                defender,
                attackData.KnockbackInitialVelocityX
            );

            defender.ReceiveHit(
                timeState.CombatFrame,
                attackData.HitStunFrames,
                knockbackVelocityX
            );

            int actualDamage = defender.ApplyDamage(attackData.Damage);
            if (defender.CurrentHitPoints <= 0)
            {
                defender.TryEnterKnockout();
            }

            if (attacker.AttackState != null)
            {
                attacker.AttackState.MarkHit();
            }

            RefreshOneFighterVisual(defender, false);
            timeState.HitStopRemaining = attackData.HitStopFrames;
            timeState.LastStatusMessage =
                attacker.SlotId + " " + attackData.AttackId + " Hit";

            Debug.Log(
                "[FightDebug] Normal hit"
                + " attack=" + attackData.AttackId
                + " attacker=" + attacker.SlotId
                + " defender=" + defender.SlotId
                + " CombatFrame=" + timeState.CombatFrame
                + " Damage=" + attackData.Damage
                + " actual=" + actualDamage
                + " distanceX=" + pendingHit.DistanceX.ToString("0.00")
            );
        }

        private void ApplyGroundClash(
            DebugPendingHit p1ToP2,
            DebugPendingHit p2ToP1)
        {
            DebugFighterParticipant p1 = p1ToP2.Attacker;
            DebugFighterParticipant p2 = p2ToP1.Attacker;
            if (p1 == null || p2 == null)
            {
                return;
            }

            float p1Knockback = ResolveKnockbackVelocityX(
                p2,
                p1,
                DebugClashTuning.HorizontalKnockback
            );
            float p2Knockback = ResolveKnockbackVelocityX(
                p1,
                p2,
                DebugClashTuning.HorizontalKnockback
            );

            if (p1.AttackState != null)
            {
                p1.AttackState.EndAttackAsClash();
            }

            if (p2.AttackState != null)
            {
                p2.AttackState.EndAttackAsClash();
            }

            p1.ReceiveHit(
                timeState.CombatFrame,
                DebugClashTuning.ClashStunFrames,
                p1Knockback
            );
            p2.ReceiveHit(
                timeState.CombatFrame,
                DebugClashTuning.ClashStunFrames,
                p2Knockback
            );

            timeState.HitStopRemaining = DebugClashTuning.HitStopFrames;
            timeState.LastStatusMessage = "Ground Clash";

            Debug.Log(
                "[FightDebug] Ground Clash"
                + " CombatFrame=" + timeState.CombatFrame
                + " P1Attack=" + p1ToP2.AttackId
                + " P2Attack=" + p2ToP1.AttackId
                + " Damage=0"
            );
        }

        private static float ResolveKnockbackVelocityX(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender,
            float speed)
        {
            speed = Mathf.Abs(speed);
            if (speed == 0f
                || attacker == null
                || defender == null
                || attacker.Motor == null
                || defender.Motor == null)
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

            return attacker.Motor.FacingRight ? speed : -speed;
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
            // HitStun / KB 初速の「値」は攻撃データ、適用結果は HitState。
            // ※ KO へ至る最後の一撃でも、ここで Stun/KB を先にセットする。
            defender.ReceiveHit(
                timeState.CombatFrame,
                JPunchData.HitStunFrames,
                knockbackVelocityX
            );

            // Damage は有効 Hit 確定時に1回だけ（段階14A）。値は攻撃データ。
            int requestedDamage = JPunchData.Damage;
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

            RefreshOneFighterVisual(defender, false);

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
            // 両方向 Hit でも同じ攻撃データのため、単純代入でよい。
            // KO へ至る最後の一撃でも HitStop は通常どおり開始する。
            timeState.HitStopRemaining = JPunchData.HitStopFrames;

            return true;
        }

        /// <summary>
        /// ノックバック初速の符号付き値を決めます（段階13A / 15）。
        ///
        /// 正本は Hit 成立時点の LogicalX 比較（Facing だけを正本にしない）。
        /// attacker.X &lt; defender.X → 右（+初速）
        /// attacker.X &gt; defender.X → 左（-初速）
        /// 同位置 → attacker の Facing を fallback（右向きなら +、左向きなら -）
        /// 大きさは攻撃データ KnockbackInitialVelocityX（絶対値）。
        /// </summary>
        private static float ResolveKnockbackVelocityX(
            DebugFighterParticipant attacker,
            DebugFighterParticipant defender)
        {
            float speed = JPunchData.KnockbackInitialVelocityX;
            if (speed == 0f)
            {
                return 0f;
            }

            if (attacker == null
                || defender == null
                || attacker.Motor == null
                || defender.Motor == null)
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

            // 物理 Held は DebugGameplayInput から読む（消さない）。
            // 共通 release gate 中は、この物理値で解除判定だけ行い、
            // Simulation へ渡す有効入力はニュートラルへ落とす。
            bool left = false;
            bool right = false;
            bool up = false;
            bool down = false;
            bool attack = false;
            bool kick = false;

            if (debugGameplayInput != null)
            {
                left = debugGameplayInput.IsLeftPressed;
                right = debugGameplayInput.IsRightPressed;
                up = debugGameplayInput.IsUpPressed;
                down = debugGameplayInput.IsDownPressed;
                attack = debugGameplayInput.IsAttackPressed;
                kick = debugGameplayInput.IsKickPressed;
            }

            if (waitForAllGameplayInputReleaseAfterReset)
            {
                // ------------------------------------------------------------
                // 全ゲーム操作が離れるまで有効入力を通さない。
                // R は DebugPlaybackInput のためここには含まれない。
                //
                // 一部だけ離しても解除しない。
                // 全解除したサンプルでも有効入力はニュートラルのままにし、
                // previous エッジを false へ再同期して偽エッジを防ぐ。
                // 次の新しい押下から通常受付を再開する。
                // ------------------------------------------------------------
                bool anyGameplayHeld = SimulationInputState.HasAnyGameplayInputHeld(
                    left,
                    right,
                    up,
                    down,
                    attack,
                    kick);

                timeState.CurrentInput.CopyFromPhysicalAndCommit(
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    timeState.SimulationTick);

                if (anyGameplayHeld == false)
                {
                    waitForAllGameplayInputReleaseAfterReset = false;
                    ResyncGameplayInputEdgePreviousAfterReleaseGate();
                    Debug.Log("[FightDebug] Gameplay input re-enabled after Training Reset");
                }

                return;
            }

            timeState.CurrentInput.CopyFromPhysicalAndCommit(
                left,
                right,
                up,
                down,
                attack,
                kick,
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
